namespace GitBinder.Domain.GlobalMode;

/// <summary>
/// 邮箱配置快照。null 表示配置项不存在；空字符串表示明确使用空邮箱。
/// </summary>
public sealed record GitEmailConfigSnapshot(
    string? UserEmail,
    string? AuthorEmail,
    string? CommitterEmail);
