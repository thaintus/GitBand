namespace GitBinder.Application.Common;

/// <summary>
/// 系统 Shell 服务，封装打开文件夹、终端等平台差异操作。
/// </summary>
public interface IShellService
{
    /// <summary>在文件管理器中打开目录。</summary>
    void OpenDirectory(string path);

    /// <summary>打开终端并定位到目录。</summary>
    void OpenTerminal(string path);

    /// <summary>打开选择目录对话框，返回所选路径或 null。</summary>
    Task<string?> PickDirectoryAsync(CancellationToken ct = default);

    /// <summary>打开选择文件对话框，返回所选路径或 null。</summary>
    Task<string?> PickFileAsync(CancellationToken ct = default);
}