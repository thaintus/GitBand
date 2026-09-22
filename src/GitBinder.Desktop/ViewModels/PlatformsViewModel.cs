using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitBinder.Application.Accounts;
using GitBinder.Application.Common;
using GitBinder.Domain.Accounts;

namespace GitBinder.Desktop.ViewModels;

/// <summary>
/// 平台管理页 ViewModel：维护平台名称与 Host。
/// </summary>
public partial class PlatformsViewModel : ViewModelBase
{
    private readonly PlatformService _platformService;
    private readonly ILocalizationService _localization;

    public ObservableCollection<PlatformItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoMatches))]
    private bool _hasItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchText))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoMatches))]
    private IReadOnlyList<PlatformItemViewModel> _filteredItems = [];

    public bool HasSearchText => !string.IsNullOrEmpty(SearchText);

    public bool HasNoMatches => HasItems && FilteredItems.Count == 0;

    private string _feedback = string.Empty;
    public string Feedback
    {
        get => _feedback;
        set => SetNotice(ref _feedback, value);
    }

    // 编辑表单字段。
    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editHost = string.Empty;

    /// <summary>平台编辑器默认关闭，保证进入页面时首先看到列表。</summary>
    [ObservableProperty]
    private bool _isEditorOpen;

    private Guid? _editingId;

    public PlatformsViewModel(PlatformService platformService, ILocalizationService localization)
    {
        _platformService = platformService;
        _localization = localization;
    }

    public async Task LoadAsync()
    {
        var all = await _platformService.GetAllAsync();
        Items.Clear();
        // 启用的平台优先展示；同一状态内保持既有排序，避免切换状态时列表顺序不可预期。
        foreach (var p in all
                     .OrderByDescending(platform => platform.Enabled)
                     .ThenBy(platform => platform.SortOrder)
                     .ThenBy(platform => platform.Name, StringComparer.OrdinalIgnoreCase))
        {
            Items.Add(new PlatformItemViewModel(p, _localization));
        }

        HasItems = Items.Count > 0;
        ApplyFilter();
        ResetForm();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    private void ApplyFilter()
    {
        // Items 已按启用状态排序，过滤后沿用相对顺序，不干扰正在填写的平台表单。
        FilteredItems = Items.Where(item => ListSearch.Matches(
            SearchText, item.Name, item.Host)).ToList();
    }

    private void ResetForm()
    {
        _editingId = null;
        EditName = string.Empty;
        EditHost = string.Empty;
        IsEditorOpen = false;
        Feedback = string.Empty;
    }

    [RelayCommand]
    private void New()
    {
        ResetForm();
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void StartEdit(PlatformItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        _editingId = item.Platform.Id;
        EditName = item.Platform.Name;
        EditHost = item.Platform.Host;
        IsEditorOpen = true;
        Feedback = string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Feedback = string.Empty;

        if (_editingId is Guid id)
        {
            var result = await _platformService.UpdateAsync(id, EditName, EditHost);
            if (!result.IsSuccess)
            {
                Feedback = _localization.GetString(result.Error!.Code, result.Error.Arguments);
                return;
            }
        }
        else
        {
            var result = await _platformService.CreateAsync(EditName, EditHost);
            if (!result.IsSuccess)
            {
                Feedback = _localization.GetString(result.Error!.Code, result.Error.Arguments);
                return;
            }
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(PlatformItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var confirmed = await DialogHelper.ConfirmAsync(
            _localization.GetString("Common.Confirm"),
            _localization.GetString("Platforms.Delete.Confirm", item.Name),
            _localization.GetString("Common.Delete"));
        if (!confirmed)
        {
            return;
        }

        var result = await _platformService.DeleteAsync(item.Platform.Id);
        if (result.IsSuccess)
        {
            await LoadAsync();
        }
        else
        {
            Feedback = _localization.GetString(result.Error!.Code, result.Error.Arguments);
        }
    }

    [RelayCommand]
    private async Task ToggleEnabledAsync(PlatformItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var result = await _platformService.SetEnabledAsync(item.Platform.Id, !item.Platform.Enabled);
        if (result.IsSuccess)
        {
            await LoadAsync();
        }
        else
        {
            Feedback = _localization.GetString(result.Error!.Code, result.Error.Arguments);
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ResetForm();
    }

    [RelayCommand]
    private async Task RestoreDefaultsAsync()
    {
        await _platformService.RestoreDefaultsAsync();
        await LoadAsync();
    }
}

/// <summary>平台列表项 ViewModel。</summary>
public sealed class PlatformItemViewModel
{
    private readonly GitBinder.Application.Common.ILocalizationService _localization;

    public GitPlatform Platform { get; }

    public PlatformItemViewModel(GitPlatform platform, GitBinder.Application.Common.ILocalizationService localization)
    {
        Platform = platform;
        _localization = localization;
    }

    public string Name => Platform.Name;

    public string Host => Platform.Host;

    public bool Enabled => Platform.Enabled;

    public string StatusText => Enabled
        ? _localization.GetString("Platforms.Status.Enabled")
        : _localization.GetString("Platforms.Status.Disabled");

    public string ToggleText => Enabled
        ? _localization.GetString("Platforms.Disable")
        : _localization.GetString("Platforms.Enable");
}
