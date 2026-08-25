using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitBinder.Desktop.Localization;

namespace GitBinder.Desktop.Views;

/// <summary>危险操作的二次确认窗口。</summary>
public partial class ConfirmationDialog : Window
{
    public string Message { get; }

    public string ConfirmText { get; }

    public string CancelText { get; } = "";

    public ConfirmationDialog()
    {
        InitializeComponent();
        Message = string.Empty;
        ConfirmText = string.Empty;
    }

    public ConfirmationDialog(string title, string message, string confirmText)
    {
        InitializeComponent();
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = LocalizationService.Instance.GetString("Common.Cancel");
        DataContext = this;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(false);
        }
    }
}
