using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GitBinder.Desktop;
using GitBinder.Desktop.ViewModels;
using GitBinder.Desktop.Views;

namespace GitBinder.UiSmoke;

public sealed partial class SmokeApp
{
    private void CheckDialogChrome(Window dialog, string description)
        => Check(!dialog.CanMinimize && !dialog.CanMaximize, description + "禁用最小化和最大化");

    // 只向隔离验证控件发送路由事件，不向操作系统注入按键。
    private static void PressEscape(InputElement target)
        => target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

    private async Task VerifyPageEscapeAsync()
    {
        var vm = _fixture.Projects;
        _fixture.Main.SelectedNav = _fixture.Main.NavigationItems.Single(item => item.Key == "Nav.Projects");
        vm.SearchText = string.Empty;
        vm.SelectedGroup = vm.Groups.Single(group => group.IsAll);
        await SettleAsync();
        var view = _window.GetVisualDescendants().OfType<ProjectsView>().Single();
        var item = vm.FilteredItems.First();
        var originalRemote = item.Project.OriginUrl;
        var originalPath = item.Project.RepositoryPath;
        vm.StartEditRemoteCommand.Execute(item);
        vm.StartEditPathCommand.Execute(item);
        await SettleAsync();
        var remote = InlineInput(view, item, "project-remote-editor");
        var path = InlineInput(view, item, "project-path-editor");
        remote.Text = "https://example.test/not-saved.git";
        path.Text = @"X:\UI-Smoke-Demo\not-saved";

        var groupPicker = view.GetVisualDescendants().OfType<ComboBox>().First(combo =>
            ReferenceEquals(combo.DataContext, item) && combo.Classes.Contains("project-group-choice"));
        groupPicker.IsDropDownOpen = true;
        await SettleAsync();
        Check(groupPicker.IsDropDownOpen, "项目分组下拉已打开供 Escape 优先级验证");
        PressEscape(groupPicker);
        await SettleAsync();
        Check(!groupPicker.IsDropDownOpen && item.IsRemoteEditorOpen && item.IsPathEditorOpen && _window.IsVisible,
            "首次 Escape 只收起下拉，不取消页内编辑或关闭主窗");

        Check(path.Focus(), "本地路径输入获得焦点");
        PressEscape(path);
        await SettleAsync();
        Check(!item.IsPathEditorOpen && item.IsRemoteEditorOpen && _window.IsVisible,
            "路径编辑 Escape 只取消焦点所在编辑区");
        Check(item.EditingPath == item.PathText, "路径 Escape 丢弃未保存输入");
        Check(remote.Focus(), "远程地址输入获得焦点");
        PressEscape(remote);
        await SettleAsync();
        Check(!item.IsRemoteEditorOpen && _window.IsVisible && item.EditingOriginUrl == item.OriginUrlDisplay,
            "地址编辑 Escape 丢弃未保存输入但不关闭主窗");
        Check(item.Project.OriginUrl == originalRemote && item.Project.RepositoryPath == originalPath,
            "页内编辑 Escape 不改项目地址及路径");

        vm.StartEditPathCommand.Execute(item);
        item.IsPathSaving = true;
        await SettleAsync();
        PressEscape(_window);
        Check(item.IsPathEditorOpen && _window.IsVisible, "路径保存期间 Escape 不误取消或关闭");
        item.IsPathSaving = false;
        PressEscape(_window);
        Check(!item.IsPathEditorOpen && _window.IsVisible, "路径保存状态解除后 Escape 可取消编辑");

        var busyStates = new (string Name, Action<bool> SetBusy)[]
        {
            ("传输", value => vm.IsTransferBusy = value),
            ("克隆收尾", value => vm.IsCloneBusy = value),
            ("分组操作", value => vm.IsGroupBusy = value),
            ("绑定操作", value => vm.IsBindingBusy = value),
        };
        foreach (var (name, setBusy) in busyStates)
        {
            setBusy(true);
            try
            {
                PressEscape(_window);
                Check(_window.IsVisible && !_fixture.Main.CanCloseWithEscape,
                    name + "期间 Escape 保留主窗口");
            }
            finally { setBusy(false); }
        }

        // Closing 的可取消回调模拟既有托盘接管点，不启动真实 TrayManager 或产品 App。
        var closeRequests = 0;
        void RetainFakeShell(object? sender, WindowClosingEventArgs args)
        {
            closeRequests++;
            args.Cancel = true;
        }
        _window.Closing += RetainFakeShell;
        try
        {
            foreach (var nav in _fixture.Main.NavigationItems)
            {
                _fixture.Main.SelectedNav = nav;
                await SettleAsync();
                var before = closeRequests;
                PressEscape(_window);
                Check(closeRequests == before + 1 && _window.IsVisible,
                    nav.Key + "空闲 Escape 经正常 Closing 入口且允许托盘接管");
            }
            await _fixture.Main.OpenSettingsCommand.ExecuteAsync(null);
            await SettleAsync();
            var beforeSettings = closeRequests;
            PressEscape(_window);
            Check(closeRequests == beforeSettings + 1 && _window.IsVisible,
                "设置页空闲 Escape 经相同 Closing 入口");
        }
        finally { _window.Closing -= RetainFakeShell; }

        Check(App.Services is null, "主窗 Escape 检查未建立真实服务容器或托盘");
        PressEscape(_window);
        await SettleAsync();
        Check(!_window.IsVisible, "无托盘接管时 Escape 关闭隔离主窗口");
    }

    private static TextBox InlineInput(ProjectsView view, ProjectItemViewModel item, string editorClass)
        => view.GetVisualDescendants().OfType<TextBox>().Single(input =>
            ReferenceEquals(input.DataContext, item) && input.GetVisualAncestors().OfType<Control>()
                .Any(control => control.Classes.Contains(editorClass)));
}
