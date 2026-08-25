namespace GitBinder.Domain.GlobalMode;

/// <summary>
/// Global Mode 配置，持久化于 settings。
/// </summary>
public sealed class GlobalModeConfig
{
    public bool Enabled { get; set; }

    /// <summary>指定账号（null 表示使用 Default Account）。</summary>
    public Guid? GlobalAccountId { get; set; }

    public GlobalModeState State { get; set; } = GlobalModeState.Disabled;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}