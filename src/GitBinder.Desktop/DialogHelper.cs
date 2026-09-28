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

    /// <summary>模态操作优先使用当前活动窗口，避免目录选择器被编辑窗口挡住。</summary>
    private static global::Avalonia.Controls.Window? GetActiveOwner()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;
        return desktop.Windows.LastOrDefault(window => window.IsVisible && window.IsActive && window is not MessageDialog)
            ?? desktop.MainWindow;
    }

    /// <summary>显示带返回结果的模态窗口；无窗口宿主时按取消处理。</summary>
    public static async Task<TResult?> ShowDialogAsync<TResult>(global::Avalonia.Controls.Window dialog)
        where TResult : struct
    {
        var owner = GetActiveOwner();
        if (owner is null) return null;
        return await dialog.ShowDialog<TResult?>(owner);
    }

    /// <summary>选择单个文件，返回路径或 null。</summary>
    public static async Task<string?> PickFileAsync()
    {
        var window = GetActiveOwner();
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
        var window = GetActiveOwner();
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

    /// <summary>由活动窗口承载确认；嵌套确认取消时不影响下面的编辑窗口。</summary>
    public static async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var owner = GetActiveOwner();
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
        var owner = GetActiveOwner();
        if (owner is null) return false;
        return await new GroupEditDialog(title, initialName, save).ShowDialog<bool?>(owner) is true;
    }

    /// <summary>平台新增/编辑共用模态表单；保存失败时由表单保留输入并展示错误。</summary>
    public static async Task<bool> EditPlatformAsync(string title, string initialName, string initialHost,
        Func<string, string, Task<string?>> save)
    {
        var owner = GetActiveOwner();
        if (owner is null) return false;
        return await new PlatformEditDialog(title, initialName, initialHost, save).ShowDialog<bool?>(owner) is true;
    }
}
