using GitBinder.Application.Accounts;
using GitBinder.Application.Bindings;
using GitBinder.Application.Common;
using GitBinder.Application.Git;
using GitBinder.Domain.Common;
using GitBinder.Domain.Projects;

namespace GitBinder.Application.Projects;

/// <summary>
/// 项目应用服务：负责仓库登记、校验与 Remote 解析。
/// </summary>
public sealed class ProjectService
{
    private readonly IProjectRepository _repository;
    private readonly IGitService _gitService;
    private readonly IPathNormalizer _pathNormalizer;
    private readonly IAccountRepository _accountRepository;
    private readonly BindingService _bindingService;

    public ProjectService(
        IProjectRepository repository,
        IGitService gitService,
        IPathNormalizer pathNormalizer,
        IAccountRepository accountRepository,
        BindingService bindingService)
    {
        _repository = repository;
        _gitService = gitService;
        _pathNormalizer = pathNormalizer;
        _accountRepository = accountRepository;
        _bindingService = bindingService;
    }

    public Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default)
        => _repository.GetAllAsync(ct);

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _repository.GetByIdAsync(id, ct);

    /// <summary>添加仓库：校验 Git 仓库、解析 Remote。</summary>
    public async Task<Result<Project>> AddAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result<Project>.Failure(new DomainError("PROJECT_PATH_REQUIRED"));
        }

        var canonical = _pathNormalizer.Canonicalize(path);

        // 校验 Git 仓库。
        if (!await _gitService.ValidateRepositoryAsync(canonical, ct))
        {
            return Result<Project>.Failure(new DomainError("PROJECT_REPOSITORY_NOT_FOUND", canonical));
        }

        // 已有登记则拒绝重复。
        var existing = await _repository.FindByPathAsync(canonical, ct);
        if (existing is not null)
        {
            return Result<Project>.Failure(new DomainError("PROJECT_ALREADY_EXISTS", canonical));
        }

        var root = await _gitService.GetRepositoryRootAsync(canonical, ct) ?? canonical;
        var gitDir = await _gitService.GetGitDirAsync(canonical, ct) ?? string.Empty;
        var originUrl = await _gitService.GetOriginUrlAsync(canonical, ct) ?? string.Empty;
        var branch = await _gitService.GetCurrentBranchAsync(canonical, ct) ?? string.Empty;

        var (protocol, host) = _gitService.ParseRemote(originUrl);

        var project = new Project
        {
            Name = Path.GetFileName(_pathNormalizer.Normalize(root)),
            RepositoryPath = root,
            CanonicalPath = _pathNormalizer.Canonicalize(root),
            GitDir = gitDir,
            OriginUrl = originUrl,
            RemoteProtocol = protocol,
            RemoteHost = host,
            CurrentBranch = branch,
        };

        await _repository.AddAsync(project, ct);

        // RULE 03：新项目默认绑定 Default Account。
        var defaultAccount = await _accountRepository.GetDefaultAsync(ct);
        if (defaultAccount is not null)
        {
            await _bindingService.BindAsync(project.Id, defaultAccount.Id, ct);
        }

        return Result<Project>.Success(project);
    }

    /// <summary>移除登记（仅移除记录，不删除源码）。同时清理绑定与快照。</summary>
    public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _repository.GetByIdAsync(id, ct);
        if (project is null)
        {
            return Result.Failure(new DomainError("PROJECT_NOT_FOUND", id.ToString()));
        }

        // 先解绑（恢复仓库配置并删除绑定/快照）。
        await _bindingService.UnbindAsync(id, ct);

        await _repository.DeleteAsync(id, ct);
        return Result.Success();
    }

    /// <summary>更新仓库的 origin 地址；仅修改本地 Git 配置，不进行联网、拉取或推送。</summary>
    public async Task<Result<Project>> UpdateOriginUrlAsync(
        Guid id,
        string originUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(originUrl))
        {
            return Result<Project>.Failure(new DomainError("PROJECT_REMOTE_URL_REQUIRED"));
        }

        var normalizedUrl = originUrl.Trim();
        if (HasHttpUserInfo(normalizedUrl))
        {
            return Result<Project>.Failure(new DomainError("PROJECT_REMOTE_CREDENTIAL_FORBIDDEN"));
        }

        var project = await _repository.GetByIdAsync(id, ct);
        if (project is null)
        {
            return Result<Project>.Failure(new DomainError("PROJECT_NOT_FOUND", id.ToString()));
        }

        if (!await _gitService.ValidateRepositoryAsync(project.RepositoryPath, ct))
        {
            return Result<Project>.Failure(new DomainError("PROJECT_REPOSITORY_NOT_FOUND", project.RepositoryPath));
        }

        var update = await _gitService.SetOriginUrlAsync(project.RepositoryPath, normalizedUrl, ct);
        if (!update.IsSuccess)
        {
            return Result<Project>.Failure(update.Error!);
        }

        project.OriginUrl = await _gitService.GetOriginUrlAsync(project.RepositoryPath, ct) ?? normalizedUrl;
        var (protocol, host) = _gitService.ParseRemote(project.OriginUrl);
        project.RemoteProtocol = protocol;
        project.RemoteHost = host;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(project, ct);
        return Result<Project>.Success(project);
    }

    private static bool HasHttpUserInfo(string originUrl)
    {
        return Uri.TryCreate(originUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            && !string.IsNullOrEmpty(uri.UserInfo);
    }

}
