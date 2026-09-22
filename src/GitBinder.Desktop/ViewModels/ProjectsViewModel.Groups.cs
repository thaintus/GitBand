using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitBinder.Application.Projects;

namespace GitBinder.Desktop.ViewModels;

public partial class ProjectsViewModel
{
    private readonly ProjectGroupService _groupService;

    [ObservableProperty] private IReadOnlyList<ProjectGroupItemViewModel> _groups = [];
    [ObservableProperty] private ProjectGroupItemViewModel? _selectedGroup;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanManageProjects))]
    private bool _isGroupBusy;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGroupPullDetails))]
    private string _groupPullDetails = string.Empty;

    public bool CanManageProjects => !IsTransferBusy && !IsGroupBusy && !IsBindingBusy;
    public bool HasGroupPullDetails => !string.IsNullOrEmpty(GroupPullDetails);
    partial void OnSelectedGroupChanged(ProjectGroupItemViewModel? value) => ApplyFilter();

    private async Task ReloadGroupsAsync()
    {
        var definitions = await _groupService.GetAllAsync();
        var membership = await _groupService.GetMembershipsAsync();
        var selectedId = SelectedGroup?.Id;
        var selectedAll = SelectedGroup?.IsAll ?? true;
        var groups = new List<ProjectGroupItemViewModel>
        {
            new(null, _localization.GetString("Groups.All"), Items.Count, true),
            new(null, _localization.GetString("Groups.Ungrouped"), Items.Count(p => !membership.ContainsKey(p.Project.Id)), false),
        };
        groups.AddRange(definitions.Select(g => new ProjectGroupItemViewModel(g.Id, g.Name,
            Items.Count(p => membership.TryGetValue(p.Project.Id, out var id) && id == g.Id), false)));
        var choices = groups.Where(g => !g.IsAll).ToList();
        foreach (var item in Items)
            item.SetGroups(choices, membership.TryGetValue(item.Project.Id, out var id) ? id : null);
        Groups = groups;
        SelectedGroup = groups.FirstOrDefault(g => g.IsAll == selectedAll && g.Id == selectedId) ?? groups[0];
        ApplyFilter();
    }

    [RelayCommand]
    private Task NewGroupAsync() => EditGroupAsync(null, string.Empty);

    [RelayCommand]
    private Task RenameGroupAsync(ProjectGroupItemViewModel? group)
    {
        return group?.Id is { } id ? EditGroupAsync(id, group.Name) : Task.CompletedTask;
    }

    private async Task EditGroupAsync(Guid? groupId, string initialName)
    {
        if (!CanManageProjects) return;
        IsGroupBusy = true;
        try
        {
            var saved = await DialogHelper.EditProjectGroupAsync(
                _localization.GetString(groupId is null ? "Groups.New" : "Groups.Rename"), initialName,
                async name =>
                {
                    var result = await _groupService.SaveAsync(groupId, name);
                    return result.IsSuccess ? null
                        : _localization.GetString(result.Error!.Code, result.Error.Arguments.Cast<object>().ToArray());
                });
            if (!saved) return;
            await ReloadGroupsAsync();
            Feedback = _localization.GetString("Groups.Saved");
        }
        catch (Exception) { Feedback = _localization.GetString("GROUP_RELOAD_FAILED"); }
        finally { IsGroupBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteGroupAsync(ProjectGroupItemViewModel? group)
    {
        if (!CanManageProjects || group?.Id is not { } id) return;
        IsGroupBusy = true;
        try
        {
            if (!await DialogHelper.ConfirmAsync(_localization.GetString("Common.Confirm"),
                _localization.GetString("Groups.DeleteConfirm", group.Name), _localization.GetString("Common.Delete"))) return;
            var result = await _groupService.DeleteAsync(id);
            if (!result.IsSuccess) { Feedback = _localization.GetString(result.Error!.Code); return; }
            await ReloadGroupsAsync();
            Feedback = _localization.GetString("Groups.Deleted");
        }
        catch (Exception) { Feedback = _localization.GetString("GROUP_RELOAD_FAILED"); }
        finally { IsGroupBusy = false; }
    }

    public async Task SwitchProjectGroupAsync(ProjectItemViewModel item, ProjectGroupItemViewModel group)
    {
        if (item.IsAssigningGroup || group.Id == item.GroupId) return;
        if (!CanManageProjects) { item.RestoreGroup(); return; }
        IsGroupBusy = true;
        item.IsAssigningGroup = true;
        try
        {
            var result = await _groupService.AssignAsync(item.Project.Id, group.Id);
            if (!result.IsSuccess)
            {
                item.RestoreGroup();
                Feedback = _localization.GetString(result.Error!.Code);
                return;
            }
            item.GroupId = group.Id;
            ApplyFilter();
            await ReloadGroupsAsync();
            Feedback = _localization.GetString("Groups.Saved");
        }
        catch (Exception) { item.RestoreGroup(); Feedback = _localization.GetString("GROUP_RELOAD_FAILED"); }
        finally { item.IsAssigningGroup = false; IsGroupBusy = false; }
    }

    [RelayCommand]
    private async Task PullGroupAsync(ProjectGroupItemViewModel? group)
    {
        if (!CanManageProjects || group is null || group.Count == 0) return;
        IsTransferBusy = true;
        string message;
        try
        {
            if (!await DialogHelper.ConfirmAsync(_localization.GetString("Common.Confirm"),
                _localization.GetString("Groups.PullConfirm", group.Name, group.Count), _localization.GetString("Projects.Pull"))) return;
            GroupPullDetails = string.Empty;
            _transferCancellation = new CancellationTokenSource();
            var cancellation = _transferCancellation;
            var progress = new Progress<GroupPullProgress>(p =>
            {
                // 忽略取消或结束后仍排队的进度通知，避免覆盖最终汇总。
                if (_transferCancellation == cancellation)
                    OperationProgress = _localization.GetString("Groups.Progress", p.Index, p.Total, p.ProjectName);
            });
            var result = await _transferService.PullGroupAsync(group.Id, group.IsAll, progress, cancellation.Token);
            if (!result.IsSuccess) message = _localization.GetString(result.Error!.Code);
            else
            {
                var report = result.Value!;
                var summary = _localization.GetString(report.Cancelled ? "Groups.CancelledSummary" : "Groups.Summary",
                    report.Total, report.Succeeded, report.Failed, report.Unfinished);
                GroupPullDetails = string.Join(Environment.NewLine, report.Outcomes.Select(o =>
                    o.ProjectName + "：" + (o.Error is null ? _localization.GetString("Projects.PullSuccess")
                    : _localization.GetString(o.Error.Code, o.Error.Arguments.Cast<object>().ToArray()))));
                message = summary + Environment.NewLine + Environment.NewLine + GroupPullDetails;
            }
        }
        catch (Exception) { message = _localization.GetString("TRANSFER_FAILED"); }
        finally
        {
            _transferCancellation?.Dispose();
            _transferCancellation = null;
            IsTransferBusy = false;
            OperationProgress = string.Empty;
        }
        try { await LoadAsync(); }
        catch (Exception) { message += " " + _localization.GetString("Projects.TransferReloadFailed"); }
        Feedback = message;
    }
}

public sealed record ProjectGroupItemViewModel(Guid? Id, string Name, int Count, bool IsAll)
{
    public bool CanEdit => Id.HasValue;
    public bool CanPull => Count > 0;
}

public partial class ProjectItemViewModel
{
    public Guid? GroupId { get; internal set; }
    [ObservableProperty] private IReadOnlyList<ProjectGroupItemViewModel> _groupChoices = [];
    [ObservableProperty] private ProjectGroupItemViewModel? _selectedProjectGroup;
    [ObservableProperty] private bool _isAssigningGroup;

    internal void SetGroups(IReadOnlyList<ProjectGroupItemViewModel> choices, Guid? groupId)
    {
        var wasAssigning = IsAssigningGroup;
        IsAssigningGroup = true;
        try { GroupId = groupId; GroupChoices = choices; RestoreGroup(); }
        finally { IsAssigningGroup = wasAssigning; }
    }

    internal void RestoreGroup() => SelectedProjectGroup = GroupChoices.FirstOrDefault(g => g.Id == GroupId);
}
