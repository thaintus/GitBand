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
    private readonly Func<string, string, string, Func<string, string, Task<string?>>, Task<bool>> _editPlatform;

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

    /// <summary>阻止新建和编辑命令重复打开模态窗口。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NewCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartEditCommand))]
    private bool _isEditorOpen;

    public PlatformsViewModel(PlatformService platformService, ILocalizationService localization,
        Func<string, string, string, Func<string, string, Task<string?>>, Task<bool>>? editPlatform = null)
    {
        _platformService = platformService;
        _localization = localization;
        _editPlatform = editPlatform ?? DialogHelper.EditPlatformAsync;
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
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    private void ApplyFilter()
    {
        // Items 已按启用状态排序，过滤后沿用相对顺序；弹窗表单独立维护输入。
        FilteredItems = Items.Where(item => ListSearch.Matches(
            SearchText, item.Name, item.Host)).ToList();
    }

    private bool CanOpenEditor() => !IsEditorOpen;

    [RelayCommand(CanExecute = nameof(CanOpenEditor))]
    private Task NewAsync() => OpenEditorAsync(null);

    [RelayCommand(CanExecute = nameof(CanOpenEditor))]
    private Task StartEditAsync(PlatformItemViewModel? item)
        => item is null ? Task.CompletedTask : OpenEditorAsync(item);

    private async Task OpenEditorAsync(PlatformItemViewModel? item)
    {
        if (IsEditorOpen) return;
        IsEditorOpen = true;
        Feedback = string.Empty;
        bool saved;
        try
        {
            saved = await _editPlatform(
                _localization.GetString(item is null ? "Platforms.Editor.New" : "Platforms.Editor.Edit"),
                item?.Name ?? string.Empty, item?.Host ?? string.Empty,
                (name, host) => SavePlatformAsync(item?.Platform.Id, name, host));
        }
        catch (Exception)
        {
            Feedback = _localization.GetString("Platforms.Editor.OpenFailed");
            return;
        }
        finally { IsEditorOpen = false; }

        if (!saved) return;
        try { await LoadAsync(); }
        catch (Exception)
        {
            // 写入已经完成，刷新失败不能被误报为保存失败，也不能重复新建。
            Feedback = _localization.GetString("Platforms.ReloadFailed");
        }
    }

    private async Task<string?> SavePlatformAsync(Guid? id, string name, string host)
    {
        try
        {
            var result = id is Guid editingId
                ? await _platformService.UpdateAsync(editingId, name, host)
                : await _platformService.CreateAsync(name, host);
            return result.IsSuccess ? null : _localization.GetString(result.Error!.Code, result.Error.Arguments);
        }
        catch (Exception)
        {
            // 原始异常可能包含路径等信息，只返回可读且可复制的本地化错误。
            return _localization.GetString("Platforms.Editor.SaveFailed");
        }
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
