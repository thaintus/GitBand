using GitBinder.Application.Common;
using GitBinder.Application.Git;
using GitBinder.Domain.Accounts;
using GitBinder.Domain.Common;
using GitBinder.Domain.Projects;

namespace GitBinder.Infrastructure.Git;

/// <summary>使用单次命令参数和环境隔离认证，不把口令/Token写入命令行或 Git 配置。</summary>
public sealed class GitTransfer(ICommandExecutor executor, IGitLocator locator, string? helperPath = null) : IGitTransfer
{
    public async Task<Result> TestAsync(string repositoryPath, Account account, CancellationToken ct = default)
    {
        var stage = "test / prepare";
        try
        {
            ct.ThrowIfCancellationRequested();
            if (!Directory.Exists(repositoryPath)) return Fail("PROJECT_REPOSITORY_NOT_FOUND", repositoryPath);
            var git = await locator.LocateAsync(ct);
            if (string.IsNullOrWhiteSpace(git)) return Fail("TRANSFER_GIT_MISSING");
            var timeout = TimeSpan.FromSeconds(60);
            stage = "git remote get-url origin";
            // 实际远程地址决定认证协议；不使用数据库缓存，也不读取继承环境指定的其他仓库。
            var origin = await executor.ExecuteWithOptionsAsync(git,
                [.. BaseArguments(), "remote", "get-url", "origin"], repositoryPath, BaseOptions(timeout: timeout), ct);
            ct.ThrowIfCancellationRequested();
            if (!origin.IsSuccess) return GitTransferFailure.FromCommand(stage, origin, ct);
            var url = origin.StdOut.Trim();
            if (!GitTransferRemote.TryParse(url, out var protocol)) return Fail("TRANSFER_URL_INVALID");
            stage = "SSH/HTTPS / prepare";
            using var sshConfig = protocol == RemoteProtocol.Ssh ? new EmptySshConfig() : null;
            var auth = BuildAuthentication(account, protocol, url, sshConfig?.Path);
            if (!auth.IsSuccess) return Result.Failure(auth.Error!);
            var args = BaseArguments();
            args.AddRange(auth.Value!.Arguments);
            args.AddRange(["ls-remote", "--", url]);
            var options = new CommandExecutionOptions { Timeout = timeout, Environment = auth.Value.Options.Environment };
            stage = "git ls-remote";
            var result = await executor.ExecuteWithOptionsAsync(git, args, repositoryPath, options, ct);
            ct.ThrowIfCancellationRequested();
            return result.IsSuccess ? Result.Success() : GitTransferFailure.FromCommand(stage, result, ct);
        }
        catch (Exception ex) { return GitTransferFailure.FromException(stage, ex, ct); }
    }

    public async Task<Result> CloneAsync(string remoteUrl, string destination, Account account, CancellationToken ct = default)
    {
        var stage = "clone / prepare";
        try
        {
            if (!GitTransferRemote.TryParse(remoteUrl, out var protocol)) return Fail("TRANSFER_URL_INVALID");
            if (Directory.Exists(destination) || File.Exists(destination)) return Fail("CLONE_TARGET_EXISTS");
            using var sshConfig = protocol == RemoteProtocol.Ssh ? new EmptySshConfig() : null;
            var auth = BuildAuthentication(account, protocol, remoteUrl, sshConfig?.Path);
            if (!auth.IsSuccess) return Result.Failure(auth.Error!);
            var git = await locator.LocateAsync(ct);
            if (string.IsNullOrWhiteSpace(git)) return Fail("TRANSFER_GIT_MISSING");
            var args = BaseArguments();
            args.AddRange(auth.Value!.Arguments);
            args.AddRange(["clone", "--no-recurse-submodules", "--", remoteUrl, destination]);
            stage = "git clone";
            var result = await executor.ExecuteWithOptionsAsync(git, args, Path.GetDirectoryName(destination),
                auth.Value.Options, ct);
            return result.IsSuccess ? Result.Success() : GitTransferFailure.FromCommand(stage, result, ct);
        }
        catch (Exception ex) { return GitTransferFailure.FromException(stage, ex, ct); }
    }

