using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using GitBinder.Desktop.Views;
using GitBinder.Domain.Common;

namespace GitBinder.UiSmoke;

public sealed partial class SmokeApp
{
    // 截图模式仅打开/关闭空表单，不保存、不调用传输，也不创建 clone-sandbox。
    private async Task CaptureModalPreviewsAsync(string culture, int ownerWidth)
    {
        _fixture.Main.SelectedNav = _fixture.Main.NavigationItems.Single(item => item.Key == "Nav.Platforms");
        await SettleAsync();
        var platformTask = _fixture.Platforms.NewCommand.ExecuteAsync(null);
        await SettleAsync();
        var platform = _desktop.Windows.OfType<PlatformEditDialog>().Single();
        if (ownerWidth == 960)
        {
            platform.SizeToContent = SizeToContent.Manual;
            platform.Width = Math.Max(440, platform.MinWidth);
            platform.Height = platform.MinHeight;
        }
        await SettleAsync();
        Check(platform.IsVisible, $"平台新增真实弹窗 {culture} {ownerWidth}");
        CheckDialogChrome(platform, $"平台新增 {culture} {ownerWidth}");
        CheckInWindow(platform, "SaveButton", $"平台保存按钮可见 {culture} {ownerWidth}");
        CheckInWindow(platform, "NameInput", $"平台名称输入可见 {culture} {ownerWidth}");
        CheckNoReadOnlyInputs(platform, $"平台说明与错误不用只读输入框 {culture} {ownerWidth}");
        Capture(platform, $"{culture}-{ownerWidth}-PlatformEditor");
        Click(platform.FindControl<Button>("CancelButton")!);
        await platformTask.WaitAsync(TimeSpan.FromSeconds(5));

        _fixture.Main.SelectedNav = _fixture.Main.NavigationItems.Single(item => item.Key == "Nav.Projects");
        await SettleAsync();
        CheckCloneEntry(culture, ownerWidth);
        var cloneTask = _fixture.Projects.OpenCloneCommand.ExecuteAsync(null);
        await SettleAsync();
        var clone = _desktop.Windows.OfType<CloneRepositoryDialog>().Single();
        if (ownerWidth == 960)
        {
            clone.SizeToContent = SizeToContent.Manual;
            clone.Width = Math.Max(520, clone.MinWidth);
            clone.Height = Math.Max(480, clone.MinHeight);
        }
        await SettleAsync();
        Check(clone.IsVisible, $"克隆仓库真实弹窗 {culture} {ownerWidth}");
        CheckDialogChrome(clone, $"克隆仓库 {culture} {ownerWidth}");
        CheckInWindow(clone, "StartCloneButton", $"克隆执行按钮可见 {culture} {ownerWidth}");
        CheckInWindow(clone, "CloneAccountInput", $"克隆账号输入可见 {culture} {ownerWidth}");
        CheckNoReadOnlyInputs(clone, $"克隆说明与错误不用只读输入框 {culture} {ownerWidth}");
        Capture(clone, $"{culture}-{ownerWidth}-CloneRepository");
        Click(clone.FindControl<Button>("CancelButton")!);
        await cloneTask.WaitAsync(TimeSpan.FromSeconds(5));
        Check(!_fixture.Projects.IsCloneEditorOpen, $"关闭克隆预览后恢复页面状态 {culture} {ownerWidth}");
    }

