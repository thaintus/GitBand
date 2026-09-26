using GitBinder.Application.Projects;
using GitBinder.Domain.Projects;
using GitBinder.Infrastructure;
using GitBinder.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace GitBinder.Tests;

public sealed class ProjectGroupRepositoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"gitbinder-groups-{Guid.NewGuid():N}.db");
    private readonly DatabaseContext _db;
    public ProjectGroupRepositoryTests()
    {
        _db = new DatabaseContext(_path);
        new DatabaseInitializer(_db).Initialize();
    }

    [Fact]
    public async Task Groups_PersistAcrossInitialization_DeletePreservesProjectAndBinding()
    {
        var projects = new ProjectRepository(_db);
        var groups = new ProjectGroupRepository(_db);
        var project = new Project { Name = "sample" };
        await projects.AddAsync(project);
        using (var connection = _db.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO bindings (id, project_id, account_id, applied_at) VALUES ($id, $project, $account, $at)";
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$project", project.Id.ToString());
            command.Parameters.AddWithValue("$account", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }
        var group = new ProjectGroup(Guid.NewGuid(), "Work");
        Assert.True((await groups.SaveAsync(group, true)).IsSuccess);
        Assert.True((await groups.AssignAsync(project.Id, group.Id)).IsSuccess);
        new DatabaseInitializer(_db).Initialize();
        Assert.Equal(group, Assert.Single(await new ProjectGroupRepository(_db).GetAllAsync()));
        Assert.Equal(group.Id, (await groups.GetMembershipsAsync())[project.Id]);
        Assert.True((await groups.DeleteAsync(group.Id)).IsSuccess);
        Assert.Empty(await groups.GetMembershipsAsync());
        Assert.NotNull(await projects.GetByIdAsync(project.Id));
        Assert.NotNull(await new BindingRepository(_db).GetByProjectIdAsync(project.Id));
    }

    [Fact]
    public async Task Groups_ValidateNamesTargetsAndCleanMembershipOnProjectRemoval()
    {
        var repository = new ProjectGroupRepository(_db);
        var service = new ProjectGroupService(repository);
        Assert.Equal("GROUP_NAME_INVALID", (await service.SaveAsync(null, " ")).Error!.Code);
        Assert.True((await service.SaveAsync(null, " Work ")).IsSuccess);
        Assert.Equal("GROUP_NAME_EXISTS", (await service.SaveAsync(null, "work")).Error!.Code);
        var group = Assert.Single(await repository.GetAllAsync());
        Assert.True((await service.SaveAsync(group.Id, "Renamed")).IsSuccess);
        Assert.Equal("Renamed", Assert.Single(await repository.GetAllAsync()).Name);
        Assert.Equal("PROJECT_NOT_FOUND", (await repository.AssignAsync(Guid.NewGuid(), group.Id)).Error!.Code);
        var project = new Project();
        var projects = new ProjectRepository(_db);
        await projects.AddAsync(project);
        Assert.Equal("GROUP_NOT_FOUND", (await repository.AssignAsync(project.Id, Guid.NewGuid())).Error!.Code);
        await repository.AssignAsync(project.Id, group.Id);
        await repository.AssignAsync(project.Id, null);
        Assert.Empty(await repository.GetMembershipsAsync());
        await repository.AssignAsync(project.Id, group.Id);
        await projects.DeleteAsync(project.Id);
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM project_group_members";
        Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
        Assert.Single(await repository.GetAllAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    [Fact]
    public async Task Migration_AddsGroupsToExistingDatabaseWithoutChangingProjects()
    {
        var projects = new ProjectRepository(_db);
        var project = new Project { Name = "existing", RepositoryPath = @"D:\existing" };
        await projects.AddAsync(project);
        // 仅在本用例的临时数据库中模拟旧版本：没有分组表。
        using (var connection = _db.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "DROP TABLE project_group_members; DROP TABLE project_groups;";
            await command.ExecuteNonQueryAsync();
        }
        new DatabaseInitializer(_db).Initialize();
        new DatabaseInitializer(_db).Initialize();
        var stored = Assert.Single(await projects.GetAllAsync());
        Assert.Equal(project.Id, stored.Id);
        Assert.Equal(project.RepositoryPath, stored.RepositoryPath);
        Assert.Empty(await new ProjectGroupRepository(_db).GetAllAsync());
        Assert.Empty(await new ProjectGroupRepository(_db).GetMembershipsAsync());
    }
}
