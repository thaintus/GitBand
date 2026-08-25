namespace GitBinder.Domain.Accounts;

/// <summary>
/// Git 托管平台目录实体：用户可维护平台名称与 Host。
/// </summary>
public sealed class GitPlatform
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>平台显示名称，如 GitHub、公司内网 GitLab。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>平台主机，如 github.com。</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>排序值，越小越靠前。</summary>
    public int SortOrder { get; set; }

    /// <summary>是否启用。禁用后新建账号时不展示。</summary>
    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}