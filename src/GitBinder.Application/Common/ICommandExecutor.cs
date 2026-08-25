namespace GitBinder.Application.Common;

/// <summary>
/// 命令执行结果。
/// </summary>
public sealed class CommandResult
{
    public int ExitCode { get; init; }

    public string StdOut { get; init; } = string.Empty;

    public string StdErr { get; init; } = string.Empty;

    public bool IsSuccess => ExitCode == 0;
}

/// <summary>
/// 命令执行器，统一处理 Git / SSH 等外部命令调用，参数使用数组避免 Shell 注入。
/// </summary>
public interface ICommandExecutor
{
    Task<CommandResult> ExecuteAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default);
}