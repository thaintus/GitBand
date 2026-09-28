using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitBinder.Desktop.Localization;

namespace GitBinder.Desktop.Views;

/// <summary>平台表单只通过保存回调调用用例；取消不写入，失败留窗且正文可复制。</summary>
public partial class PlatformEditDialog : Window
{
    private readonly Func<string, string, Task<string?>>? _save;
    private bool _isSaving;

    public PlatformEditDialog() => InitializeComponent();

    public PlatformEditDialog(string title, string initialName, string initialHost,
        Func<string, string, Task<string?>> save) : this()
    {
        Title = title;
        Heading.Text = title;
        NameInput.Text = initialName;
        HostInput.Text = initialHost;
        _save = save;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        NameInput.Focus();
        NameInput.SelectAll();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_isSaving || _save is null) return;
        ErrorPanel.IsVisible = false;
        SetSaving(true);
        string? error;
        try
        {
            error = await _save(NameInput.Text ?? string.Empty, HostInput.Text ?? string.Empty);
        }
        catch (Exception)
        {
            error = LocalizationService.Instance.GetString("Platforms.Editor.SaveFailed");
        }
        finally { SetSaving(false); }

        if (error is null)
        {
            Close(true);
            return;
        }

        ErrorText.Text = error;
        ErrorPanel.IsVisible = true;
        NameInput.Focus();
    }

    private void SetSaving(bool saving)
    {
        _isSaving = saving;
        NameInput.IsEnabled = !saving;
        HostInput.IsEnabled = !saving;
        SaveButton.IsEnabled = !saving;
        CancelButton.IsEnabled = !saving;
        SaveButton.Content = LocalizationService.Instance.GetString(
            saving ? "Platforms.Editor.Saving" : "Common.Save");
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (!_isSaving) Close(false);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // 保存是短时本地写入，不能在写入中途关闭并让用户误以为已取消。
        if (_isSaving) e.Cancel = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        if (!_isSaving) Close(false);
    }
}
