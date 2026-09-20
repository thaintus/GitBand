using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitBinder.Application.Accounts;
using GitBinder.Application.Bindings;
using GitBinder.Application.Common;
using GitBinder.Application.Projects;
using GitBinder.Domain.Accounts;
using GitBinder.Domain.Bindings;
using GitBinder.Domain.Projects;
using GitBinder.Domain.Services;

namespace GitBinder.Desktop.ViewModels;

/// <summary>
/// 项目页 ViewModel。
/// </summary>
public partial class ProjectsViewModel : ViewModelBase
{
    private readonly IProjectRepository _repository;
    private readonly ProjectService _projectService;
    private readonly IBindingRepository _bindingRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly BindingService _bindingService;
    private readonly EffectiveAccountResolver _effectiveAccountResolver;
    private readonly ILocalizationService _localization;
    private readonly DirectoryPickerDelegate _directoryPicker;

    public ObservableCollection<ProjectItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoMatches))]
    private bool _hasItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchText))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoMatches))]
    private IReadOnlyList<ProjectItemViewModel> _filteredItems = [];

    [ObservableProperty]
    private ProjectItemViewModel? _selectedItem;

    [ObservableProperty]
    private string _feedback = string.Empty;

    public bool HasSearchText => !string.IsNullOrEmpty(SearchText);

    public bool HasNoMatches => HasItems && FilteredItems.Count == 0;

    public ProjectsViewModel(
        IProjectRepository repository,
        ProjectService projectService,
        IBindingRepository bindingRepository,
        IAccountRepository accountRepository,
        BindingService bindingService,
        EffectiveAccountResolver effectiveAccountResolver,
        ILocalizationService localization,
        DirectoryPickerDelegate directoryPicker)
    {
        _repository = repository;
        _projectService = projectService;
        _bindingRepository = bindingRepository;
        _accountRepository = accountRepository;
        _bindingService = bindingService;
        _effectiveAccountResolver = effectiveAccountResolver;
        _localization = localization;
        _directoryPicker = directoryPicker;
    }

    public async Task LoadAsync()
    {
        var projects = await _repository.GetAllAsync();
        var accounts = await _accountRepository.GetAllAsync();
        var selectableAccounts = accounts.Where(account => account.Enabled).ToList();
        var loadedItems = new List<ProjectItemViewModel>(projects.Count);
        foreach (var project in projects)
        {
            var binding = await _bindingRepository.GetByProjectIdAsync(project.Id);
            Account? boundAccount = null;
            if (binding is not null)
            {
                boundAccount = accounts.FirstOrDefault(account => account.Id == binding.AccountId);
            }

            var effectiveAccount = _effectiveAccountResolver.Resolve(project);
            var effectiveReason = _effectiveAccountResolver.ResolveReason(project);
            loadedItems.Add(new ProjectItemViewModel(
                project,
                binding,
                boundAccount,
                effectiveAccount,
                effectiveReason,
                selectableAccounts,
                _localization));
        }

        Items.Clear();
        foreach (var item in loadedItems)
        {
            Items.Add(item);
        }

        HasItems = Items.Count > 0;
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    /// <summary>仅筛选已载入的项目，复用条目以保留地址编辑与账号选择状态。</summary>
    private void ApplyFilter()
    {
        FilteredItems = Items.Where(item => ListSearch.Matches(
            SearchText,
            item.Name,
            item.PathText,
            item.OriginUrlDisplay,
            item.RemoteHost,
            item.ProtocolText,
            item.BoundAccount?.DisplayAlias,
            item.EffectiveAccount?.DisplayAlias)).ToList();
    }

    /// <summary>从统一的页头“新建”入口选择并登记仓库。</summary>
    [RelayCommand]
    private async Task CreateAsync()
    {
        Feedback = string.Empty;
        var path = await _directoryPicker();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var result = await _projectService.AddAsync(path);
        if (result.IsSuccess)
        {
            Feedback = string.Empty;
            await LoadAsync();
        }
        else
        {
            Feedback = _localization.GetString(result.Error!.Code, result.Error.Arguments);
        }
    }

    [RelayCommand]
    private async Task RemoveAsync(ProjectItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var confirmed = await DialogHelper.ConfirmAsync(
            _localization.GetString("Common.Confirm"),
            _localization.GetString("Projects.Remove.Confirm", item.Name),
            _localization.GetString("Common.Delete"));
        if (!confirmed)
        {
            return;
        }

        var result = await _projectService.RemoveAsync(item.Project.Id);
        if (result.IsSuccess)
        {
            Feedback = string.Empty;
            await LoadAsync();
        }
        else
        {
            Feedback = _localization.GetString(result.Error!.Code, result.Error.Arguments);
        }
    }

    [RelayCommand]
    private void StartEditRemote(ProjectItemViewModel? item)
    {
        item?.BeginRemoteEdit();
    }

    [RelayCommand]
    private async Task SaveRemoteAsync(ProjectItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var result = await _projectService.UpdateOriginUrlAsync(item.Project.Id, item.EditingOriginUrl);
        if (result.IsSuccess)
        {
            Feedback = string.Empty;
            await LoadAsync();
        }
        else
        {
            Feedback = _localization.GetString(result.Error!.Code, result.Error.Arguments);
        }
    }

    [RelayCommand]
    private void CancelEditRemote(ProjectItemViewModel? item)
    {
        item?.CancelRemoteEdit();
    }

    [RelayCommand]
    private void SwitchRemoteProtocol(ProjectItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.TrySwitchEditingOriginProtocol())
        {
            Feedback = string.Empty;
            return;
        }

        Feedback = _localization.GetString("Projects.ProtocolSwitch.Unsupported");
    }

    /// <summary>账号下拉选择后立即创建或修改项目绑定。</summary>
    public async Task SwitchBindingAccountAsync(ProjectItemViewModel item, Account account)
    {
        // 筛选隐藏再显示会重建账号选择控件；正在改绑时忽略其初始化事件。
        if (item.IsSwitchingBinding || account.Id == item.BoundAccount?.Id)
        {
            return;
        }

        item.IsSwitchingBinding = true;
        try
        {
            item.SelectedBindingAccount = account;
            var result = await _bindingService.BindAsync(item.Project.Id, account.Id);
            if (result.IsSuccess)
            {
                Feedback = string.Empty;
                await LoadAsync();
            }
            else
            {
                item.RestoreBoundAccountSelection();
                Feedback = _localization.GetString(result.Error!.Code, result.Error.Arguments);
            }
        }
        finally
        {
            item.IsSwitchingBinding = false;
        }
    }

    /// <summary>从项目卡片直接解除账号绑定。</summary>
    [RelayCommand]
    private async Task UnbindAsync(ProjectItemViewModel? item)
    {
        if (item?.Binding is null)
        {
            return;
        }

        var result = await _bindingService.UnbindAsync(item.Project.Id);
        if (result.IsSuccess)
        {
            Feedback = string.Empty;
            await LoadAsync();
        }
        else
        {
            Feedback = _localization.GetString(result.Error!.Code, result.Error.Arguments);
        }
    }

    /// <summary>使用当前项目的生效账号测试远程认证。</summary>
    [RelayCommand]
    private async Task TestBindingAsync(ProjectItemViewModel? item)
    {
        if (item?.Binding is null)
        {
            Feedback = _localization.GetString("Projects.BindingRequired");
            return;
        }

        Feedback = _localization.GetString("Test.Running");
        var result = await _bindingService.TestAsync(item.Binding);
        Feedback = result.Success
            ? _localization.GetString("Test.Success") + $"（{result.Duration.TotalMilliseconds:0} ms）"
            : _localization.GetString("Test.Failed") + "\n" + result.Output.Trim();
    }

}

