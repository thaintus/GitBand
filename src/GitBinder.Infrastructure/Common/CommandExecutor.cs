using System.Diagnostics;
using System.Text;
using GitBinder.Application.Common;

namespace GitBinder.Infrastructure.Common;

/// <summary>
/// 命令执行器实现，使用参数数组避免 Shell 注入，统一处理 stdout/stderr/exit code/超时。
/// </summary>
public sealed class CommandExecutor : ICommandExecutor
{
    private readonly TimeSpan _defaultTimeout;

    public CommandExecutor(TimeSpan? defaultTimeout = null)
    {
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(60);
    }

    public Task<CommandResult> ExecuteAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
        => ExecuteCoreAsync(executable, arguments, workingDirectory, null, cancellationToken);

    public Task<CommandResult> ExecuteWithOptionsAsync(string executable, IReadOnlyList<string> arguments,
        string? workingDirectory, CommandExecutionOptions options, CancellationToken cancellationToken = default)
        => ExecuteCoreAsync(executable, arguments, workingDirectory, options, cancellationToken);

    private async Task<CommandResult> ExecuteCoreAsync(string executable, IReadOnlyList<string> arguments,
        string? workingDirectory, CommandExecutionOptions? options, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }
        if (options is not null)
        {
            foreach (var pair in options.Environment)
            {
                if (pair.Value is null) startInfo.Environment.Remove(pair.Key);
                else startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        try
        {
            if (!process.Start())
            {
                return new CommandResult
                {
                    ExitCode = -1,
                    StdErr = "Failed to start process.",
                };
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.StandardInput.Close();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(options?.Timeout ?? _defaultTimeout);

            await process.WaitForExitAsync(timeoutCts.Token);

            return new CommandResult
            {
                ExitCode = process.ExitCode,
                StdOut = stdout.ToString(),
                StdErr = stderr.ToString(),
            };
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return new CommandResult
            {
                ExitCode = -1,
                StdErr = "Command timed out.",
            };
        }
        catch (Exception ex)
        {
            return new CommandResult
            {
                ExitCode = -1,
                StdErr = ex.Message,
            };
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 忽略进程已退出等情况。
        }
    }
}
