using GitBinder.Domain.Accounts;

namespace GitBinder.Application.Accounts;

/// <summary>
/// 创建账号的输入 DTO。
/// </summary>
public class CreateAccountInput
{
    public string Username { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;

    public Guid? PlatformId { get; set; }

    public string PlatformName { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public string GitName { get; set; } = string.Empty;

    public string GitEmail { get; set; } = string.Empty;

    public AuthenticationType AuthenticationType { get; set; } = AuthenticationType.None;

    public string SshPrivateKeyPath { get; set; } = string.Empty;

    public string SshPublicKeyPath { get; set; } = string.Empty;

    public string HttpsUsername { get; set; } = string.Empty;

    /// <summary>HTTPS 凭据明文（Password/Token），仅用于录入后加密保存，不落库。留空表示不修改。</summary>
    public string? HttpsCredential { get; set; }

    /// <summary>SSH 私钥 passphrase 明文，仅加密保存，不落库。留空表示不修改。</summary>
    public string? SshPassphrase { get; set; }
}

/// <summary>
/// 更新账号的输入 DTO。
/// </summary>
public sealed class UpdateAccountInput : CreateAccountInput
{
    public Guid Id { get; set; }
}