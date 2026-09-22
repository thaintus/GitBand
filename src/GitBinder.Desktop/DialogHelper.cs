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
    private static readonly SemaphoreSlim NoticeQueue = new(1, 1);

    /// <summary>由操作所属窗口承载通知；串行显示，避免弹窗叠加或错误弹在账号编辑窗口后面。</summary>
    public static void Notify(string message, object source)
    {
        if (string.IsNullOrWhiteSpace(message)
            || Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = ShowNoticeAsync(message, source));
    }

    private static async Task ShowNoticeAsync(string message, object source)
    {
        await NoticeQueue.WaitAsync();
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
            var owner = desktop.Windows.FirstOrDefault(w => w.DataContext == source && w.IsVisible)
                ?? desktop.Windows.LastOrDefault(w => w.IsActive && w is not MessageDialog)
                ?? desktop.MainWindow;
            if (owner is null) return;
            if (!owner.IsVisible) owner.Show();
            if (owner.WindowState == global::Avalonia.Controls.WindowState.Minimized)
                owner.WindowState = global::Avalonia.Controls.WindowState.Normal;
            await new MessageDialog(message).ShowDialog(owner);
        }
        catch (Exception)
        {
            // 退出过程中窗口可能已关闭；不能让通知异常导致应用崩溃。
        }
        finally { NoticeQueue.Release(); }
    }

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

    /// <summary>编辑分组名称；保存回调返回错误文案时保留弹窗，取消不会调用保存。</summary>
    public static async Task<bool> EditProjectGroupAsync(
        string title, string initialName, Func<string, Task<string?>> save)
    {
        var owner = GetTopLevel();
        if (owner is null) return false;
        return await new GroupEditDialog(title, initialName, save).ShowDialog<bool?>(owner) is true;
    }
}