    private async Task VerifyPlatformEditorAsync()
    {
        _fixture.Main.SelectedNav = _fixture.Main.NavigationItems.Single(item => item.Key == "Nav.Platforms");
        await SettleAsync();
        var originalCount = _fixture.Platforms.Items.Count;
        var task = _fixture.Platforms.NewCommand.ExecuteAsync(null);
        await SettleAsync();
        var dialog = _desktop.Windows.OfType<PlatformEditDialog>().Single();
        Check(dialog.Owner == _window, "平台新增有正确模态父窗口");
        Click(dialog.FindControl<Button>("SaveButton")!);
        await SettleAsync();
        Check(dialog.IsVisible && dialog.FindControl<Border>("ErrorPanel")!.IsVisible,
            "平台空名称校验留在弹窗");
        Check((await _fixture.PlatformRepository.GetAllAsync()).Count == originalCount,
            "平台空名称未写入仓储");
        CheckSelectableError(dialog, "平台错误正文可全选复制");
        Capture(dialog, "platform-validation");

        dialog.FindControl<TextBox>("NameInput")!.Text = "隔离验证平台";
        dialog.FindControl<TextBox>("HostInput")!.Text = "git.example.test";
        _fixture.PlatformRepository.FailNextWrite = true;
        Click(dialog.FindControl<Button>("SaveButton")!);
        await SettleAsync();
        Check(dialog.IsVisible && dialog.FindControl<Border>("ErrorPanel")!.IsVisible,
            "平台保存异常留窗并显示错误");
        Check(dialog.FindControl<TextBox>("NameInput")!.Text == "隔离验证平台",
            "平台保存失败保留已填写字段");
        Check((await _fixture.PlatformRepository.GetAllAsync()).Count == originalCount,
            "平台模拟写入异常没有保存条目");

        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _fixture.PlatformRepository.WriteGate = release.Task;
        var writes = _fixture.PlatformRepository.WriteAttempts;
        Click(dialog.FindControl<Button>("SaveButton")!);
        await SettleAsync();
        Check(!dialog.FindControl<Button>("SaveButton")!.IsEnabled
            && !dialog.FindControl<Button>("CancelButton")!.IsEnabled
            && !dialog.FindControl<TextBox>("NameInput")!.IsEnabled,
            "平台写入进行中冻结输入及按钮");
        dialog.Close();
        Check(dialog.IsVisible, "平台写入进行中阻止窗口关闭");
        PressEscape(dialog);
        Check(dialog.IsVisible, "平台写入进行中 Escape 不误关窗口");
        Check(_fixture.PlatformRepository.WriteAttempts == writes + 1, "平台进行中未重复写入");
        release.SetResult();
        await task.WaitAsync(TimeSpan.FromSeconds(5));
        _fixture.PlatformRepository.WriteGate = null;
        Check(_fixture.Platforms.Items.Count == originalCount + 1, "平台保存成功关闭并刷新页面");
        var added = _fixture.Platforms.Items.Single(item => item.Name == "隔离验证平台");
        Check(added.Host == "git.example.test", "平台弹窗输入正确保存 Host");

        task = _fixture.Platforms.StartEditCommand.ExecuteAsync(added);
        await SettleAsync();
        dialog = _desktop.Windows.OfType<PlatformEditDialog>().Single();
        Check(dialog.FindControl<TextBox>("NameInput")!.Text == added.Name
            && dialog.FindControl<TextBox>("HostInput")!.Text == added.Host, "平台编辑弹窗正确回填");
        dialog.FindControl<TextBox>("NameInput")!.Text = "不应保存的修改";
        PressEscape(dialog.FindControl<TextBox>("NameInput")!);
        await task.WaitAsync(TimeSpan.FromSeconds(5));
        Check(_fixture.Platforms.Items.Any(item => item.Name == "隔离验证平台"), "Escape 取消平台编辑不改数据");

        task = _fixture.Platforms.NewCommand.ExecuteAsync(null);
        await SettleAsync();
        dialog = _desktop.Windows.OfType<PlatformEditDialog>().Single();
        dialog.FindControl<TextBox>("NameInput")!.Text = "取消新增";
        PressEscape(dialog.FindControl<TextBox>("NameInput")!);
        await task.WaitAsync(TimeSpan.FromSeconds(5));
        Check(_fixture.Platforms.Items.Count == originalCount + 1, "Escape取消平台新增不保存");
        Check(_window.IsVisible, "平台 Escape 不关闭父窗口");
    }

