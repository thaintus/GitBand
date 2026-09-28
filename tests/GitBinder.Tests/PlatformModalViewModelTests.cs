using System.Globalization;
using GitBinder.Application.Accounts;
using GitBinder.Application.Common;
using GitBinder.Desktop.ViewModels;
using GitBinder.Domain.Accounts;
using GitBinder.Tests.Fakes;

namespace GitBinder.Tests;

/// <summary>通过内存编辑器替身验证模态入口；真实窗口交互由 UiSmoke 验证。</summary>
public sealed class PlatformModalViewModelTests
{
    [Fact]
    public async Task New_SaveTrimsValuesAndRefreshesWithoutLosingSearch()
    {
        var repository = new PlatformRepository();
        var vm = Create(repository, async (title, name, host, save) =>
        {
            Assert.Equal("Platforms.Editor.New", title);
            Assert.Empty(name);
            Assert.Empty(host);
            Assert.Null(await save("  Company Git  ", "  git.example.invalid  "));
            return true;
        });
        vm.SearchText = "company";

        await vm.NewCommand.ExecuteAsync(null);

        var saved = Assert.Single(repository.Items.Values);
        Assert.Equal("Company Git", saved.Name);
        Assert.Equal("git.example.invalid", saved.Host);
        Assert.Equal(saved.Id, Assert.Single(vm.FilteredItems).Platform.Id);
        Assert.Equal("company", vm.SearchText);
        Assert.False(vm.IsEditorOpen);
        Assert.Empty(vm.Feedback);
    }

    [Fact]
    public async Task Edit_SavePreservesIdStatusAndSortOrder()
    {
        var repository = new PlatformRepository();
        var existing = new GitPlatform { Name = "Before", Host = "before.invalid", Enabled = false, SortOrder = 12 };
        repository.Items.Add(existing.Id, existing);
        var vm = Create(repository, async (title, name, host, save) =>
        {
            Assert.Equal("Platforms.Editor.Edit", title);
            Assert.Equal("Before", name);
            Assert.Equal("before.invalid", host);
            Assert.Null(await save("After", "after.invalid"));
            return true;
        });
        await vm.LoadAsync();

        await vm.StartEditCommand.ExecuteAsync(vm.Items.Single());

        var saved = Assert.Single(repository.Items.Values);
        Assert.Equal(existing.Id, saved.Id);
        Assert.Equal("After", saved.Name);
        Assert.Equal("after.invalid", saved.Host);
        Assert.False(saved.Enabled);
        Assert.Equal(12, saved.SortOrder);
        Assert.Equal("After", Assert.Single(vm.Items).Name);
    }

    [Fact]
    public async Task Cancel_DoesNotSaveOrReload()
    {
        var repository = new PlatformRepository();
        var existing = new GitPlatform { Name = "Original", Host = "original.invalid" };
        repository.Items.Add(existing.Id, existing);
        var vm = Create(repository, (_, _, _, _) => Task.FromResult(false));
        await vm.LoadAsync();
        var reads = repository.ReadCount;

        await vm.NewCommand.ExecuteAsync(null);
        await vm.StartEditCommand.ExecuteAsync(vm.Items.Single());

        Assert.Equal(0, repository.WriteCount);
        Assert.Equal(reads, repository.ReadCount);
        Assert.Equal("Original", Assert.Single(repository.Items.Values).Name);
        Assert.False(vm.IsEditorOpen);
    }

