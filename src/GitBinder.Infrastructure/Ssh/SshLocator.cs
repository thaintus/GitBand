using GitBinder.Application.Security;

namespace GitBinder.Infrastructure.Ssh;

/// <summary>
/// SSH 定位器（Windows V1）。
/// </summary>
public sealed class SshLocator : ISshLocator
{
    public async Task<string?> LocateAsync(CancellationToken ct = default)
    {
        await Task.CompletedTask;

        // PATH 中的 ssh.exe。
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in paths)
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                continue;
            }

            var candidate = Path.Combine(dir, "ssh.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // 常见 OpenSSH 安装目录。
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(systemRoot))
        {
            candidates.Add(Path.Combine(systemRoot, "System32", "OpenSSH", "ssh.exe"));
        }
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
        {
            candidates.Add(Path.Combine(programFiles, "Git", "usr", "bin", "ssh.exe"));
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
