namespace GitBinder.Domain.GlobalMode;

/// <summary>
/// Global Mode 状态机，避免仅用 Boolean 表达。
/// </summary>
public enum GlobalModeState
{
    /// <summary>已关闭。</summary>
    Disabled,
    /// <summary>正在开启。</summary>
    Enabling,
    /// <summary>已开启。</summary>
    Enabled,
    /// <summary>正在切换 Global Account。</summary>
    Switching,
    /// <summary>正在恢复。</summary>
    Restoring,
    /// <summary>出错。</summary>
    Error,
}