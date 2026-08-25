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

    public string SshCommand { get; set; } = string.Empty;

    public string CredentialHelper { get; set; } = string.Empty;

    public bool CredentialUseHttpPath { get; set; }

    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}