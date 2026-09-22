using GitBinder.Application.Accounts;
using GitBinder.Application.Bindings;
using GitBinder.Application.Common;
using GitBinder.Application.Git;
using GitBinder.Domain.Common;
using GitBinder.Domain.Projects;
using GitBinder.Domain.Services;

namespace GitBinder.Application.Projects;

/// <summary>克隆后登记/绑定、按有效账号快进拉取；失败不清理目录、不覆盖本地修改。</summary>
public sealed class ProjectTransferService(
    IGitTransfer transfer, ProjectService projects, IProjectRepository repository,
    IAccountRepository accounts, BindingService bindings, EffectiveAccountResolver resolver,
    IPathNormalizer paths, IProjectGroupRepository groups)
{
    private readonly SemaphoreSlim _operation = new(1, 1);

    public async Task<Result> CloneAsync(string remoteUrl, string parentDirectory, string folderName,
        Guid accountId, CancellationToken ct = default)
    {
        if (!await _operation.WaitAsync(0, ct)) return Fail("TRANSFER_BUSY");
        var cloned = false;
        var registered = false;
        try
        {
            if (!GitTransferRemote.TryParse(remoteUrl, out _)) return Fail("TRANSFER_URL_INVALID");
            if (!Path.IsPathFullyQualified(parentDirectory) || !Directory.Exists(parentDirectory))
                return Fail("CLONE_PARENT_INVALID");
            // 目录名只能是一层，不能通过 ../、绝对路径或 NTFS ADS 逃逸到父目录外。
            if (string.IsNullOrWhiteSpace(folderName) || folderName is "." or ".."
                || folderName != folderName.Trim() || folderName.EndsWith('.')
                || folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || folderName.Contains('/') || folderName.Contains('\\')
                || System.Text.RegularExpressions.Regex.IsMatch(folderName,
                    @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])($|\.)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return Fail("CLONE_FOLDER_INVALID");
            var parent = paths.Canonicalize(parentDirectory);
            var target = Path.Combine(parent, folderName);
            if (File.Exists(target) || Directory.Exists(target)) return Fail("CLONE_TARGET_EXISTS");
            if ((await repository.GetAllAsync(ct)).Any(p => paths.Equals(p.RepositoryPath, target)))
                return Fail("PROJECT_ALREADY_EXISTS", target);

            var selected = await accounts.GetByIdAsync(accountId, ct);
            if (selected is null || !selected.Enabled) return Fail("TRANSFER_ACCOUNT_INVALID");
            var effective = resolver.ResolveForClone(selected);
            if (effective is null || !effective.Enabled) return Fail("TRANSFER_ACCOUNT_INVALID");
            var result = await transfer.CloneAsync(remoteUrl.Trim(), target, effective, ct);
            if (!result.IsSuccess) return result;
            cloned = true;

            // 避免先绑定默认账号再改绑，克隆后只应用用户选择的绑定。
            var added = await projects.AddAsync(target, ct, bindDefaultAccount: false);
            if (!added.IsSuccess) return Fail("CLONE_REGISTER_FAILED");
            registered = true;
            var bound = await bindings.BindAsync(added.Value!.Id, selected.Id, ct);
            return bound.IsSuccess ? Result.Success() : Fail("CLONE_BIND_FAILED");
        }
        catch (Exception)
        {
            if (registered) return Fail("CLONE_BIND_FAILED");
            if (cloned) return Fail("CLONE_REGISTER_FAILED");
            return Fail(ct.IsCancellationRequested ? "TRANSFER_CANCELLED" : "CLONE_FAILED");
        }
        finally { _operation.Release(); }
    }

    public async Task<Result> PullAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!await _operation.WaitAsync(0, ct)) return Fail("TRANSFER_BUSY");
        try { return await PullCoreAsync(projectId, ct); }
        finally { _operation.Release(); }
    }

    /// <summary>按完整分组快照依次拉取，与界面搜索结果无关；整个批次共用传输互斥锁。</summary>
    public async Task<Result<GroupPullReport>> PullGroupAsync(Guid? groupId, bool allProjects,
        IProgress<GroupPullProgress>? progress = null, CancellationToken ct = default)
    {
        if (!await _operation.WaitAsync(0, ct))
            return Result<GroupPullReport>.Failure(new DomainError("TRANSFER_BUSY"));
        try
        {
            if (!allProjects && groupId.HasValue && !(await groups.GetAllAsync(ct)).Any(g => g.Id == groupId))
                return Result<GroupPullReport>.Failure(new DomainError("GROUP_NOT_FOUND"));
            var members = await groups.GetMembershipsAsync(ct);
            var selected = (await repository.GetAllAsync(ct)).Where(p => allProjects
                || (members.TryGetValue(p.Id, out var id) ? (Guid?)id : null) == groupId).ToList();
            var outcomes = new List<GroupPullOutcome>();
            foreach (var project in selected)
            {
                if (ct.IsCancellationRequested) break;
                progress?.Report(new(project.Name, outcomes.Count + 1, selected.Count));
                var result = await PullCoreAsync(project.Id, ct);
                outcomes.Add(new(project.Id, project.Name, result.Error));
            }
            return Result<GroupPullReport>.Success(new(selected.Count, outcomes, ct.IsCancellationRequested));
        }
        catch (OperationCanceledException)
        {
            return Result<GroupPullReport>.Failure(new DomainError("TRANSFER_CANCELLED"));
        }
        catch (Exception) { return Result<GroupPullReport>.Failure(new DomainError("TRANSFER_FAILED")); }
        finally { _operation.Release(); }
    }

    private async Task<Result> PullCoreAsync(Guid projectId, CancellationToken ct)
    {
        var pulled = false;
        try
        {
            var project = await repository.GetByIdAsync(projectId, ct);
            if (project is null) return Fail("PROJECT_NOT_FOUND");
            var account = resolver.Resolve(project);
            if (account is null || !account.Enabled) return Fail("TRANSFER_ACCOUNT_INVALID");
            var result = await transfer.PullAsync(project.RepositoryPath, account, ct);
            if (!result.IsSuccess) return result;
            pulled = true;
            var refreshed = await projects.RefreshMetadataAsync(projectId, ct);
            return refreshed.IsSuccess ? Result.Success() : Fail("PULL_METADATA_FAILED");
        }
        catch (Exception)
        {
            if (pulled) return Fail("PULL_METADATA_FAILED");
            return Fail(ct.IsCancellationRequested ? "TRANSFER_CANCELLED" : "PULL_FAILED");
        }
    }

    private static Result Fail(string code, params string[] args) => Result.Failure(new DomainError(code, args));
}

public sealed record GroupPullProgress(string ProjectName, int Index, int Total);
public sealed record GroupPullOutcome(Guid ProjectId, string ProjectName, DomainError? Error);
public sealed record GroupPullReport(int Total, IReadOnlyList<GroupPullOutcome> Outcomes, bool Cancelled)
{
    public int Succeeded => Outcomes.Count(o => o.Error is null);
    public int Failed => Outcomes.Count(o => o.Error is not null && o.Error.Code != "TRANSFER_CANCELLED");
    public int Unfinished => Total - Succeeded - Failed;
}