    private async Task VerifyCloneEditorAsync()
    {
        _fixture.Main.SelectedNav = _fixture.Main.NavigationItems.Single(item => item.Key == "Nav.Projects");
        await SettleAsync();
        var originalCount = _fixture.Projects.Items.Count;
        var originalCalls = _fixture.Transfer.CloneCount;
        var cancelled = _fixture.Projects.OpenCloneCommand.ExecuteAsync(null);
        await SettleAsync();
        var idleDialog = _desktop.Windows.OfType<CloneRepositoryDialog>().Single();
        idleDialog.FindControl<TextBox>("RemoteUrlInput")!.Text = "https://github.com/example/not-cloned.git";
        PressEscape(idleDialog.FindControl<TextBox>("RemoteUrlInput")!);
        await cancelled.WaitAsync(TimeSpan.FromSeconds(5));
        Check(!_fixture.Projects.IsCloneEditorOpen && _fixture.Projects.Items.Count == originalCount
            && _fixture.Transfer.CloneCount == originalCalls, "克隆输入框 Escape 不执行传输或登记");
        Check(_window.IsVisible, "克隆 Escape 不关闭父窗口");
        _fixture.Projects.CloneRemoteUrl = string.Empty;
        var opened = _fixture.Projects.OpenCloneCommand.ExecuteAsync(null);
        await SettleAsync();
        var dialog = _desktop.Windows.OfType<CloneRepositoryDialog>().Single();
        var vm = _fixture.Projects;
        Check(dialog.Owner == _window, "克隆仓库有正确模态父窗口");
        Check(ReferenceEquals(dialog.FindControl<Button>("StartCloneButton")!.Command, vm.CloneCommand)
            && ReferenceEquals(dialog.FindControl<Button>("BrowseDirectoryButton")!.Command, vm.BrowseCloneDirectoryCommand),
            "克隆执行及浏览按钮绑定正确命令（不打开系统目录选择器）");
        dialog.Width = 520;
        dialog.Height = 480;
        await SettleAsync();
        await vm.CloneCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(5));
        await SettleAsync();
        Check(dialog.IsVisible && dialog.FindControl<Border>("ErrorPanel")!.IsVisible,
            "克隆空地址校验留在弹窗");
        CheckErrorInScrollViewport(dialog, "窄尺寸克隆校验错误自动滚入可见视口");
        CheckInWindow(dialog, "StartCloneButton", "窄尺寸克隆错误显示后执行按钮仍可见");
        Check(_fixture.Transfer.CloneCount == 0, "克隆无效输入未进入传输层");
        CheckSelectableError(dialog, "克隆错误正文可全选复制");
        Capture(dialog, "clone-validation-narrow");
        dialog.Width = 620;
        dialog.Height = 560;
        await SettleAsync();

        var selectedAccount = vm.CloneAccounts.Last();
        dialog.FindControl<ComboBox>("CloneAccountInput")!.SelectedItem = selectedAccount;
        dialog.FindControl<TextBox>("RemoteUrlInput")!.Text = "https://github.com/example/isolated-clone.git";
        await SettleAsync();
        Check(vm.SelectedCloneAccount?.Id == selectedAccount.Id && vm.CloneFolderName == "isolated-clone",
            "克隆账号选择与地址输入绑定、自动目录名生效");
        var parent = Path.Combine(OutputDirectory, "clone-sandbox");
        Directory.CreateDirectory(parent);
        dialog.FindControl<TextBox>("ParentDirectoryInput")!.Text = parent;
        dialog.FindControl<TextBox>("FolderNameInput")!.Text = "manual-name";
        await SettleAsync();
        dialog.FindControl<TextBox>("RemoteUrlInput")!.Text = "https://github.com/example/changed.git";
        await SettleAsync();
        Check(vm.CloneFolderName == "manual-name", "改远程地址不覆盖手工目录名");

