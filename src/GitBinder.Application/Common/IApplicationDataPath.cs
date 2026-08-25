namespace GitBinder.Application.Common;

/// <summary>
/// 应用数据目录提供者，返回平台相关的数据目录。
/// </summary>
public interface IApplicationDataPath
{
    /// <summary>根数据目录，如 %LOCALAPPDATA%/GitBinder/。</summary>
    string DataDirectory { get; }

    /// <summary>数据库文件路径。</summary>
    string DatabasePath { get; }

    /// <summary>日志目录。</summary>
    string LogDirectory { get; }

    /// <summary>SSH Key 管理目录。</summary>
    string KeysDirectory { get; }
}