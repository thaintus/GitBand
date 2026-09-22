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
    private readonly ProjectTransferService _transferService;
    private readonly Func<string, string, string, Task<bool>> _confirmProtocolChange;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanManageProjects))]
    private bool _isBindingBusy;
    private CancellationTokenSource? _transferCancellation;
    private string _suggestedCloneFolderName = string.Empty;
    [ObservableProperty] private string _operationProgress = string.Empty;

    [ObservableProperty]
    private bool _isCloneEditorOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanManageProjects))]
    private bool _isTransferBusy;

    [ObservableProperty]
    private string _cloneRemoteUrl = string.Empty;

    [ObservableProperty]
    private string _cloneParentDirectory = string.Empty;

    [ObservableProperty]
    private string _cloneFolderName = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<Account> _cloneAccounts = [];

    [ObservableProperty]
    private Account? _selectedCloneAccount;

    partial void OnCloneRemoteUrlChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(CloneFolderName) && CloneFolderName != _suggestedCloneFolderName) return;
        if (!GitTransferRemote.TryParse(value, out _)) return;
        var name = value.Trim().TrimEnd('/').Split('/').LastOrDefault() ?? string.Empty;
        if (name.StartsWith("git@", StringComparison.Ordinal)) name = name[(name.IndexOf(':') + 1)..];
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        if (!name.Contains(':') && !name.Contains('@'))
        {
            _suggestedCloneFolderName = name;
            CloneFolderName = name;
        }
    }

    public ObservableCollection<ProjectItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoMatches))]
    private bool _hasItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchText))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoMatches))]
    [NotifyPropertyChangedFor(nameof(ProjectCountLabel))]
    private IReadOnlyList<ProjectItemViewModel> _filteredItems = [];

    [ObservableProperty]
    private ProjectItemViewModel? _selectedItem;

    private string _feedback = string.Empty;
    public string Feedback
    {
        get => _feedback;
        set => SetNotice(ref _feedback, value);
    }

    public bool HasSearchText => !string.IsNullOrEmpty(SearchText);

    public bool HasNoMatches => HasItems && FilteredItems.Count == 0;

    public string ProjectCountLabel => _localization.GetString("Projects.VisibleCount", FilteredItems.Count, Items.Count);

    public ProjectsViewModel(
        IProjectRepository repository,
        ProjectService projectService,
        IBindingRepository bindingRepository,
        IAccountRepository accountRepository,
        BindingService bindingService,
        EffectiveAccountResolver effectiveAccountResolver,
        ILocalizationService localization,
        DirectoryPickerDelegate directoryPicker,
        ProjectTransferService transferService,
        ProjectGroupService groupService,
        Func<string, string, string, Task<bool>>? confirmProtocolChange = null)
    {
        _repository = repository;
        _projectService = projectService;
        _bindingRepository = bindingRepository;
        _accountRepository = accountRepository;
        _bindingService = bindingService;
        _effectiveAccountResolver = effectiveAccountResolver;
        _localization = localization;
        _directoryPicker = directoryPicker;
        _transferService = transferService;
        _groupService = groupService;
        _confirmProtocolChange = confirmProtocolChange ?? DialogHelper.ConfirmAsync;
    }

    public async Task LoadAsync()
    {
        if (IsTransferBusy) return;
        var projects = await _repository.GetAllAsync();
        var accounts = await _accountRepository.GetAllAsync();
        var selectableAccounts = accounts.Where(account => account.Enabled).ToList();
        CloneAccounts = selectableAccounts;
        SelectedCloneAccount = selectableAccounts.FirstOrDefault(a => a.Id == SelectedCloneAccount?.Id)
            ?? selectableAccounts.FirstOrDefault(a => a.IsDefault) ?? selectableAccounts.FirstOrDefault();
        var loadedItems = new List<ProjectItemViewModel>(projects.Count);
        foreach (var storedProject in projects)
        {
            var refresh = await _projectService.RefreshMetadataAsync(storedProject.Id);
            var project = refresh.IsSuccess ? refresh.Value! : storedProject;
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
                _localization)
            {
                MetadataWarning = refresh.IsSuccess
                    ? (string.IsNullOrWhiteSpace(project.OriginUrl) ? _localization.GetString("Projects.NoOrigin") : string.Empty)
                    : _localization.GetString(refresh.Error!.Code, refresh.Error.Arguments)
                        + " " + _localization.GetString("Projects.CachedMetadata"),
            });
        }

        Items.Clear();
        foreach (var item in loadedItems)
        {
            Items.Add(item);
        }

        HasItems = Items.Count > 0;
        await ReloadGroupsAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    /// <summary>仅筛选已载入的项目，复用条目以保留地址编辑与账号选择状态。</summary>
    private void ApplyFilter()
    {
        FilteredItems = Items.Where(item => (SelectedGroup is null || SelectedGroup.IsAll || item.GroupId == SelectedGroup.Id)
            && ListSearch.Matches(
            SearchText,
            item.Name,
            item.PathText,
            item.OriginUrlDisplay,
            item.RemoteHost,
            item.ProtocolText,
            item.BoundAccount?.DisplayAlias,
            item.EffectiveAccount?.DisplayAlias)).ToList();
    }

    [RelayCommand]
    private void OpenClone() => IsCloneEditorOpen = true;

    [RelayCommand]
    private void CloseClone()
    {
        if (!IsTransferBusy) IsCloneEditorOpen = false;
    }

    [RelayCommand]
    private async Task BrowseCloneDirectoryAsync()
    {
        try
        {
            var path = await _directoryPicker();
            if (!string.IsNullOrWhiteSpace(path)) CloneParentDirectory = path;
        }
        catch (Exception) { Feedback = _localization.GetString("CLONE_PARENT_INVALID"); }
    }

    [RelayCommand]
    private void CancelTransfer() => _transferCancellation?.Cancel();

    [RelayCommand]
    private async Task CloneAsync()
    {
        if (!CanManageProjects) return;
        if (SelectedCloneAccount is null)
        {
            Feedback = _localization.GetString("TRANSFER_ACCOUNT_INVALID");
            return;
        }
        var selected = SelectedCloneAccount;
        await RunTransferAsync(async ct =>
        {
            var result = await _transferService.CloneAsync(CloneRemoteUrl, CloneParentDirectory, CloneFolderName, selected.Id, ct);
            if (result.IsSuccess)
            {
                IsCloneEditorOpen = false;
                CloneRemoteUrl = string.Empty;
                CloneFolderName = string.Empty;
            }
            return result;
        }, "Projects.CloneSuccess", () => _effectiveAccountResolver.ResolveForClone(selected)?.DisplayAlias);
    }

    [RelayCommand]
    private async Task PullAsync(ProjectItemViewModel? item)
    {
        if (item is null || !CanManageProjects) return;
        IsBindingBusy = true;
        try
        {
            if (!await EnsureCompatibleProtocolAsync(item.Project.Id)) return;
            await RunTransferAsync(ct => _transferService.PullAsync(item.Project.Id, ct), "Projects.PullSuccess",
                () => _effectiveAccountResolver.Resolve(item.Project)?.DisplayAlias);
        }
        catch (Exception) { Feedback = _localization.GetString("TRANSFER_FAILED"); }
        finally { IsBindingBusy = false; }
    }

    private async Task RunTransferAsync(Func<CancellationToken, Task<GitBinder.Domain.Common.Result>> operation,
        string successKey, Func<string?> accountAlias)
    {
        IsTransferBusy = true;
        GroupPullDetails = string.Empty;
        _transferCancellation = new CancellationTokenSource();
        string message;
        try
        {
            OperationProgress = _localization.GetString("Projects.TransferRunning", accountAlias() ?? string.Empty);
            var result = await operation(_transferCancellation.Token);
            message = result.IsSuccess ? _localization.GetString(successKey)
                : _localization.GetString(result.Error!.Code, result.Error.Arguments.Cast<object>().ToArray());
        }
        catch (Exception) { message = _localization.GetString("TRANSFER_FAILED"); }
        finally
        {
            _transferCancellation.Dispose();
            _transferCancellation = null;
            IsTransferBusy = false;
            OperationProgress = string.Empty;
        }
        try { await LoadAsync(); }
        catch (Exception) { message += " " + _localization.GetString("Projects.TransferReloadFailed"); }
        Feedback = message;
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

    [RelayCommand]
    private void StartEditPath(ProjectItemViewModel? item)
    {
        if (item is null) return;
        item.EditingPath = item.PathText;
        item.IsPathEditorOpen = true;
    }

    [RelayCommand]
    private void CancelEditPath(ProjectItemViewModel? item)
    {
        if (item is null) return;
        item.EditingPath = item.PathText;
        item.IsPathEditorOpen = false;
    }

    [RelayCommand]
    private async Task BrowsePathAsync(ProjectItemViewModel? item)
    {
        if (item is null) return;
        try
        {
            var path = await _directoryPicker();
            if (!string.IsNullOrWhiteSpace(path)) item.EditingPath = path;
        }
        catch (Exception)
        {
            Feedback = _localization.GetString("PROJECT_PATH_UPDATE_FAILED");
        }
    }

    [RelayCommand]
    private async Task SavePathAsync(ProjectItemViewModel? item)
    {
        if (item is null || item.IsPathSaving) return;
        item.IsPathSaving = true;
        try
        {
            var newPath = item.EditingPath.Trim();
            var confirmed = await DialogHelper.ConfirmAsync(
                _localization.GetString("Common.Confirm"),
                _localization.GetString("Projects.PathConfirm", item.PathText, newPath),
                _localization.GetString("Projects.SavePath"));
            if (!confirmed) return;

            var result = await _projectService.UpdatePathAsync(item.Project.Id, newPath);
            if (result.IsSuccess)
            {
                await LoadAsync();
                Feedback = _localization.GetString("Projects.PathUpdated");
            }
            else
            {
                Feedback = _localization.GetString(result.Error!.Code, result.Error.Arguments);
            }
        }
        catch (Exception)
        {
            Feedback = _localization.GetString("PROJECT_PATH_UPDATE_FAILED");
        }
        finally
        {
            item.IsPathSaving = false;
        }
    }

    /// <summary>账号下拉选择后立即创建或修改项目绑定。</summary>
    public async Task SwitchBindingAccountAsync(ProjectItemViewModel item, Account account)
    {
        // 筛选隐藏再显示会重建账号选择控件；正在改绑时忽略其初始化事件。
        if (!CanManageProjects || item.IsSwitchingBinding || account.Id == item.BoundAccount?.Id)
        {
            return;
        }

        item.IsSwitchingBinding = true;
        IsBindingBusy = true;
        var bindingSaved = false;
        try
        {
            item.SelectedBindingAccount = account;
            var result = await _bindingService.BindAsync(item.Project.Id, account.Id);
            if (result.IsSuccess)
            {
                bindingSaved = true;
                Feedback = string.Empty;
                // 先保留用户已选择的绑定；地址协议变更必须再次确认，取消不撤销绑定。
                await EnsureCompatibleProtocolAsync(item.Project.Id);
                await LoadAsync();
            }
            else
            {
                item.RestoreBoundAccountSelection();
                Feedback = _localization.GetString(result.Error!.Code, result.Error.Arguments);
            }
        }
        catch (Exception)
        {
            if (!bindingSaved) item.RestoreBoundAccountSelection();
            Feedback = _localization.GetString(bindingSaved ? "Projects.BindingReloadFailed" : "BINDING_APPLY_FAILED");
        }
        finally
        {
            item.IsSwitchingBinding = false;
            IsBindingBusy = false;
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
        if (!CanManageProjects) return;
        if (item?.Binding is null)
        {
            Feedback = _localization.GetString("Projects.BindingRequired");
            return;
        }

        IsBindingBusy = true;
        try
        {
            if (!await EnsureCompatibleProtocolAsync(item.Project.Id)) return;
            OperationProgress = _localization.GetString("Test.Running");
            var result = await _bindingService.TestAsync(item.Binding);
            Feedback = result.Success
                ? _localization.GetString("Test.Success") + $"（{result.Duration.TotalMilliseconds:0} ms）"
                : _localization.GetString("Test.Failed") + "\n" + (result.Error is { } error
                    ? _localization.GetString(error.Code, error.Arguments.Cast<object>().ToArray())
                    : _localization.GetString("TEST_FAILED"));
        }
        catch (Exception) { Feedback = _localization.GetString("Test.Failed"); }
        finally { OperationProgress = string.Empty; IsBindingBusy = false; }
    }

    /// <summary>协议不匹配时仅生成预览，明确确认后才写 origin；取消不执行网络测试或传输。</summary>
    private async Task<bool> EnsureCompatibleProtocolAsync(Guid projectId)
    {
        var preview = await _bindingService.GetProtocolChangeAsync(projectId);
        if (!preview.IsSuccess)
        {
            Feedback = _localization.GetString(preview.Error!.Code, preview.Error.Arguments.Cast<object>().ToArray());
            return false;
        }
        if (preview.Value is not { } change) return true;

        var confirmed = await _confirmProtocolChange(
            _localization.GetString("Projects.ProtocolSwitch.ConfirmTitle"),
            _localization.GetString("Projects.ProtocolSwitch.Confirm", change.AccountName,
                change.TargetProtocol.ToString().ToUpperInvariant(), change.CurrentUrl, change.TargetUrl),
            _localization.GetString("Projects.ProtocolSwitch.ConfirmAction"));
        if (!confirmed) return false;

        var result = await _bindingService.ChangeProtocolAsync(change);
        if (!result.IsSuccess)
        {
            Feedback = _localization.GetString(result.Error!.Code, result.Error.Arguments.Cast<object>().ToArray());
            return false;
        }
        await LoadAsync();
        return true;
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
    private bool _isPathEditorOpen;

    [ObservableProperty]
    private bool _isPathSaving;

    [ObservableProperty]
    private string _editingPath = string.Empty;

    public string MetadataWarning { get; init; } = string.Empty;

    public bool HasMetadataWarning => !string.IsNullOrEmpty(MetadataWarning);

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