        _fixture.Transfer.OnClone = (_, _, _, _) => Task.FromResult(Result.Failure(new DomainError("CLONE_FAILED")));
        await vm.CloneCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(5));
        await SettleAsync();
        Check(dialog.IsVisible && vm.HasCloneFeedback && !vm.IsCloneBusy, "模拟克隆失败后留窗可重试");
        Check(vm.CloneFolderName == "manual-name" && _fixture.Transfer.UsedAccountId == selectedAccount.Id,
            "克隆失败保留输入并使用所选账号");
        Check(_fixture.Projects.Items.Count == originalCount, "克隆失败不登记项目");
        Capture(dialog, "clone-transfer-failed");

        var cancellationRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCancelledTransfer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _fixture.Transfer.OnClone = async (_, _, _, ct) =>
        {
            using var registration = ct.Register(() => cancellationRequested.TrySetResult());
            // 留出确定的收尾阶段，证明 Esc 只是请求取消而不是提前隐藏正在传输的窗口。
            await releaseCancelledTransfer.Task.WaitAsync(TimeSpan.FromSeconds(10));
            ct.ThrowIfCancellationRequested();
            return Result.Failure(new DomainError("CLONE_FAILED"));
        };
        var pending = vm.CloneCommand.ExecuteAsync(null);
        await SettleAsync();
        Check(vm.IsCloneBusy && !dialog.FindControl<Button>("StartCloneButton")!.IsEnabled
            && !dialog.FindControl<TextBox>("RemoteUrlInput")!.IsEffectivelyEnabled,
            "克隆进行中冻结提交与表单字段");
        var cloneCalls = _fixture.Transfer.CloneCount;
        await vm.CloneCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(5));
        Check(_fixture.Transfer.CloneCount == cloneCalls, "克隆进行中重复命令未重复传输");
        Capture(dialog, "clone-running");
        PressEscape(dialog);
        await cancellationRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Check(dialog.IsVisible && vm.IsCloneBusy && !pending.IsCompleted,
            "克隆进行中 Escape 请求取消，传输返回前窗口仍保留");
        dialog.Close();
        Check(dialog.IsVisible, "克隆取消收尾期间窗口关闭同样受保护");
        releaseCancelledTransfer.TrySetResult();
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await SettleAsync();
        Check(dialog.IsVisible && !vm.IsCloneBusy && vm.HasCloneFeedback,
            "运行中 Escape 取消传输后留窗显示结果");
        Check(_fixture.Projects.Items.Count == originalCount, "取消克隆不登记项目");
        Click(dialog.FindControl<Button>("CancelButton")!);
        await opened.WaitAsync(TimeSpan.FromSeconds(5));
        Check(!vm.IsCloneEditorOpen, "完成取消后可关闭克隆弹窗");

        opened = vm.OpenCloneCommand.ExecuteAsync(null);
        await SettleAsync();
        dialog = _desktop.Windows.OfType<CloneRepositoryDialog>().Single();
        dialog.FindControl<TextBox>("RemoteUrlInput")!.Text = "https://github.com/example/success.git";
        dialog.FindControl<TextBox>("ParentDirectoryInput")!.Text = parent;
        // 专属 artifacts 内的唯一空目录，仅供应用服务存在性检查；没有 .git 或源码。
        var folderName = "fake-success-" + Guid.NewGuid().ToString("N");
        dialog.FindControl<TextBox>("FolderNameInput")!.Text = folderName;
        _fixture.Transfer.OnClone = (_, destination, _, _) =>
        {
            Directory.CreateDirectory(destination);
            _fixture.Git.RepositoryRoot = destination;
            return Task.FromResult(Result.Success());
        };
        await vm.CloneCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(5));
        await opened.WaitAsync(TimeSpan.FromSeconds(5));
        await DismissNoticesAsync();
        Check(!vm.IsCloneEditorOpen && _fixture.Projects.Items.Count == originalCount + 1,
            "模拟克隆成功关闭弹窗并刷新项目列表");
        Check(_fixture.Projects.Items.Any(item => item.Name == folderName), "克隆登记项目显示在页面");
        Check(!Directory.EnumerateFiles(Path.Combine(parent, folderName), "*", SearchOption.AllDirectories).Any(),
            "模拟克隆只创建隔离空目录，没有真实 Git 或源码文件");
        _fixture.Transfer.OnClone = null;
    }

    private void CheckSelectableError(Window dialog, string message)
    {
        var error = dialog.FindControl<SelectableTextBlock>("ErrorText")!;
        error.SelectAll();
        Check(!string.IsNullOrWhiteSpace(error.Text) && error.SelectionEnd - error.SelectionStart == error.Text.Length,
            message + "（未写入系统剪贴板）");
    }

    private void CheckNoReadOnlyInputs(Window dialog, string message)
        => Check(!dialog.GetVisualDescendants().OfType<TextBox>().Any(input => input.IsReadOnly), message);

    private void CheckErrorInScrollViewport(Window dialog, string message)
    {
        // IsVisible 仅表示未显式隐藏，不能证明正文位于 ScrollViewer 的裁剪视口内。
        var error = dialog.FindControl<Border>("ErrorPanel")!;
        var scroll = error.GetVisualAncestors().OfType<ScrollViewer>().First();
        var presenter = error.GetVisualAncestors()
            .OfType<Avalonia.Controls.Presenters.ScrollContentPresenter>().First();
        var origin = error.TranslatePoint(new Point(0, 0), presenter);
        var viewportWidth = Math.Min(scroll.Viewport.Width, presenter.Bounds.Width);
        var viewportHeight = Math.Min(scroll.Viewport.Height, presenter.Bounds.Height);
        Check(error.IsVisible && error.GetVisualAncestors().All(ancestor => ancestor.IsVisible)
            && error.Bounds.Width > 0 && error.Bounds.Height > 0
            && origin is { X: >= -1, Y: >= -1 } point
            && point.X + error.Bounds.Width <= viewportWidth + 1
            && point.Y + error.Bounds.Height <= viewportHeight + 1, message);
    }

    private void CheckCloneEntry(string culture, int ownerWidth)
    {
        var projectView = _window.GetVisualDescendants().OfType<ProjectsView>().Single();
        var clone = projectView.FindControl<Button>("CloneRepositoryButton")!;
        // CreateCommand 同时用于页头和空态入口；只比较可见页头按钮，避免匹配到隐藏空态。
        var add = projectView.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains("project-entry")
                && ReferenceEquals(button.Command, _fixture.Projects.CreateCommand));
        var cloneIcon = clone.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single();
        var addIcon = add.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single();
        var expected = culture == "zh-CN" ? "克隆仓库" : "Clone Repository";
        Check(clone.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == expected),
            $"页头克隆仓库文案正确 {culture} {ownerWidth}");
        Check(Math.Abs(clone.Bounds.Height - add.Bounds.Height) < 1
            && clone.Bounds.Height is >= 36 and <= 40
            && Math.Abs(cloneIcon.Bounds.Height - addIcon.Bounds.Height) < 1
            && cloneIcon.Bounds.Height is >= 14 and <= 18,
            $"克隆与添加按钮同高、图标尺寸协调 {culture} {ownerWidth}");
    }

    private void CheckInWindow(Window dialog, string name, string message)
    {
        var control = dialog.FindControl<Control>(name)!;
        var origin = control.TranslatePoint(new Point(0, 0), dialog);
        Check(control.IsVisible && control.Bounds.Width > 0 && control.Bounds.Height > 0
            && origin is { X: >= 0, Y: >= 0 } point
            && point.X + control.Bounds.Width <= dialog.Bounds.Width + 1
            && point.Y + control.Bounds.Height <= dialog.Bounds.Height + 1, message);
    }
}
