namespace GitBinder.Domain.Accounts;

/// <summary>
/// 账号认证方式。
/// </summary>
public enum AuthenticationType
{
    /// <summary>仅参与 Commit Identity，不提供远程认证。</summary>
    None,
    /// <summary>SSH 私钥认证。</summary>
    Ssh,
    /// <summary>HTTPS Username + Credential 认证。</summary>
    Https,
    /// <summary>同时支持 SSH 与 HTTPS，运行时按 Remote URL 自动选择。</summary>
    Both,
}