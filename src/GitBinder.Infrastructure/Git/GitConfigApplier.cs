using GitBinder.Application.Common;
using GitBinder.Application.Git;
using GitBinder.Application.GlobalMode;
using GitBinder.Domain.Accounts;
using GitBinder.Domain.Common;
using GitBinder.Infrastructure.Common;

namespace GitBinder.Infrastructure.Git;

/// <summary>
/// Git Config 应用器实现。
/// </summary>
public sealed class GitConfigApplier : IGitConfigApplier, IGlobalGitConfigApplier
{
    private readonly ICommandExecutor _executor;
    private readonly IGitLocator? _gitLocator;

    public GitConfigApplier(ICommandExecutor executor, IGitLocator? gitLocator = null)
    {
        _executor = executor;
        _gitLocator = gitLocator;
    }

    public async Task<(string Name, string Email)> ReadIdentityAsync(string repositoryPath, CancellationToken ct = default)
    {
        var name = await ReadConfigAsync(repositoryPath, "user.name", ct);
        var email = await ReadConfigAsync(repositoryPath, "user.email", ct);
        return (name, email);
    }

    public async Task<string> ReadSshCommandAsync(string repositoryPath, CancellationToken ct = default)
        => await ReadConfigAsync(repositoryPath, "core.sshCommand", ct);

    public async Task<string> ReadCredentialHelperAsync(string repositoryPath, CancellationToken ct = default)
        => await ReadConfigAsync(repositoryPath, "credential.helper", ct);

    public async Task ApplyIdentityAsync(
        string repositoryPath,
        string gitName,
        string gitEmail,
        CancellationToken ct = default)
    {
        // 账号切换必须覆盖完整身份。可选字段为空时需移除旧账号遗留的本地配置，
        // 否则新账号会继续使用旧 user.email。
        await WriteLocalConfigAsync(repositoryPath, "user.name", gitName, ct);
        await WriteLocalConfigAsync(repositoryPath, "user.email", gitEmail, ct);
    }

    public async Task ApplySshCommandAsync(
        string repositoryPath,
        string sshCommand,
        CancellationToken ct = default)
    {
        await WriteLocalConfigAsync(repositoryPath, "core.sshCommand", sshCommand, ct);
    }

    public async Task ApplyCredentialHelperAsync(
        string repositoryPath,
        Guid? accountId,
        CancellationToken ct = default)
    {
        // 先确认 Helper 可用，再重置原有链；若随附程序缺失，不能留下一个“看似已改绑”
        // 但仍可能回退到系统凭据的仓库配置。
        var helperCommand = accountId is Guid id ? BuildCredentialHelperCommand(id) : null;
        await WriteLocalConfigValueAsync(repositoryPath, "credential.helper", string.Empty, ct);
        if (!string.IsNullOrWhiteSpace(helperCommand))
        {
            await AddLocalConfigAsync(repositoryPath, "credential.helper", helperCommand, ct);
        }
    }

    public async Task RestoreAsync(
        string repositoryPath,
        string? name,
        string? email,
        string? sshCommand,
        string? credentialHelper,
        CancellationToken ct = default)
    {
        await RestoreKeyAsync(repositoryPath, "user.name", name, ct);
        await RestoreKeyAsync(repositoryPath, "user.email", email, ct);
        await RestoreKeyAsync(repositoryPath, "core.sshCommand", sshCommand, ct);
        await RestoreKeyAsync(repositoryPath, "credential.helper", credentialHelper, ct);
    }

    public async Task<GlobalGitConfigSnapshot> CaptureAsync(CancellationToken ct = default)
    {
        var name = await ReadGlobalConfigAsync("user.name", ct);
        var email = await ReadGlobalConfigAsync("user.email", ct);
        var sshCommand = await ReadGlobalConfigAsync("core.sshCommand", ct);
        var credentialHelpers = await ReadGlobalConfigValuesAsync("credential.helper", ct);
        return new GlobalGitConfigSnapshot(name, email, sshCommand, credentialHelpers);
    }

