namespace GitBinder.Domain.Projects;

/// <summary>
/// Remote URL 协议。
/// </summary>
public enum RemoteProtocol
{
    /// <summary>未知或无法解析。</summary>
    Unknown,
    /// <summary>SSH（git@host:path 或 ssh://）。</summary>
    Ssh,
    /// <summary>HTTPS。</summary>
    Https,
    /// <summary>HTTP。</summary>
    Http,
    /// <summary>本地文件路径。</summary>
    File,
}