using GitBinder.Domain.Accounts;

namespace GitBinder.Domain.Accounts;

/// <summary>
/// Git 账号聚合。同时描述 Commit Identity 与 Authentication Identity。
/// </summary>
public sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>账号别名，未填写时等于 Username。</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>所属平台目录 Id（可空，兼容旧数据）。</summary>
    public Guid? PlatformId { get; set; }

    /// <summary>平台显示名称（冗余存储，便于展示）。</summary>
    public string PlatformName { get; set; } = string.Empty;

    /// <summary>托管平台主机，例如 github.com。</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>平台登录用户名。</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Commit Identity: user.name。</summary>
    public string GitName { get; set; } = string.Empty;

    /// <summary>Commit Identity: user.email。</summary>
    public string GitEmail { get; set; } = string.Empty;

    public AuthenticationType AuthenticationType { get; set; } = AuthenticationType.None;

    /// <summary>SSH 私钥文件路径（仅记录路径）。</summary>
    public string SshPrivateKeyPath { get; set; } = string.Empty;

    /// <summary>SSH 公钥文件路径（可选）。</summary>
    public string SshPublicKeyPath { get; set; } = string.Empty;

    /// <summary>SSH 私钥 passphrase 对应的 SecretId。</summary>
    public string PassphraseSecretId { get; set; } = string.Empty;

    /// <summary>HTTPS 用户名。</summary>
    public string HttpsUsername { get; set; } = string.Empty;

    /// <summary>HTTPS Credential（Password/Token）对应的 SecretId。</summary>
    public string HttpsSecretId { get; set; } = string.Empty;

    /// <summary>是否为默认账号。</summary>
    public bool IsDefault { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>展示用别名：Alias 为空时回退到 Username。</summary>
    public string DisplayAlias => string.IsNullOrWhiteSpace(Alias) ? Username : Alias;

    /// <summary>是否为有效账号。</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(GitName) || !string.IsNullOrWhiteSpace(GitEmail);
}