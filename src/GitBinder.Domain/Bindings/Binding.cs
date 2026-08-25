using GitBinder.Domain.Accounts;
using GitBinder.Domain.Bindings;

namespace GitBinder.Domain.Bindings;

/// <summary>
/// 绑定实体：Project → Account 一对一。
/// 数据库唯一约束 UNIQUE(project_id)。
/// </summary>
public sealed class Binding
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid AccountId { get; set; }

    public BindingStatus Status { get; set; } = BindingStatus.Active;

    /// <summary>绑定首次应用时间。</summary>
    public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>最近一次验证时间。</summary>
    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>最近一次测试结果文本。</summary>
    public string LastTestResult { get; set; } = string.Empty;
}