    public async Task<Result> ApplyAsync(Account account, CancellationToken ct = default)
    {
        var name = await WriteGlobalConfigAsync("user.name", account.GitName, ct);
        if (!name.IsSuccess)
        {
            return name;
        }

        var email = await WriteGlobalConfigAsync("user.email", account.GitEmail, ct);
        if (!email.IsSuccess)
        {
            return email;
        }

        var sshCommand = account.AuthenticationType is AuthenticationType.Ssh or AuthenticationType.Both
            && !string.IsNullOrWhiteSpace(account.SshPrivateKeyPath)
                ? $"ssh -i \"{account.SshPrivateKeyPath}\" -o IdentitiesOnly=yes"
                : NoFallbackSshCommand;
        var ssh = await WriteGlobalConfigAsync("core.sshCommand", sshCommand, ct);
        if (!ssh.IsSuccess)
        {
            return ssh;
        }

        Guid? credentialAccountId = account.AuthenticationType is AuthenticationType.Https or AuthenticationType.Both
            && !string.IsNullOrWhiteSpace(account.HttpsSecretId)
            ? account.Id
            : null;
        return await ApplyGlobalCredentialHelperAsync(credentialAccountId, ct);
    }

    public async Task<Result> RestoreAsync(GlobalGitConfigSnapshot snapshot, CancellationToken ct = default)
    {
        var name = await WriteGlobalConfigAsync("user.name", snapshot.UserName, ct);
        if (!name.IsSuccess)
        {
            return name;
        }

        var email = await WriteGlobalConfigAsync("user.email", snapshot.UserEmail, ct);
        if (!email.IsSuccess)
        {
            return email;
        }

        var ssh = await WriteGlobalConfigAsync("core.sshCommand", snapshot.SshCommand, ct);
        if (!ssh.IsSuccess)
        {
            return ssh;
        }

        return await RestoreGlobalCredentialHelpersAsync(snapshot.CredentialHelpers, ct);
    }

    private async Task<string> ReadConfigAsync(string repositoryPath, string key, CancellationToken ct)
    {
        var git = await ResolveGitAsync(ct);
        var result = await _executor.ExecuteAsync(git, ["config", "--local", "--get", key], repositoryPath, ct);
        return result.IsSuccess ? result.StdOut.Trim() : string.Empty;
    }

    private async Task<string?> ReadGlobalConfigAsync(string key, CancellationToken ct)
    {
        var git = await ResolveGitAsync(ct);
        var result = await _executor.ExecuteAsync(git, ["config", "--global", "--get", key], null, ct);
        return result.IsSuccess ? result.StdOut.Trim() : null;
    }

    private async Task<Result> WriteGlobalConfigAsync(string key, string? value, CancellationToken ct)
    {
        var git = await ResolveGitAsync(ct);
        var args = string.IsNullOrWhiteSpace(value)
            ? new[] { "config", "--global", "--unset-all", key }
            : new[] { "config", "--global", key, value };
        var result = await _executor.ExecuteAsync(git, args, null, ct);

        // 关闭或恢复不存在的配置项时，git 会返回非零；此时目标状态已满足。
        if (result.IsSuccess || (string.IsNullOrWhiteSpace(value) && result.ExitCode == 5))
        {
            return Result.Success();
        }

        return Result.Failure(new DomainError("GLOBALMODE_GLOBAL_CONFIG_FAILED", key));
    }

    private async Task<Result> ApplyGlobalCredentialHelperAsync(Guid? accountId, CancellationToken ct)
    {
        string? helperCommand;
        try
        {
            helperCommand = accountId is Guid id ? BuildCredentialHelperCommand(id) : null;
        }
        catch (Exception)
        {
            return Result.Failure(new DomainError("GLOBALMODE_GLOBAL_CONFIG_FAILED", "credential.helper"));
        }

        var reset = await WriteGlobalConfigValueAsync("credential.helper", string.Empty, ct);
        if (!reset.IsSuccess || string.IsNullOrWhiteSpace(helperCommand))
        {
            return reset;
        }

        return await AddGlobalConfigAsync("credential.helper", helperCommand, ct);
    }

    private async Task<Result> RestoreGlobalCredentialHelpersAsync(
        IReadOnlyList<string>? credentialHelpers,
        CancellationToken ct)
    {
        if (credentialHelpers is null || credentialHelpers.Count == 0)
        {
            return await WriteGlobalConfigAsync("credential.helper", null, ct);
        }

        var first = await WriteGlobalConfigValueAsync("credential.helper", credentialHelpers[0], ct);
        if (!first.IsSuccess)
        {
            return first;
        }

        foreach (var helper in credentialHelpers.Skip(1))
        {
            var added = await AddGlobalConfigAsync("credential.helper", helper, ct);
            if (!added.IsSuccess)
            {
                return added;
            }
        }

        return Result.Success();
    }

