using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace GitBinder.Desktop.Views;

/// <summary>结果文本可选择、滚动并复制，不写入日志或持久化。</summary>
public partial class MessageDialog : Window
{
    public MessageDialog() => InitializeComponent();
    public MessageDialog(string message) : this() => MessageText.Text = message;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Close();
    }
}
