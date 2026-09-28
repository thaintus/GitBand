using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitBinder.Desktop.ViewModels;

namespace GitBinder.Desktop.Views;

/// <summary>克隆表单只绑定现有用例；窗口关闭不能遗留失去进度/取消入口的后台传输。</summary>
public partial class CloneRepositoryDialog : Window
{
    private ProjectsViewModel? _viewModel;

    public CloneRepositoryDialog() => InitializeComponent();

    public CloneRepositoryDialog(ProjectsViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.IsCloneEditorOpen = true;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        RemoteUrlInput.Focus();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectsViewModel.IsCloneEditorOpen)
            && _viewModel is { IsCloneEditorOpen: false, IsCloneBusy: false }) Close(true);
        if (e.PropertyName == nameof(ProjectsViewModel.CloneFeedback)
            && _viewModel is { HasCloneFeedback: true })
        {
            // 错误出现后等待可见性绑定与布局更新，窄窗下自动展示可复制的错误正文。
            Dispatcher.UIThread.Post(() =>
            {
                if (IsVisible && _viewModel is { HasCloneFeedback: true } && ErrorPanel.IsVisible)
                    ErrorPanel.BringIntoView();
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => CancelOrClose();

    private void CancelOrClose()
    {
        if (_viewModel is { IsCloneBusy: true })
        {
            _viewModel.CancelTransferCommand.Execute(null);
            return;
        }
        Close(false);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_viewModel is not { IsCloneBusy: true }) return;
        // X / Alt+F4 同样请求取消，但在 Git 返回前保留窗口与进度，不能误报已经停止。
        e.Cancel = true;
        _viewModel.CancelTransferCommand.Execute(null);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModel is null) return;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.CloseCloneCommand.Execute(null);
        _viewModel = null;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        CancelOrClose();
    }
}