    private async Task<Result> WriteGlobalConfigValueAsync(string key, string value, CancellationToken ct)
    {
        var git = await ResolveGitAsync(ct);
        var result = await _executor.ExecuteAsync(git, ["config", "--global", "--replace-all", key, value], null, ct);
        return result.IsSuccess
            ? Result.Success()
            : Result.Failure(new DomainError("GLOBALMODE_GLOBAL_CONFIG_FAILED", key));
    }

    private async Task<Result> AddGlobalConfigAsync(string key, string value, CancellationToken ct)
    {
        var git = await ResolveGitAsync(ct);
        var result = await _executor.ExecuteAsync(git, ["config", "--global", "--add", key, value], null, ct);
        return result.IsSuccess
            ? Result.Success()
            : Result.Failure(new DomainError("GLOBALMODE_GLOBAL_CONFIG_FAILED", key));
    }

    private async Task<string[]> ReadGlobalConfigValuesAsync(string key, CancellationToken ct)
    {
        var git = await ResolveGitAsync(ct);
        var result = await _executor.ExecuteAsync(git, ["config", "--global", "--get-all", key], null, ct);
        if (!result.IsSuccess)
        {
            return [];
        }

        var output = result.StdOut.Replace("\r\n", "\n").TrimEnd('\n');
        return output.Split('\n', StringSplitOptions.None);
    }

    private async Task RestoreKeyAsync(
        string repositoryPath,
        string key,
        string? value,
        CancellationToken ct)
    {
        await WriteLocalConfigAsync(repositoryPath, key, value, ct);
    }

    /// <summary>写入或清除仓库级配置；失败必须上抛，避免界面只更新绑定记录却未真正生效。</summary>
    private async Task WriteLocalConfigAsync(
        string repositoryPath,
        string key,
        string? value,
        CancellationToken ct)
    {
        var git = await ResolveGitAsync(ct);
        var args = string.IsNullOrWhiteSpace(value)
            ? new[] { "config", "--local", "--unset-all", key }
            : new[] { "config", "--local", "--replace-all", key, value };
        var result = await _executor.ExecuteAsync(git, args, repositoryPath, ct);
        if (result.IsSuccess || (string.IsNullOrWhiteSpace(value) && result.ExitCode == 5))
        {
            return;
        }

        throw new InvalidOperationException($"Failed to write local Git config '{key}' (exit code {result.ExitCode}).");
    }

    private async Task WriteLocalConfigValueAsync(
        string repositoryPath,
        string key,
        string value,
        CancellationToken ct)
    {
        var git = await ResolveGitAsync(ct);
        var result = await _executor.ExecuteAsync(
            git,
            ["config", "--local", "--replace-all", key, value],
            repositoryPath,
            ct);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to write local Git config '{key}' (exit code {result.ExitCode}).");
        }
    }

    private async Task AddLocalConfigAsync(
        string repositoryPath,
        string key,
        string value,
        CancellationToken ct)
    {
        var git = await ResolveGitAsync(ct);
        var result = await _executor.ExecuteAsync(
            git,
            ["config", "--local", "--add", key, value],
            repositoryPath,
            ct);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to write local Git config '{key}' (exit code {result.ExitCode}).");
        }
    }

    private static string BuildCredentialHelperCommand(Guid accountId)
    {
        var helperPath = Path.Combine(AppContext.BaseDirectory, "gitbinder-credential.exe");
        if (!File.Exists(helperPath))
        {
            throw new FileNotFoundException("GitBinder Credential Helper is unavailable.", helperPath);
        }

        var shellPath = Path.GetFullPath(helperPath).Replace('\\', '/');
        return $"!\"{shellPath}\" --account-id {accountId:D}";
    }

    private const string NoFallbackSshCommand =
        "ssh -F NUL -i NUL -o BatchMode=yes -o IdentitiesOnly=yes -o IdentityAgent=none -o PasswordAuthentication=no -o KbdInteractiveAuthentication=no";

    private async Task<string> ResolveGitAsync(CancellationToken ct)
    {
        if (_gitLocator is not null)
        {
            var located = await _gitLocator.LocateAsync(ct);
            if (!string.IsNullOrWhiteSpace(located))
            {
                return located;
            }
        }

        return "git";
    }
}
