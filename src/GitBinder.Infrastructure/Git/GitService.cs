using GitBinder.Application.Common;
using GitBinder.Application.Git;
using GitBinder.Application.Settings;
using GitBinder.Domain.Common;
using GitBinder.Domain.Projects;
using GitBinder.Infrastructure.Common;

namespace GitBinder.Infrastructure.Git;

/// <summary>
/// Git 定位器，仅支持 Windows（V1）。
/// </summary>
public sealed class GitLocator : IGitLocator
{
    private readonly ISettingsRepository _settings;

    public GitLocator(ISettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<string?> LocateAsync(CancellationToken ct = default)
    {
        // 1. 用户设置。
        var configured = await _settings.GetAsync("git.executable", ct);
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        // 2. PATH。
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in paths)
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                continue;
            }

            var candidate = Path.Combine(dir, "git.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // 3. 常见安装目录。
        foreach (var common in CommonLocations())
        {
            if (File.Exists(common))
            {
                return common;
            }
        }

        return null;
    }

    private static IEnumerable<string> CommonLocations()
    {
        yield return @"C:\Program Files\Git\cmd\git.exe";
        yield return @"C:\Program Files (x86)\Git\cmd\git.exe";
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            yield return Path.Combine(localAppData, "Programs\\Git\\cmd\\git.exe");
        }
    }
}

/// <summary>
/// IGitService 实现，封装 Git CLI 行为。
/// </summary>
public sealed class GitService : IGitService
{
    private readonly ICommandExecutor _executor;
    private readonly IGitLocator _locator;

    public GitService(ICommandExecutor executor, IGitLocator locator)
    {
        _executor = executor;
        _locator = locator;
    }

    // 无论定位结果如何都回退到 PATH 中的 git，因此调用方始终可获得非空可执行文件名。
    private async Task<string> ResolveGitAsync(CancellationToken ct)
        => await _locator.LocateAsync(ct) ?? "git";

    public async Task<bool> ValidateRepositoryAsync(string path, CancellationToken ct = default)
    {
        var git = await ResolveGitAsync(ct);
        var result = await _executor.ExecuteAsync(git, ["rev-parse", "--git-dir"], path, ct);
        return result.IsSuccess;
    }

    public async Task<string?> GetRepositoryRootAsync(string path, CancellationToken ct = default)
    {
        var git = await ResolveGitAsync(ct);
        var result = await _executor.ExecuteAsync(git, ["rev-parse", "--show-toplevel"], path, ct);
        return result.IsSuccess ? result.StdOut.Trim() : null;
    }

    public async Task<string?> GetGitDirAsync(string path, CancellationToken ct = default)
    {
        var git = await ResolveGitAsync(ct);
        var result = await _executor.ExecuteAsync(git, ["rev-parse", "--git-dir"], path, ct);
        if (!result.IsSuccess)
        {
            return null;
        }

        var gitDir = result.StdOut.Trim();
        // 转换为绝对路径。
        if (!Path.IsPathRooted(gitDir))
        {
            var root = await GetRepositoryRootAsync(path, ct) ?? path;
            gitDir = Path.GetFullPath(Path.Combine(root, gitDir));
        }

        return gitDir;
    }

    public async Task<string?> GetOriginUrlAsync(string path, CancellationToken ct = default)
    {
        var git = await ResolveGitAsync(ct);
        var result = await _executor.ExecuteAsync(git, ["remote", "get-url", "origin"], path, ct);
        return result.IsSuccess ? result.StdOut.Trim() : null;
    }

    public async Task<Result> SetOriginUrlAsync(string path, string originUrl, CancellationToken ct = default)
    {
        var git = await ResolveGitAsync(ct);
        var existing = await GetOriginUrlAsync(path, ct);
        var arguments = string.IsNullOrWhiteSpace(existing)
            ? new[] { "remote", "add", "origin", originUrl }
            : new[] { "remote", "set-url", "origin", originUrl };
        var result = await _executor.ExecuteAsync(git, arguments, path, ct);
        return result.IsSuccess
            ? Result.Success()
            : Result.Failure(new DomainError("PROJECT_REMOTE_UPDATE_FAILED", path));
    }

    public async Task<string?> GetCurrentBranchAsync(string path, CancellationToken ct = default)
    {
        var git = await ResolveGitAsync(ct);
        var result = await _executor.ExecuteAsync(git, ["branch", "--show-current"], path, ct);
        return result.IsSuccess ? result.StdOut.Trim() : null;
    }

    public (RemoteProtocol Protocol, string Host) ParseRemote(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return (RemoteProtocol.Unknown, string.Empty);
        }

        var trimmed = url.Trim();

        // SCP-like: git@host:path/repo.git
        if (trimmed.StartsWith("git@", StringComparison.Ordinal))
        {
            var at = trimmed.IndexOf('@');
            var colon = trimmed.IndexOf(':', at + 1);
            if (colon > at)
            {
                var host = trimmed.Substring(at + 1, colon - at - 1);
                return (RemoteProtocol.Ssh, host);
            }
        }

        // ssh://
        if (trimmed.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
        {
            var host = ExtractHostFromUri(trimmed, "ssh://");
            return (RemoteProtocol.Ssh, host);
        }

        // https://
        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var host = ExtractHostFromUri(trimmed, "https://");
            return (RemoteProtocol.Https, host);
        }

        // http://
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            var host = ExtractHostFromUri(trimmed, "http://");
            return (RemoteProtocol.Http, host);
        }

        // 本地路径。
        if (Directory.Exists(trimmed) || Path.IsPathRooted(trimmed))
        {
            return (RemoteProtocol.File, string.Empty);
        }

        return (RemoteProtocol.Unknown, string.Empty);
    }

    public async Task<TestRemoteResult> TestRemoteAsync(
        string repositoryPath,
        string? sshCommand,
        CancellationToken ct = default)
    {
        var start = DateTimeOffset.UtcNow;
        var git = await ResolveGitAsync(ct);

        var args = new List<string>();
        if (!string.IsNullOrWhiteSpace(sshCommand))
        {
            args.Add("-c");
            args.Add($"core.sshCommand={sshCommand}");
        }

        args.Add("ls-remote");
        args.Add("origin");

        var result = await _executor.ExecuteAsync(git, args, repositoryPath, ct);
        return new TestRemoteResult
        {
            Success = result.IsSuccess,
            ExitCode = result.ExitCode,
            Output = result.IsSuccess ? result.StdOut : result.StdErr,
            Duration = DateTimeOffset.UtcNow - start,
        };
    }

    public async Task<string?> GetGitVersionAsync(CancellationToken ct = default)
    {
        var git = await ResolveGitAsync(ct);
        var result = await _executor.ExecuteAsync(git, ["--version"], null, ct);
        return result.IsSuccess ? result.StdOut.Trim() : null;
    }

    private static string ExtractHostFromUri(string uri, string scheme)
    {
        var rest = uri.Substring(scheme.Length);
        var slash = rest.IndexOf('/');
        var hostPort = slash >= 0 ? rest.Substring(0, slash) : rest;

        // 去除 userinfo 与端口。
        var at = hostPort.LastIndexOf('@');
        if (at >= 0)
        {
            hostPort = hostPort.Substring(at + 1);
        }

        var colon = hostPort.LastIndexOf(':');
        if (colon >= 0 && colon > hostPort.LastIndexOf(']'))
        {
            hostPort = hostPort.Substring(0, colon);
        }

        return hostPort;
    }
}
