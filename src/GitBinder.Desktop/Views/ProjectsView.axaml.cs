using Avalonia.Controls;
using GitBinder.Domain.Accounts;
using GitBinder.Desktop.ViewModels;

namespace GitBinder.Desktop.Views;

public partial class ProjectsView : UserControl
{
    public ProjectsView()
    {
        InitializeComponent();
    }

    private async void OnProjectGroupSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { DataContext: ProjectItemViewModel item }
            && DataContext is ProjectsViewModel viewModel
            && e.AddedItems.OfType<ProjectGroupItemViewModel>().FirstOrDefault() is { } group)
            await viewModel.SwitchProjectGroupAsync(item, group);
    }

    private async void OnBindingAccountSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { DataContext: ProjectItemViewModel item }
            || DataContext is not ProjectsViewModel viewModel
            || e.AddedItems.OfType<Account>().FirstOrDefault() is not { } selectedAccount
            || selectedAccount.Id == item.BoundAccount?.Id)
        {
            return;
        }

        await viewModel.SwitchBindingAccountAsync(item, selectedAccount);
    }
}
