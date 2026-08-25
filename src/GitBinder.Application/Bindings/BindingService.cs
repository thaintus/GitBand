using GitBinder.Application.Accounts;
using GitBinder.Application.Git;
using GitBinder.Application.GlobalMode;
using GitBinder.Application.Projects;
using GitBinder.Application.Security;
using GitBinder.Domain.Accounts;
using GitBinder.Domain.Bindings;
using GitBinder.Domain.Common;
using GitBinder.Domain.GlobalMode;
using GitBinder.Domain.Projects;
using GitBinder.Domain.Services;

namespace GitBinder.Application.Bindings;

/// <summary>
/// 绑定应用服务：负责 Project → Account 一对一绑定、改绑、解绑、测试与状态校验。
/// 绑定时会将账号身份应用到仓库，并在此之前做配置快照以便回滚。
/// </summary>
public sealed class BindingService : IGlobalModeApplier
{
    private readonly IBindingRepository _repository;
    private readonly IAccountRepository _accountRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretStore _secretStore;
    private readonly IGitService _gitService;
    private readonly IGitConfigApplier _gitConfigApplier;
    private readonly IRepositorySnapshotRepository _snapshotRepository;
    private readonly EffectiveAccountResolver? _effectiveAccountResolver;

    public BindingService(
        IBindingRepository repository,
        IAccountRepository accountRepository,
        IProjectRepository projectRepository,
        ISecretStore secretStore,
        IGitService gitService,
        IGitConfigApplier gitConfigApplier,
        IRepositorySnapshotRepository snapshotRepository,
        EffectiveAccountResolver? effectiveAccountResolver = null)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _projectRepository = projectRepository;
        _secretStore = secretStore;
        _gitService = gitService;
        _gitConfigApplier = gitConfigApplier;
        _snapshotRepository = snapshotRepository;
        _effectiveAccountResolver = effectiveAccountResolver;
    }

    public Task<IReadOnlyList<Binding>> GetAllAsync(CancellationToken ct = default)
        => _repository.GetAllAsync(ct);

    public Task<Binding?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => _repository.GetByProjectIdAsync(projectId, ct);

    /// <summary>绑定或改绑（一个 Project 只绑定一个 Account），并将账号身份应用到仓库。</summary>
    public async Task<Result<Binding>> BindAsync(Guid projectId, Guid accountId, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, ct);
        if (account is null || !account.Enabled)
        {
            return Result<Binding>.Failure(new DomainError("BINDING_ACCOUNT_INVALID", accountId.ToString()));
        }

        var project = await _projectRepository.GetByIdAsync(projectId, ct);
        if (project is null)
        {
            return Result<Binding>.Failure(new DomainError("PROJECT_NOT_FOUND", projectId.ToString()));
        }

        // 仓库存在时应用配置，缺失时仅记录绑定。
        var repoExists = !string.IsNullOrWhiteSpace(project.RepositoryPath)
            && Directory.Exists(project.RepositoryPath);

        if (repoExists)
        {
            try
            {
                await EnsureSnapshotAsync(project, ct);
                // 常规模式下应立即应用用户刚选择的新账号。全局模式才优先使用全局账号，
                // 避免改绑后仓库仍保留旧绑定账号的本地 Git 身份。
                var effectiveAccount = _effectiveAccountResolver is not null
                    && _effectiveAccountResolver.ResolveReason(project) is EffectiveReason.GlobalMode
                    ? _effectiveAccountResolver.Resolve(project) ?? account
                    : account;
                await ApplyAccountToRepositoryAsync(project, effectiveAccount, ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Git 配置未真正写入时不能仅更新绑定记录，否则界面会显示新账号但仓库仍使用旧身份。
                return Result<Binding>.Failure(new DomainError("BINDING_APPLY_FAILED", project.RepositoryPath));
            }
        }

        var existing = await _repository.GetByProjectIdAsync(projectId, ct);
        if (existing is not null)
        {
            existing.AccountId = accountId;
            existing.Status = BindingStatus.Active;
            existing.AppliedAt = DateTimeOffset.UtcNow;
            await _repository.UpdateAsync(existing, ct);
            return Result<Binding>.Success(existing);
        }

        var binding = new Binding
        {
            ProjectId = projectId,
            AccountId = accountId,
            Status = BindingStatus.Active,
        };

        await _repository.AddAsync(binding, ct);
        return Result<Binding>.Success(binding);
    }

    /// <summary>解除绑定：恢复仓库原始配置，删除绑定与快照。</summary>
    public async Task<Result> UnbindAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, ct);
        if (project is not null)
        {
            var snapshot = await _snapshotRepository.GetByProjectIdAsync(projectId, ct);
            if (snapshot is not null && !string.IsNullOrWhiteSpace(project.RepositoryPath)
                && Directory.Exists(project.RepositoryPath))
            {
                await _gitConfigApplier.RestoreAsync(
                    project.RepositoryPath,
                    snapshot.UserName,
                    snapshot.UserEmail,
                    snapshot.SshCommand,
                    snapshot.CredentialHelper,
                    ct);
            }
        }

        await _snapshotRepository.DeleteByProjectIdAsync(projectId, ct);
        await _repository.DeleteByProjectIdAsync(projectId, ct);
        return Result.Success();
    }

    /// <summary>校验绑定状态（私钥/凭据是否存在等）。</summary>
    public async Task<BindingStatus> ValidateAsync(Binding binding, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByIdAsync(binding.AccountId, ct);
        if (account is null || !account.Enabled)
        {
            return BindingStatus.AccountDisabled;
        }

        var project = await _projectRepository.GetByIdAsync(binding.ProjectId, ct);
        if (project is null || !System.IO.Directory.Exists(project.RepositoryPath))
        {
            return BindingStatus.RepositoryMissing;
        }

        // SSH Key 缺失检查。
        if (account.AuthenticationType is AuthenticationType.Ssh or AuthenticationType.Both
            && !string.IsNullOrWhiteSpace(account.SshPrivateKeyPath)
            && !System.IO.File.Exists(account.SshPrivateKeyPath))
        {
            return BindingStatus.SshKeyMissing;
        }

        // HTTPS Credential 缺失检查。
        if (account.AuthenticationType is AuthenticationType.Https or AuthenticationType.Both
            && !string.IsNullOrWhiteSpace(account.HttpsSecretId))
        {
            var secret = await _secretStore.GetAsync(account.HttpsSecretId, ct);
            if (string.IsNullOrEmpty(secret))
            {
                return BindingStatus.CredentialMissing;
            }
        }

        return BindingStatus.Active;
    }

    /// <summary>校验绑定。供 AccountService 删除守卫使用。</summary>
    public Task<int> CountByAccountIdAsync(Guid accountId, CancellationToken ct = default)
        => _repository.CountByAccountIdAsync(accountId, ct);

    /// <summary>执行绑定连接测试（只读认证测试）。</summary>
    public async Task<TestRemoteResult> TestAsync(
        Binding binding,
        CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByIdAsync(binding.AccountId, ct);
        var project = await _projectRepository.GetByIdAsync(binding.ProjectId, ct);
        if (account is null || project is null)
        {
            return new TestRemoteResult { Success = false, Output = "BINDING_INCOMPLETE" };
        }

        var effectiveAccount = _effectiveAccountResolver?.Resolve(project) ?? account;
        try
        {
            // 旧绑定可能尚未写入“禁止凭据回退”的配置；测试前按当前实际生效账号刷新，
            // 使 SSH/HTTPS 测试也不会意外使用系统已有账号。
            await ApplyAccountToRepositoryAsync(project, effectiveAccount, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return new TestRemoteResult { Success = false, Output = "BINDING_APPLY_FAILED" };
        }

        var sshCommand = BuildSshCommand(project, effectiveAccount);
        return await _gitService.TestRemoteAsync(project.RepositoryPath, sshCommand, ct);
    }

    /// <summary>开启或切换全局模式时，以同一账号重写所有已登记仓库的本地 Git 身份。</summary>
    public Task<Result> ApplyGlobalAccountAsync(Account account, CancellationToken ct = default)
        => ApplyToBoundRepositoriesAsync(_ => Task.FromResult<Account?>(account), ct);

    /// <summary>关闭全局模式时，按各仓库原有绑定重新应用身份。</summary>
    public Task<Result> RestoreBoundAccountsAsync(CancellationToken ct = default)
        => ApplyToBoundRepositoriesAsync(
            binding => _accountRepository.GetByIdAsync(binding.AccountId, ct),
            ct);

    /// <summary>确保仓库已有配置快照（保留最初的原始配置，供解绑时回滚）。</summary>
    private async Task EnsureSnapshotAsync(Project project, CancellationToken ct)
    {
        var existing = await _snapshotRepository.GetByProjectIdAsync(project.Id, ct);
        if (existing is not null)
        {
            return;
        }

        var (name, email) = await _gitConfigApplier.ReadIdentityAsync(project.RepositoryPath, ct);
        var sshCommand = await _gitConfigApplier.ReadSshCommandAsync(project.RepositoryPath, ct);
        var credentialHelper = await _gitConfigApplier.ReadCredentialHelperAsync(project.RepositoryPath, ct);

        var snapshot = new RepositorySnapshot
        {
            ProjectId = project.Id,
            UserName = name,
            UserEmail = email,
            SshCommand = sshCommand,
            CredentialHelper = credentialHelper,
            CapturedAt = DateTimeOffset.UtcNow,
        };

        await _snapshotRepository.SaveAsync(snapshot, ct);
    }

    /// <summary>把账号身份及所选认证方式应用到目标仓库，禁止回退到系统已有凭据。</summary>
    private async Task ApplyAccountToRepositoryAsync(Project project, Account account, CancellationToken ct)
    {
        var protocol = GetRemoteProtocol(project);
        if (protocol is RemoteProtocol.Https or RemoteProtocol.Http)
        {
            Guid? credentialAccountId = account.AuthenticationType is AuthenticationType.Https or AuthenticationType.Both
                && !string.IsNullOrWhiteSpace(account.HttpsSecretId)
                ? account.Id
                : null;
            await _gitConfigApplier.ApplyCredentialHelperAsync(project.RepositoryPath, credentialAccountId, ct);
        }

        await _gitConfigApplier.ApplySshCommandAsync(project.RepositoryPath, BuildSshCommand(project, account), ct);
        await _gitConfigApplier.ApplyIdentityAsync(project.RepositoryPath, account.GitName, account.GitEmail, ct);
    }

    private async Task<Result> ApplyToBoundRepositoriesAsync(
        Func<Binding, Task<Account?>> accountResolver,
        CancellationToken ct)
    {
        var bindings = await _repository.GetAllAsync(ct);
        foreach (var binding in bindings)
        {
            var project = await _projectRepository.GetByIdAsync(binding.ProjectId, ct);
            if (project is null || string.IsNullOrWhiteSpace(project.RepositoryPath)
                || !Directory.Exists(project.RepositoryPath))
            {
                continue;
            }

            var account = await accountResolver(binding);
            if (account is null || !account.Enabled)
            {
                return Result.Failure(new DomainError("GLOBALMODE_ACCOUNT_INVALID"));
            }

            try
            {
                await ApplyAccountToRepositoryAsync(project, account, ct);
            }
            catch (Exception)
            {
                return Result.Failure(new DomainError("GLOBALMODE_APPLY_FAILED", project.RepositoryPath));
            }
        }

        return Result.Success();
    }

    private string BuildSshCommand(Project project, Account account)
    {
        if (GetRemoteProtocol(project) is not RemoteProtocol.Ssh)
        {
            return string.Empty;
        }

        if (account.AuthenticationType is AuthenticationType.Ssh or AuthenticationType.Both
            && !string.IsNullOrWhiteSpace(account.SshPrivateKeyPath))
        {
            return $"ssh -i \"{account.SshPrivateKeyPath}\" -o IdentitiesOnly=yes";
        }

        // 没有为所选账号配置 SSH 私钥时，显式关闭 Agent 与交互式认证，避免 Git
        // 回退到开发机的默认密钥或 SSH Agent 中其他平台的密钥。
        return "ssh -F NUL -i NUL -o BatchMode=yes -o IdentitiesOnly=yes -o IdentityAgent=none -o PasswordAuthentication=no -o KbdInteractiveAuthentication=no";
    }

    private RemoteProtocol GetRemoteProtocol(Project project)
    {
        return project.RemoteProtocol is not RemoteProtocol.Unknown
            ? project.RemoteProtocol
            : _gitService.ParseRemote(project.OriginUrl).Protocol;
    }
}
