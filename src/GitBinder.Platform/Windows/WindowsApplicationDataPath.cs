using GitBinder.Application.Common;

namespace GitBinder.Platform.Windows;

/// <summary>
/// Windows 应用数据目录提供者。
/// </summary>
public sealed class WindowsApplicationDataPath : IApplicationDataPath
{
    public string DataDirectory { get; }

    public string DatabasePath { get; }

    public string LogDirectory { get; }

    public string KeysDirectory { get; }

    public WindowsApplicationDataPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        DataDirectory = Path.Combine(baseDir, "GitBinder");
        DatabasePath = Path.Combine(DataDirectory, "gitbinder.db");
        LogDirectory = Path.Combine(DataDirectory, "logs");
        KeysDirectory = Path.Combine(DataDirectory, "keys");
    }
}