    [Fact]
    public async Task ValidationFailure_ReturnsLocalErrorAndAllowsRetryInSameDialog()
    {
        var repository = new PlatformRepository();
        var existing = new GitPlatform { Name = "Existing" };
        repository.Items.Add(existing.Id, existing);
        var vm = Create(repository, async (_, _, _, save) =>
        {
            Assert.Equal("PLATFORM_NAME_REQUIRED", await save("  ", ""));
            Assert.Equal("PLATFORM_NAME_EXISTS", await save("Existing", ""));
            Assert.Equal(0, repository.WriteCount);
            Assert.Null(await save("Valid", ""));
            return true;
        });

        await vm.NewCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Items.Count);
        Assert.Empty(vm.Feedback);
    }

    [Fact]
    public async Task StorageFailure_ReturnsSafeErrorAndAllowsRetry()
    {
        var repository = new PlatformRepository { FailWrite = true };
        var vm = Create(repository, async (_, _, _, save) =>
        {
            var error = await save("Company", "company.invalid");
            Assert.Equal("Platforms.Editor.SaveFailed", error);
            Assert.DoesNotContain("sensitive", error);
            Assert.Empty(repository.Items);
            repository.FailWrite = false;
            Assert.Null(await save("Company", "company.invalid"));
            return true;
        });

        var error = await Record.ExceptionAsync(() => vm.NewCommand.ExecuteAsync(null));

        Assert.Null(error);
        Assert.Single(repository.Items);
        Assert.Single(vm.Items);
        Assert.False(vm.IsEditorOpen);
    }

    [Fact]
    public async Task OpenFailure_IsHandledAndEditorCommandsRecover()
    {
        var vm = Create(new PlatformRepository(), (_, _, _, _) => throw new InvalidOperationException("sensitive"));

        var error = await Record.ExceptionAsync(() => vm.NewCommand.ExecuteAsync(null));

        Assert.Null(error);
        Assert.Equal("Platforms.Editor.OpenFailed", vm.Feedback);
        Assert.False(vm.IsEditorOpen);
        Assert.True(vm.NewCommand.CanExecute(null));
        Assert.True(vm.StartEditCommand.CanExecute(null));
    }

    [Fact]
    public async Task OpenEditor_BlocksDuplicateNewAndEditCommands()
    {
        var closed = new TaskCompletionSource<bool>();
        var calls = 0;
        var vm = Create(new PlatformRepository(), (_, _, _, _) =>
        {
            calls++;
            return closed.Task;
        });
        var pending = vm.NewCommand.ExecuteAsync(null);
        Assert.True(vm.IsEditorOpen);
        Assert.False(vm.NewCommand.CanExecute(null));
        Assert.False(vm.StartEditCommand.CanExecute(null));

        await vm.StartEditCommand.ExecuteAsync(new PlatformItemViewModel(new GitPlatform(), new TestLocalization()));
        Assert.Equal(1, calls);
        closed.SetResult(false);
        await pending;

        Assert.False(vm.IsEditorOpen);
        Assert.True(vm.NewCommand.CanExecute(null));
    }

    [Fact]
    public async Task SavedButRefreshFailed_DoesNotReportSaveFailureOrRepeatWrite()
    {
        var repository = new PlatformRepository();
        var vm = Create(repository, async (_, _, _, save) =>
        {
            Assert.Null(await save("Saved", "saved.invalid"));
            repository.FailRead = true;
            return true;
        });

        await vm.NewCommand.ExecuteAsync(null);

        Assert.Single(repository.Items);
        Assert.Equal(1, repository.WriteCount);
        Assert.Equal("Platforms.ReloadFailed", vm.Feedback);
        Assert.False(vm.IsEditorOpen);
    }

    private static PlatformsViewModel Create(PlatformRepository repository,
        Func<string, string, string, Func<string, string, Task<string?>>, Task<bool>> editor)
        => new(new PlatformService(repository, new FakeSettingsRepository()), new TestLocalization(), editor);

    private sealed class PlatformRepository : IPlatformRepository
    {
        public Dictionary<Guid, GitPlatform> Items { get; } = [];
        public bool FailWrite { get; set; }
        public bool FailRead { get; set; }
        public int WriteCount { get; private set; }
        public int ReadCount { get; private set; }

        public Task<GitPlatform?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.GetValueOrDefault(id));
        public Task<GitPlatform?> GetByNameAsync(string name, CancellationToken ct = default)
            => Task.FromResult(Items.Values.FirstOrDefault(p => p.Name == name));
        public Task<IReadOnlyList<GitPlatform>> GetAllAsync(CancellationToken ct = default)
        {
            ReadCount++;
            if (FailRead) throw new IOException("sensitive-storage-path");
            return Task.FromResult<IReadOnlyList<GitPlatform>>(Items.Values.ToList());
        }

        public Task AddAsync(GitPlatform platform, CancellationToken ct = default)
        {
            if (FailWrite) throw new IOException("sensitive-storage-path");
            WriteCount++;
            Items.Add(platform.Id, platform);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(GitPlatform platform, CancellationToken ct = default)
        {
            if (FailWrite) throw new IOException("sensitive-storage-path");
            WriteCount++;
            Items[platform.Id] = platform;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Items.Remove(id);
            return Task.CompletedTask;
        }
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
