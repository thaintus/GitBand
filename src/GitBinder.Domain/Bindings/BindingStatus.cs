namespace GitBinder.Domain.Bindings;

/// <summary>
/// 绑定状态。
/// </summary>
public enum BindingStatus
{
    /// <summary>有效绑定。</summary>
    Active,
    /// <summary>仓库路径缺失。</summary>
    RepositoryMissing,
    /// <summary>SSH 私钥缺失。</summary>
    SshKeyMissing,
    /// <summary>HTTPS Credential 缺失。</summary>
    CredentialMissing,
    /// <summary>账号已被禁用。</summary>
    AccountDisabled,
    /// <summary>检测到配置漂移（仓库实际配置与绑定不一致）。</summary>
    Drifted,
    /// <summary>绑定异常。</summary>
    Error,
}
