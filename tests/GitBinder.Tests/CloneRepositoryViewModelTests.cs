using System.Globalization;
using GitBinder.Application.Bindings;
using GitBinder.Application.Common;
using GitBinder.Application.Git;
using GitBinder.Application.Projects;
using GitBinder.Desktop.ViewModels;
using GitBinder.Domain.Accounts;
using GitBinder.Domain.Common;
using GitBinder.Domain.Services;
using GitBinder.Platform.Windows;
using GitBinder.Tests.Fakes;

namespace GitBinder.Tests;

/// <summary>克隆表单回归仅使用内存后端与专属临时空目录，不打开真实窗口或执行 Git。</summary>
public sealed class CloneRepositoryViewModelTests
{
    [Fact]
    public async Task OpenClone_UsesSingleModal_AndCancelDoesNotTransfer()
    {
        using var f = await Fixture.CreateAsync();
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.ShowDialog = _ => closed.Task;
        f.ViewModel.CloneFeedback = "old error";

        var first = f.ViewModel.OpenCloneCommand.ExecuteAsync(null);
        await f.ViewModel.OpenCloneCommand.ExecuteAsync(null);

        Assert.True(f.ViewModel.IsCloneEditorOpen);
        Assert.Empty(f.ViewModel.CloneFeedback);
        Assert.Equal(1, f.DialogCount);
        closed.SetResult();
        await first;
        Assert.False(f.ViewModel.IsCloneEditorOpen);
        Assert.Equal(0, f.Transfer.CloneCount);
    }

    [Fact]
    public async Task MissingAccount_LeavesEditorOpenWithInlineError()
    {
        using var f = await Fixture.CreateAsync();
        f.ViewModel.IsCloneEditorOpen = true;
        f.ViewModel.SelectedCloneAccount = null;

        await f.ViewModel.CloneCommand.ExecuteAsync(null);

        Assert.True(f.ViewModel.IsCloneEditorOpen);
        Assert.Equal("TRANSFER_ACCOUNT_INVALID", f.ViewModel.CloneFeedback);
        Assert.True(f.ViewModel.HasCloneFeedback);
        Assert.Empty(f.ViewModel.Feedback);
        Assert.Equal(0, f.Transfer.CloneCount);
    }

    [Theory]
    [InlineData("not a remote", "new", "TRANSFER_URL_INVALID")]
    [InlineData("https://example.org/repo.git", "../escape", "CLONE_FOLDER_INVALID")]
    public async Task InvalidInput_PreservesValuesAndNeverCallsTransfer(string url, string folder, string error)
    {
        using var f = await Fixture.CreateAsync();
        f.ViewModel.IsCloneEditorOpen = true;
        f.ViewModel.CloneRemoteUrl = url;
        f.ViewModel.CloneFolderName = folder;

        await f.ViewModel.CloneCommand.ExecuteAsync(null);

        Assert.Equal(error, f.ViewModel.CloneFeedback);
        Assert.Equal(url, f.ViewModel.CloneRemoteUrl);
        Assert.Equal(folder, f.ViewModel.CloneFolderName);
        Assert.True(f.ViewModel.IsCloneEditorOpen);
        Assert.False(f.ViewModel.IsCloneBusy);
        Assert.Equal(0, f.Transfer.CloneCount);
    }

    [Fact]
    public async Task Failure_StaysOpenAndCanRetryWithoutChangingInputs()
    {
        using var f = await Fixture.CreateAsync();
        f.ViewModel.IsCloneEditorOpen = true;
        f.Transfer.Operation = (_, _, _) => Task.FromResult(Result.Failure(new DomainError("CLONE_FAILED")));

        await f.ViewModel.CloneCommand.ExecuteAsync(null);
        await f.ViewModel.CloneCommand.ExecuteAsync(null);

        Assert.True(f.ViewModel.IsCloneEditorOpen);
        Assert.Equal("CLONE_FAILED", f.ViewModel.CloneFeedback);
        Assert.Equal("https://example.org/repo.git", f.ViewModel.CloneRemoteUrl);
        Assert.Equal("new", f.ViewModel.CloneFolderName);
        Assert.Empty(f.ViewModel.Feedback);
        Assert.Equal(2, f.Transfer.CloneCount);
    }

