using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using GitBinder.Desktop.Views;

namespace GitBinder.Desktop;

/// <summary>文件选择委托。</summary>
public delegate Task<string?> FilePickerDelegate();

/// <summary>目录选择委托。</summary>
public delegate Task<string?> DirectoryPickerDelegate();

/// <summary>
/// Avalonia 对话框辅助类：提供文件/目录选择。
/// </summary>
public static class DialogHelper
{
    private static global::Avalonia.Controls.Window? GetTopLevel()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }

    /// <summary>选择单个文件，返回路径或 null。</summary>
    public static async Task<string?> PickFileAsync()
    {
        var window = GetTopLevel();
        if (window is null)
        {
            return null;
        }

        var storage = window.StorageProvider;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 SSH 私钥文件",
            AllowMultiple = false,
        });

        if (files.Count == 0)
        {
            return null;
        }

        return files[0].TryGetLocalPath();
    }

    /// <summary>选择目录，返回路径或 null。</summary>
    public static async Task<string?> PickDirectoryAsync()
    {
        var window = GetTopLevel();
        if (window is null)
        {
            return null;
        }

        var storage = window.StorageProvider;
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择目录",
            AllowMultiple = false,
        });

        if (folders.Count == 0)
        {
            return null;
        }

        return folders[0].TryGetLocalPath();
    }

    /// <summary>显示模态确认窗口；找不到主窗口时按取消处理。</summary>
    public static async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var owner = GetTopLevel();
        if (owner is null)
        {
            return false;
        }

        var dialog = new ConfirmationDialog(title, message, confirmText);
        return await dialog.ShowDialog<bool?>(owner) is true;
    }
}
