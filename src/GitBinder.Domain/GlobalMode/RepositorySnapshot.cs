using GitBinder.Domain.Accounts;

namespace GitBinder.Domain.GlobalMode;

/// <summary>
/// 仓库配置快照，用于在修改 .git/config 前保存原始值以便回滚。
/// </summary>
public sealed class RepositorySnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    /// <summary>完整邮箱配置；null 表示旧版本快照，字段 null 与空字符串分别表示未配置与显式留空。</summary>
    public GitEmailConfigSnapshot? EmailConfig { get; set; }

    public string SshCommand { get; set; } = string.Empty;

    public string CredentialHelper { get; set; } = string.Empty;

    public bool CredentialUseHttpPath { get; set; }

    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}