    [Fact]
    public async Task RunningClone_BlocksDuplicateAndClose_CancelLeavesEditorAvailable()
    {
        using var f = await Fixture.CreateAsync();
        f.ViewModel.IsCloneEditorOpen = true;
        f.Transfer.Operation = async (_, _, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Result.Success();
        };

        var running = f.ViewModel.CloneCommand.ExecuteAsync(null);
        Assert.True(f.ViewModel.IsCloneBusy);
        Assert.False(f.ViewModel.CanManageProjects);
        await f.ViewModel.CloneCommand.ExecuteAsync(null);
        f.ViewModel.CloseCloneCommand.Execute(null);
        Assert.True(f.ViewModel.IsCloneEditorOpen);
        Assert.Equal(1, f.Transfer.CloneCount);
        f.ViewModel.CancelTransferCommand.Execute(null);
        await running;

        Assert.Equal("TRANSFER_CANCELLED", f.ViewModel.CloneFeedback);
        Assert.True(f.ViewModel.IsCloneEditorOpen);
        Assert.True(f.ViewModel.CanManageProjects);
        Assert.False(f.ViewModel.IsCloneBusy);
        Assert.False(f.ViewModel.IsTransferBusy);
        Assert.Empty(f.ViewModel.OperationProgress);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Success_ClosesRefreshesAndBindsSelected_WhileUsingEffectiveAccount(bool globalMode)
    {
        using var f = await Fixture.CreateAsync();
        var global = new Account { GitName = "global", Alias = "Global Account" };
        await f.Accounts.AddAsync(global);
        f.Global.IsEnabled = globalMode;
        f.Global.GlobalAccountId = global.Id;
        f.ViewModel.IsCloneEditorOpen = true;
        f.Transfer.Operation = (destination, _, _) =>
        {
            Directory.CreateDirectory(destination);
            f.Git.RepositoryRoot = destination;
            return Task.FromResult(Result.Success());
        };

        await f.ViewModel.CloneCommand.ExecuteAsync(null);

        Assert.False(f.ViewModel.IsCloneEditorOpen);
        Assert.False(f.ViewModel.IsCloneBusy);
        Assert.Empty(f.ViewModel.CloneRemoteUrl);
        Assert.Empty(f.ViewModel.CloneFolderName);
        Assert.Empty(f.ViewModel.CloneFeedback);
        Assert.Equal("Projects.CloneSuccess", f.ViewModel.Feedback);
        Assert.Equal(globalMode ? global.Id : f.Selected.Id, f.Transfer.UsedAccountId);
        var project = Assert.Single(f.ViewModel.Items);
        Assert.Equal(f.Selected.Id, project.BoundAccount!.Id);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "gitbinder-clone-vm-" + Guid.NewGuid().ToString("N"));
        public FakeAccountRepository Accounts { get; } = new();
        public FakeGitService Git { get; } = new() { OriginUrl = "https://example.org/repo.git" };
        public FakeGlobalModeState Global { get; } = new();
        public ControlledTransfer Transfer { get; } = new();
        public Account Selected { get; } = new() { GitName = "selected", IsDefault = true };
        public ProjectsViewModel ViewModel { get; private set; } = null!;
        public Func<ProjectsViewModel, Task> ShowDialog { get; set; } = _ => Task.CompletedTask;
        public int DialogCount { get; private set; }

        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            Directory.CreateDirectory(f._directory);
            var projects = new FakeProjectRepository();
            var bindings = new FakeBindingRepository();
            var groups = new FakeProjectGroupRepository();
            await f.Accounts.AddAsync(f.Selected);
            var resolver = new EffectiveAccountResolver(f.Global, bindings, f.Accounts);
            var bindingService = new BindingService(bindings, f.Accounts, projects, new FakeSecretStore(),
                f.Git, new FakeGitConfigApplier(), new FakeSnapshotRepository(), f.Transfer, resolver);
            var paths = new WindowsPathNormalizer();
            var projectService = new ProjectService(projects, f.Git, paths, f.Accounts, bindingService);
            var transferService = new ProjectTransferService(f.Transfer, projectService, projects,
                f.Accounts, bindingService, resolver, paths, groups);
            f.ViewModel = new ProjectsViewModel(projects, projectService, bindings, f.Accounts, bindingService,
                resolver, new TestLocalization(), () => Task.FromResult<string?>(null), transferService,
                new ProjectGroupService(groups), showCloneDialog: async vm =>
                {
                    f.DialogCount++;
                    await f.ShowDialog(vm);
                });
            await f.ViewModel.LoadAsync();
            f.ViewModel.CloneRemoteUrl = "https://example.org/repo.git";
            f.ViewModel.CloneFolderName = "new";
            f.ViewModel.CloneParentDirectory = f._directory;
            return f;
        }

        public void Dispose() => Directory.Delete(_directory, true);
    }

    private sealed class ControlledTransfer : IGitTransfer
    {
        public int CloneCount { get; private set; }
        public Guid UsedAccountId { get; private set; }
        public Func<string, Account, CancellationToken, Task<Result>> Operation { get; set; }
            = (_, _, _) => Task.FromResult(Result.Failure(new DomainError("CLONE_FAILED")));
        public Task<Result> CloneAsync(string remoteUrl, string destination, Account account, CancellationToken ct = default)
        {
            CloneCount++;
            UsedAccountId = account.Id;
            return Operation(destination, account, ct);
        }
        public Task<Result> TestAsync(string repositoryPath, Account account, CancellationToken ct = default)
            => throw new InvalidOperationException("Clone test must not run authentication tests.");
        public Task<Result> PullAsync(string repositoryPath, Account account, CancellationToken ct = default)
            => throw new InvalidOperationException("Clone test must not pull an existing repository.");
    }

    private sealed class TestLocalization : ILocalizationService
    {
        public CultureInfo CurrentCulture { get; private set; } = CultureInfo.InvariantCulture;
        public IReadOnlyList<CultureInfo> SupportedCultures { get; } = [CultureInfo.InvariantCulture];
        public event EventHandler? CultureChanged;
        public string GetString(string key) => key;
        public string GetString(string key, params object[] args) => key;
        public Task SetCultureAsync(CultureInfo culture, CancellationToken ct = default)
        {
            CurrentCulture = culture;
            CultureChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}