/// <summary>
/// 项目列表项 ViewModel。
/// </summary>
public partial class ProjectItemViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;

    public Project Project { get; }

    public Binding? Binding { get; }

    public Account? BoundAccount { get; }

    public Account? EffectiveAccount { get; }

    public EffectiveReason EffectiveReason { get; }

    public IReadOnlyList<Account> AvailableAccounts { get; }

    [ObservableProperty]
    private Account? _selectedBindingAccount;

    [ObservableProperty]
    private bool _isSwitchingBinding;

    [ObservableProperty]
    private bool _isRemoteEditorOpen;

    [ObservableProperty]
    private string _editingOriginUrl = string.Empty;

    partial void OnEditingOriginUrlChanged(string value)
    {
        OnPropertyChanged(nameof(RemoteProtocolSwitchText));
    }

    public ProjectItemViewModel(
        Project project,
        Binding? binding,
        Account? boundAccount,
        Account? effectiveAccount,
        EffectiveReason effectiveReason,
        IReadOnlyList<Account> availableAccounts,
        ILocalizationService localization)
    {
        Project = project;
        Binding = binding;
        BoundAccount = boundAccount;
        EffectiveAccount = effectiveAccount;
        EffectiveReason = effectiveReason;
        AvailableAccounts = availableAccounts;
        SelectedBindingAccount = availableAccounts.FirstOrDefault(account => account.Id == boundAccount?.Id);
        _localization = localization;
        _localization.CultureChanged += (_, _) => RefreshLocalization();
    }

    public string Name => Project.Name;

    public string BoundAccountAlias => BoundAccount?.DisplayAlias ?? _localization.GetString("Projects.Unbound");

    public string EffectiveAccountAlias => EffectiveAccount?.DisplayAlias ?? _localization.GetString("Projects.Unbound");

    public string RemoteHost => Project.RemoteHost;

    /// <summary>展示远程地址时隐藏旧数据中可能存在的 HTTPS 用户信息或 Token。</summary>
    public string OriginUrlDisplay => SanitizeHttpUserInfo(Project.OriginUrl);

    public string ProtocolText => Project.RemoteProtocol.ToString().ToUpperInvariant();

    public string PathText => Project.RepositoryPath;

    public string BoundAccountLabel => _localization.GetString("Projects.BoundAccountLabel", BoundAccountAlias);

    public string EffectiveAccountLabel => _localization.GetString("Projects.EffectiveAccountLabel", EffectiveAccountAlias);

    public string EffectiveReasonLabel => _localization.GetString($"Projects.Reason.{EffectiveReason}");

    public bool HasBinding => Binding is not null;

    public string RemoteProtocolSwitchText
    {
        get
        {
            return GitRemoteUrlConverter.TrySwitchProtocol(EditingOriginUrl, out _, out var targetProtocol)
                ? _localization.GetString(targetProtocol is RemoteProtocol.Ssh
                    ? "Projects.SwitchToSsh"
                    : "Projects.SwitchToHttps")
                : _localization.GetString("Projects.SwitchProtocol");
        }
    }

    public void BeginRemoteEdit()
    {
        EditingOriginUrl = OriginUrlDisplay;
        IsRemoteEditorOpen = true;
    }

    public void CancelRemoteEdit()
    {
        EditingOriginUrl = OriginUrlDisplay;
        IsRemoteEditorOpen = false;
    }

    /// <summary>绑定失败时还原下拉显示，允许用户再次选择后重试。</summary>
    public void RestoreBoundAccountSelection()
    {
        SelectedBindingAccount = BoundAccount;
    }

    /// <summary>切换编辑框中的标准 SSH/HTTPS Remote；尚未保存到仓库。</summary>
    public bool TrySwitchEditingOriginProtocol()
    {
        if (!GitRemoteUrlConverter.TrySwitchProtocol(EditingOriginUrl, out var convertedUrl, out _))
        {
            return false;
        }

        EditingOriginUrl = convertedUrl;
        return true;
    }

    private static string SanitizeHttpUserInfo(string originUrl)
    {
        if (!Uri.TryCreate(originUrl, UriKind.Absolute, out var uri)
            || (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            || string.IsNullOrEmpty(uri.UserInfo))
        {
            return originUrl;
        }

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
        };
        return builder.Uri.AbsoluteUri;
    }

    public string ProtocolLabel => _localization.GetString("Projects.ProtocolLabel", ProtocolText);

    public string HostLabel => _localization.GetString("Projects.HostLabel", RemoteHost);

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(BoundAccountAlias));
        OnPropertyChanged(nameof(EffectiveAccountAlias));
        OnPropertyChanged(nameof(BoundAccountLabel));
        OnPropertyChanged(nameof(EffectiveAccountLabel));
        OnPropertyChanged(nameof(EffectiveReasonLabel));
        OnPropertyChanged(nameof(ProtocolLabel));
        OnPropertyChanged(nameof(HostLabel));
        OnPropertyChanged(nameof(RemoteProtocolSwitchText));
    }
}
