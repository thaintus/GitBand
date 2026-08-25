using GitBinder.Domain.Projects;
using GitBinder.Domain.Common;

namespace GitBinder.Application.Git;

/// <summary>
/// Git 仓库定位器。
/// </summary>
public interface IGitLocator
{
    Task<string?> LocateAsync(CancellationToken ct = default);
}

/// <summary>
/// Git 服务，封装所有 Git CLI 行为。
/// </summary>
public interface IGitService
{
    /// <summary>校验目录是否为有效 Git 仓库。</summary>
    Task<bool> ValidateRepositoryAsync(string path, CancellationToken ct = default);

    /// <summary>获取仓库根路径。</summary>
    Task<string?> GetRepositoryRootAsync(string path, CancellationToken ct = default);

    /// <summary>获取 .git 目录真实路径。</summary>
    Task<string?> GetGitDirAsync(string path, CancellationToken ct = default);

    /// <summary>读取 origin URL。</summary>
    Task<string?> GetOriginUrlAsync(string path, CancellationToken ct = default);

    /// <summary>新增或更新仓库的 origin URL。</summary>
    Task<Result> SetOriginUrlAsync(string path, string originUrl, CancellationToken ct = default);

    /// <summary>获取当前分支。</summary>
    Task<string?> GetCurrentBranchAsync(string path, CancellationToken ct = default);

    /// <summary>解析 Remote URL 为协议与主机。</summary>
    (RemoteProtocol Protocol, string Host) ParseRemote(string url);

    /// <summary>执行连接测试（ls-remote）。</summary>
    Task<TestRemoteResult> TestRemoteAsync(
        string repositoryPath,
        string? sshCommand,
        CancellationToken ct = default);

    /// <summary>获取 Git 版本。</summary>
    Task<string?> GetGitVersionAsync(CancellationToken ct = default);
}

/// <summary>Remote 连接测试结果。</summary>
public sealed class TestRemoteResult
{
    public bool Success { get; init; }

    public int ExitCode { get; init; }

    public string Output { get; init; } = string.Empty;

    public TimeSpan Duration { get; init; }
}
