using CommunityToolkit.Mvvm.ComponentModel;
using GitBinder.Application.Accounts;
using GitBinder.Application.Bindings;
using GitBinder.Application.Projects;

namespace GitBinder.Desktop.ViewModels;

/// <summary>
/// 总览页 ViewModel。
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    private readonly IAccountRepository _accountRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IBindingRepository _bindingRepository;

    [ObservableProperty]
    private int _accountCount;

    [ObservableProperty]
    private int _projectCount;

    [ObservableProperty]
    private int _bindingCount;

    [ObservableProperty]
    private string _defaultAccountAlias = string.Empty;

    public DashboardViewModel(
        IAccountRepository accountRepository,
        IProjectRepository projectRepository,
        IBindingRepository bindingRepository)
    {
        _accountRepository = accountRepository;
        _projectRepository = projectRepository;
        _bindingRepository = bindingRepository;
    }

    public async Task LoadAsync()
    {
        var accounts = await _accountRepository.GetAllAsync();
        var projects = await _projectRepository.GetAllAsync();
        var bindings = await _bindingRepository.GetAllAsync();

        AccountCount = accounts.Count;
        ProjectCount = projects.Count;
        BindingCount = bindings.Count;

        var defaultAccount = await _accountRepository.GetDefaultAsync();
        DefaultAccountAlias = defaultAccount?.DisplayAlias ?? string.Empty;
    }
}