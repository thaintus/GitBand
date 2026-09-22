using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitBinder.Desktop.Localization;

namespace GitBinder.Desktop.Views;

/// <summary>分组名称表单仅通过保存回调交给用例处理，失败信息留在窗口内供选择复制。</summary>
public partial class GroupEditDialog : Window
{
    private readonly Func<string, Task<string?>>? _save;
    private bool _isSaving;

    public GroupEditDialog() => InitializeComponent();

    public GroupEditDialog(string title, string initialName, Func<string, Task<string?>> save) : this()
    {
        Title = title;
        Heading.Text = title;
        NameInput.Text = initialName;
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
            error = await _save(NameInput.Text ?? string.Empty);
        }
        catch (Exception)
        {
            error = LocalizationService.Instance.GetString("GROUP_SAVE_FAILED");
        }
        finally
        {
            SetSaving(false);
        }

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
        SaveButton.IsEnabled = !saving;
        CancelButton.IsEnabled = !saving;
        SaveButton.Content = LocalizationService.Instance.GetString(saving ? "Groups.Saving" : "Common.Save");
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (!_isSaving) Close(false);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // 保存只涉及短时本地写入，避免写入期间关闭后误以为已取消。
        if (_isSaving) e.Cancel = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        if (!_isSaving) Close(false);
    }
}
