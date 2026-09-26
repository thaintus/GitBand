using GitBinder.Domain.Accounts;
using GitBinder.Domain.Projects;
using GitBinder.Domain.Services;
using GitBinder.Tests.Fakes;

namespace GitBinder.Tests;

public class EffectiveAccountResolverTests
{
    private static Account MakeAccount(string alias, bool isDefault = false, bool enabled = true)
        => new() { Alias = alias, Username = alias, IsDefault = isDefault, Enabled = enabled };

    [Fact]
    public void Resolve_GlobalModeEnabled_ReturnsGlobalAccount()
    {
        // Arrange
        var accountRepo = new FakeAccountRepository();
        var bindingRepo = new FakeBindingRepository();
        var defaultAccount = MakeAccount("default", isDefault: true);
        var globalAccount = MakeAccount("company-a");
        var boundAccount = MakeAccount("personal");
        accountRepo.AddAsync(defaultAccount).GetAwaiter().GetResult();
        accountRepo.AddAsync(globalAccount).GetAwaiter().GetResult();
        accountRepo.AddAsync(boundAccount).GetAwaiter().GetResult();

        var project = new Project { Id = Guid.NewGuid() };
        bindingRepo.AddAsync(new GitBinder.Domain.Bindings.Binding
        {
            ProjectId = project.Id,
            AccountId = boundAccount.Id,
        }).GetAwaiter().GetResult();

        var state = new FakeGlobalModeState { IsEnabled = true, GlobalAccountId = globalAccount.Id };
        var resolver = new EffectiveAccountResolver(state, bindingRepo, accountRepo);

        // Act
        var result = resolver.Resolve(project);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(globalAccount.Id, result!.Id);
    }

    [Fact]
    public void Resolve_NoGlobalMode_ReturnsBoundAccount()
    {
        var accountRepo = new FakeAccountRepository();
        var bindingRepo = new FakeBindingRepository();
        var defaultAccount = MakeAccount("default", isDefault: true);
        var boundAccount = MakeAccount("personal");
        accountRepo.AddAsync(defaultAccount).GetAwaiter().GetResult();
        accountRepo.AddAsync(boundAccount).GetAwaiter().GetResult();

        var project = new Project { Id = Guid.NewGuid() };
        bindingRepo.AddAsync(new GitBinder.Domain.Bindings.Binding
        {
            ProjectId = project.Id,
            AccountId = boundAccount.Id,
        }).GetAwaiter().GetResult();

        var resolver = new EffectiveAccountResolver(new FakeGlobalModeState { IsEnabled = false }, bindingRepo, accountRepo);

        var result = resolver.Resolve(project);

        Assert.NotNull(result);
        Assert.Equal(boundAccount.Id, result!.Id);
    }

    [Fact]
    public void Resolve_NoBinding_ReturnsDefaultAccount()
    {
        var accountRepo = new FakeAccountRepository();
        var bindingRepo = new FakeBindingRepository();
        var defaultAccount = MakeAccount("default", isDefault: true);
        accountRepo.AddAsync(defaultAccount).GetAwaiter().GetResult();

        var resolver = new EffectiveAccountResolver(new FakeGlobalModeState { IsEnabled = false }, bindingRepo, accountRepo);

        var result = resolver.Resolve(new Project { Id = Guid.NewGuid() });

        Assert.NotNull(result);
        Assert.Equal(defaultAccount.Id, result!.Id);
    }

    [Fact]
    public void ResolveReason_GlobalMode_ReturnsGlobalMode()
    {
        var accountRepo = new FakeAccountRepository();
        var bindingRepo = new FakeBindingRepository();
        var state = new FakeGlobalModeState { IsEnabled = true };
        var resolver = new EffectiveAccountResolver(state, bindingRepo, accountRepo);

        var reason = resolver.ResolveReason(new Project { Id = Guid.NewGuid() });

        Assert.Equal(EffectiveReason.GlobalMode, reason);
    }
}