    public async Task<Result> PullAsync(string repositoryPath, Account account, CancellationToken ct = default)
    {
        var stage = "pull / prepare";
        try
        {
            if (!Directory.Exists(repositoryPath)) return Fail("PROJECT_REPOSITORY_NOT_FOUND", repositoryPath);
            var git = await locator.LocateAsync(ct);
            if (string.IsNullOrWhiteSpace(git)) return Fail("TRANSFER_GIT_MISSING");
            // 全部检查在写入之前完成；不 stash，不 reset，不自动生成 merge commit。
            async Task<CommandResult> Read(params string[] args)
            {
                stage = "git " + args[0];
                var options = BaseOptions();
                return await executor.ExecuteWithOptionsAsync(git, [.. BaseArguments(), .. args], repositoryPath, options, ct);
            }
            var status = await Read("status", "--porcelain", "--untracked-files=normal");
            if (!status.IsSuccess) return GitTransferFailure.FromCommand(stage, status, ct);
            if (!string.IsNullOrWhiteSpace(status.StdOut)) return Fail("PULL_DIRTY");
            foreach (var marker in new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply" })
            {
                var state = await Read("rev-parse", "--git-path", marker);
                if (!state.IsSuccess) return GitTransferFailure.FromCommand(stage, state, ct);
                var statePath = Path.GetFullPath(state.StdOut.Trim(), repositoryPath);
                if (File.Exists(statePath) || Directory.Exists(statePath)) return Fail("PULL_IN_PROGRESS");
            }
            var branch = await Read("symbolic-ref", "--quiet", "--short", "HEAD");
            if (!branch.IsSuccess && branch.ExitCode != 1) return GitTransferFailure.FromCommand(stage, branch, ct);
            if (!branch.IsSuccess || string.IsNullOrWhiteSpace(branch.StdOut)) return Fail("PULL_NO_UPSTREAM");
            var name = branch.StdOut.Trim();
            var remote = await Read("config", "--get", $"branch.{name}.remote");
            if (!remote.IsSuccess && remote.ExitCode != 1) return GitTransferFailure.FromCommand(stage, remote, ct);
            var merge = await Read("config", "--get", $"branch.{name}.merge");
            if (!merge.IsSuccess && merge.ExitCode != 1) return GitTransferFailure.FromCommand(stage, merge, ct);
            if (!remote.IsSuccess || remote.StdOut.Trim() != "origin"
                || !merge.IsSuccess || !merge.StdOut.Trim().StartsWith("refs/heads/", StringComparison.Ordinal)
                || merge.StdOut.Trim().Contains('\n')) return Fail("PULL_NO_UPSTREAM");
            var origin = await Read("remote", "get-url", "origin");
            if (!origin.IsSuccess) return GitTransferFailure.FromCommand(stage, origin, ct);
            var url = origin.StdOut.Trim();
            if (!origin.IsSuccess || !GitTransferRemote.TryParse(url, out var protocol)) return Fail("TRANSFER_URL_INVALID");
            stage = "SSH/HTTPS / prepare";
            using var sshConfig = protocol == RemoteProtocol.Ssh ? new EmptySshConfig() : null;
            var auth = BuildAuthentication(account, protocol, url, sshConfig?.Path);
            if (!auth.IsSuccess) return Result.Failure(auth.Error!);
            var args = BaseArguments();
            args.AddRange(auth.Value!.Arguments);
            args.AddRange(["pull", "--ff-only", "--no-rebase", "--no-recurse-submodules", "--", "origin", merge.StdOut.Trim()]);
            stage = "git pull --ff-only";
            var result = await executor.ExecuteWithOptionsAsync(git, args, repositoryPath, auth.Value.Options, ct);
            return result.IsSuccess ? Result.Success() : GitTransferFailure.FromCommand(stage, result, ct);
        }
        catch (Exception ex) { return GitTransferFailure.FromException(stage, ex, ct); }
    }

    internal static List<string> BaseArguments() =>
    [
        "-c", "core.hooksPath=NUL", "-c", "core.fsmonitor=false", "-c", "submodule.recurse=false",
        "-c", "merge.autoStash=false", "-c", "rebase.autoStash=false",
        "-c", "protocol.allow=never", "-c", "protocol.https.allow=always", "-c", "protocol.ssh.allow=always",
        "-c", "core.askPass=", "-c", "credential.interactive=false", "-c", "http.followRedirects=false",
    ];

    internal static CommandExecutionOptions BaseOptions(string sshCommand = "", TimeSpan? timeout = null) => new()
    {
        Timeout = timeout ?? TimeSpan.FromMinutes(30),
        Environment = new Dictionary<string, string?>
        {
            ["GIT_TERMINAL_PROMPT"] = "0", ["GCM_INTERACTIVE"] = "Never",
            ["GIT_ASKPASS"] = "", ["SSH_ASKPASS"] = "", ["SSH_ASKPASS_REQUIRE"] = "never",
            ["GIT_SSH_COMMAND"] = sshCommand, ["GIT_SSH_VARIANT"] = "ssh",
            ["GIT_CONFIG_GLOBAL"] = "NUL", ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_CONFIG_COUNT"] = "0", ["GIT_CONFIG_PARAMETERS"] = "",
            ["GIT_DIR"] = null, ["GIT_WORK_TREE"] = null, ["GIT_INDEX_FILE"] = null,
            ["GIT_COMMON_DIR"] = null, ["GIT_OBJECT_DIRECTORY"] = null, ["GIT_ALTERNATE_OBJECT_DIRECTORIES"] = null,
        },
    };

    private sealed record Authentication(List<string> Arguments, CommandExecutionOptions Options);

    private Result<Authentication> BuildAuthentication(Account account, RemoteProtocol protocol, string url, string? sshConfigPath)
    {
        if (!account.Enabled) return Result<Authentication>.Failure(new DomainError("TRANSFER_ACCOUNT_INVALID"));
        var args = new List<string> { "-c", "credential.helper=", "-c", "http.extraHeader=" };
        if (protocol == RemoteProtocol.Https)
        {
            if (account.AuthenticationType is not (AuthenticationType.Https or AuthenticationType.Both)
                || string.IsNullOrWhiteSpace(account.HttpsSecretId))
                return Result<Authentication>.Failure(new DomainError("TRANSFER_HTTPS_REQUIRED"));
            var helper = helperPath ?? Path.Combine(AppContext.BaseDirectory, "gitbinder-credential.exe");
            if (!File.Exists(helper)) return Result<Authentication>.Failure(new DomainError("TRANSFER_HELPER_MISSING"));
            // 精确 URL 作用域重置，防止仓库内更具体的 helper/extraHeader 覆盖选定账号。
            args.AddRange(["-c", $"credential.{url}.helper=", "-c",
                $"credential.{url}.helper=!{Quote(helper.Replace('\\', '/'))} --account-id {account.Id:D}",
                "-c", $"http.{url}.extraHeader="]);
            return Result<Authentication>.Success(new(args, BaseOptions()));
        }
        if (account.AuthenticationType is not (AuthenticationType.Ssh or AuthenticationType.Both)
            || string.IsNullOrWhiteSpace(account.SshPrivateKeyPath) || !File.Exists(account.SshPrivateKeyPath))
            return Result<Authentication>.Failure(new DomainError("TRANSFER_SSH_REQUIRED"));
        // IdentitiesOnly 仅允许此私钥；已解锁的同一密钥可由 Agent 签名，不尝试其他账号密钥。
        var keyPath = Path.GetFullPath(account.SshPrivateKeyPath).Replace('\\', '/');
        var configPath = Path.GetFullPath(sshConfigPath!).Replace('\\', '/');
        var ssh = $"ssh -F {Quote(configPath)} -i {Quote(keyPath)} -o IdentitiesOnly=yes"
            + " -o BatchMode=yes -o PasswordAuthentication=no -o KbdInteractiveAuthentication=no -o StrictHostKeyChecking=yes";
        args.AddRange(["-c", $"core.sshCommand={ssh}"]);
        return Result<Authentication>.Success(new(args, BaseOptions(ssh)));
    }

    // Git 的 helper/sshCommand 自身由 Git shell 解析；路径使用单引号转义，不拼接用户口令。
    private static string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";
    private static Result Fail(string code, params string[] args) => Result.Failure(new DomainError(code, args));
}
