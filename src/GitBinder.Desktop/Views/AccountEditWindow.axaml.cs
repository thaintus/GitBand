using Avalonia.Controls;
using Avalonia.Input;
using GitBinder.Desktop.ViewModels;

namespace GitBinder.Desktop.Views;

public partial class AccountEditWindow : Window
{
    private readonly AccountEditViewModel? _viewModel;
    private bool _closingAfterSave;

    public AccountEditWindow()
    {
        InitializeComponent();
    }

    public AccountEditWindow(AccountEditViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        Title = viewModel.WindowTitle;
        _viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested()
    {
        if (_viewModel is null) return;
        // 成功事件仍在 SaveCommand 内触发，需要允许这一条确定已完成的关闭路径。
        _closingAfterSave = true;
        Close(_viewModel.Saved);
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // 冒泡处理未消费的按键：下拉菜单先使用 Esc 收起，不同时关闭账号表单。
        if (e.Key != Key.Escape || e.Handled) return;
        e.Handled = true;
        Close(false);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // Esc、取消与标题栏 X 行为一致，避免保存/导入仍在进行却被误认为已取消。
        if (!_closingAfterSave && _viewModel is not null
            && (_viewModel.SaveCommand.IsRunning || _viewModel.UploadKeyCommand.IsRunning
                || _viewModel.ClearKeyCommand.IsRunning)) e.Cancel = true;
    }

    protected override void OnClosed(System.EventArgs e)
    {
        if (_viewModel is not null) _viewModel.CloseRequested -= OnCloseRequested;
        base.OnClosed(e);
    }
}
