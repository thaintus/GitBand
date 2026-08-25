using Avalonia.Controls;
using GitBinder.Desktop.ViewModels;

namespace GitBinder.Desktop.Views;

public partial class AccountEditWindow : Window
{
    private readonly AccountEditViewModel _viewModel;

    public AccountEditWindow()
    {
        InitializeComponent();
        _viewModel = null!;
    }

    public AccountEditWindow(AccountEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Title = viewModel.WindowTitle;
        _viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested()
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        Close(_viewModel.Saved);
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        Close(false);
    }

    protected override void OnClosed(System.EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        base.OnClosed(e);
    }
}