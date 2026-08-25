using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitBinder.Application.Accounts;
using GitBinder.Application.Common;
using GitBinder.Application.GlobalMode;
using GitBinder.Domain.Accounts;

namespace GitBinder.Desktop.ViewModels;

/// <summary>
/// 主窗口 ViewModel，负责导航与全局状态。
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private readonly GlobalModeService _globalModeService;
    private readonly IAccountRepository _accountRepository;

    private readonly DashboardViewModel _dashboard;
    private readonly AccountsViewModel _accounts;
    private readonly ProjectsViewModel _projects;
    private readonly PlatformsViewModel _platforms;
    private readonly SettingsViewModel _settings;

    public ObservableCollection<NavItem> NavigationItems { get; } = [];

    public ObservableCollection<Account> Accounts { get; } = [];

    [ObservableProperty]
    private ViewModelBase _currentPage = null!;

    [ObservableProperty]
    private NavItem? _selectedNav;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GlobalModeStatusText))]
    [NotifyPropertyChangedFor(nameof(GlobalModeToggleText))]
    [NotifyPropertyChangedFor(nameof(GlobalModeDisabled))]
    private bool _globalModeEnabled;

    [ObservableProperty]
    private string _globalAccountAlias = string.Empty;

    [ObservableProperty]
    private Account? _selectedGlobalAccount;

    [ObservableProperty]
    private string _globalModeFeedback = string.Empty;

    public MainViewModel(
        ILocalizationService localization,
        GlobalModeService globalModeService,
        IAccountRepository accountRepository,
        DashboardViewModel dashboard,
        AccountsViewModel accounts,
        ProjectsViewModel projects,
        PlatformsViewModel platforms,
        SettingsViewModel settings)
    {
        _localization = localization;
        _globalModeService = globalModeService;
        _accountRepository = accountRepository;
        _dashboard = dashboard;
        _accounts = accounts;
        _projects = projects;
        _platforms = platforms;
        _settings = settings;

        _localization.CultureChanged += (_, _) => RefreshLocalization();

        BuildNavigation();
        RefreshLocalization();

        _currentPage = _dashboard;
        SelectedNav = NavigationItems[0];
    }

    private void BuildNavigation()
    {
        NavigationItems.Add(new NavItem("Nav.Dashboard", _dashboard));
        NavigationItems.Add(new NavItem("Nav.Accounts", _accounts));
        NavigationItems.Add(new NavItem("Nav.Projects", _projects));
        NavigationItems.Add(new NavItem("Nav.Platforms", _platforms));
        NavigationItems.Add(new NavItem("Nav.Settings", _settings));
    }

    private void RefreshLocalization()
    {
        foreach (var item in NavigationItems)
        {
            item.Refresh(_localization);
        }

        OnPropertyChanged(nameof(GlobalModeStatusText));
    }

    partial void OnSelectedNavChanged(NavItem? value)
    {
        if (value is not null)
        {
            CurrentPage = value.Page;
            _ = LoadPageAsync(value.Page);
        }
    }

    /// <summary>导航切换后即时加载该页面的数据，避免升级或首次进入时看到空列表。</summary>
    private async Task LoadPageAsync(ViewModelBase page)
    {
        try
        {
            switch (page)
            {
                case DashboardViewModel:
                    await _dashboard.LoadAsync();
                    break;
                case AccountsViewModel:
                    await _accounts.LoadAsync();
                    break;
                case ProjectsViewModel:
                    await _projects.LoadAsync();
                    break;
                case PlatformsViewModel:
                    await _platforms.LoadAsync();
                    break;
                case SettingsViewModel:
                    await _settings.LoadAsync();
                    break;
            }
        }
        catch (Exception)
        {
            // 页面加载异常不应让导航失效，也不能再静默显示为空列表。
            GlobalModeFeedback = _localization.GetString("Navigation.LoadFailed");
        }
    }

    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        await _dashboard.LoadAsync();
        await _accounts.LoadAsync();
        await _projects.LoadAsync();
        await _platforms.LoadAsync();
        await LoadGlobalModeAsync();
    }

    [RelayCommand]
    private async Task LoadGlobalModeAsync()
    {
        // 载入账号列表（供切换选择）。
        var all = await _accountRepository.GetAllAsync();
        Accounts.Clear();
        foreach (var a in all)
        {
            Accounts.Add(a);
        }

        var config = await _globalModeService.GetConfigAsync();
        GlobalModeEnabled = config.Enabled;

        if (config.Enabled && config.GlobalAccountId is Guid id)
        {
            // ComboBox 以引用匹配 SelectedItem；必须使用 Accounts 集合内的实例，
            // 否则全局账号已生效但下拉框会显示为空。
            var account = Accounts.FirstOrDefault(a => a.Id == id);
            GlobalAccountAlias = account?.DisplayAlias ?? string.Empty;
            SelectedGlobalAccount = account;
        }
        else
        {
            var def = Accounts.FirstOrDefault(a => a.IsDefault && a.Enabled);
            GlobalAccountAlias = def?.DisplayAlias ?? string.Empty;
            SelectedGlobalAccount = def;
        }

        OnPropertyChanged(nameof(GlobalModeStatusText));
    }

    [RelayCommand]
    private async Task EnableGlobalModeAsync()
    {
        GlobalModeFeedback = string.Empty;
        var id = SelectedGlobalAccount?.Id;
        var result = await _globalModeService.EnableAsync(id);
        await LoadGlobalModeAsync();
        await _projects.LoadAsync();
        if (result.IsSuccess)
        {
            return;
        }

        GlobalModeFeedback = _localization.GetString(result.Error!.Code, result.Error.Arguments);
    }

    [RelayCommand]
    private async Task SwitchGlobalAccountAsync()
    {
        if (SelectedGlobalAccount is null)
        {
            return;
        }

        GlobalModeFeedback = string.Empty;
        var result = await _globalModeService.SwitchAccountAsync(SelectedGlobalAccount.Id);
        await LoadGlobalModeAsync();
        await _projects.LoadAsync();
        if (result.IsSuccess)
        {
            return;
        }

        GlobalModeFeedback = _localization.GetString(result.Error!.Code, result.Error.Arguments);
    }

    [RelayCommand]
    private async Task DisableGlobalModeAsync()
    {
        GlobalModeFeedback = string.Empty;
        var result = await _globalModeService.DisableAsync();
        await LoadGlobalModeAsync();
        await _projects.LoadAsync();
        if (result.IsSuccess)
        {
            return;
        }

        GlobalModeFeedback = _localization.GetString(result.Error!.Code, result.Error.Arguments);
    }

    public async Task InitializeAsync()
    {
        var globalModeResult = await _globalModeService.EnsureAppliedAsync();
        if (!globalModeResult.IsSuccess)
        {
            GlobalModeFeedback = _localization.GetString(
                globalModeResult.Error!.Code,
                globalModeResult.Error.Arguments);
        }

        await LoadGlobalModeAsync();
        await _dashboard.LoadAsync();
    }

    public string GlobalModeStatusText =>
        GlobalModeEnabled
            ? _localization.GetString("Dashboard.On")
            : _localization.GetString("Dashboard.Off");

    public string GlobalModeToggleText =>
        GlobalModeEnabled
            ? _localization.GetString("GlobalMode.Disable")
            : _localization.GetString("GlobalMode.Enable");

    public bool GlobalModeDisabled => !GlobalModeEnabled;
}

/// <summary>
/// 导航项。
/// </summary>
public sealed partial class NavItem : ViewModelBase
{
    public string Key { get; }

    public ViewModelBase Page { get; }

    public NavItem(string key, ViewModelBase page)
    {
        Key = key;
        Page = page;
    }

    [ObservableProperty]
    private string _label = string.Empty;

    public void Refresh(ILocalizationService localization)
    {
        Label = localization.GetString(Key);
    }
}
