using GitBinder.Application.Projects;
using GitBinder.Domain.Common;
using GitBinder.Domain.Projects;
using Microsoft.Data.Sqlite;

namespace GitBinder.Infrastructure.Persistence;

public sealed class ProjectGroupRepository(DatabaseContext db) : IProjectGroupRepository
{
    public async Task<IReadOnlyList<ProjectGroup>> GetAllAsync(CancellationToken ct = default)
    {
        using var connection = db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM project_groups ORDER BY name COLLATE NOCASE, id";
        using var reader = await command.ExecuteReaderAsync(ct);
        var groups = new List<ProjectGroup>();
        while (await reader.ReadAsync(ct)) groups.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1)));
        return groups;
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetMembershipsAsync(CancellationToken ct = default)
    {
        using var connection = db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT m.project_id, m.group_id FROM project_group_members m JOIN projects p ON p.id = m.project_id JOIN project_groups g ON g.id = m.group_id";
        using var reader = await command.ExecuteReaderAsync(ct);
        var members = new Dictionary<Guid, Guid>();
        while (await reader.ReadAsync(ct)) members.Add(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)));
        return members;
    }

    public async Task<Result> SaveAsync(ProjectGroup group, bool create, CancellationToken ct = default)
    {
        using var connection = db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = create ? "INSERT INTO project_groups (id, name) VALUES ($id, $name)"
            : "UPDATE project_groups SET name = $name WHERE id = $id";
        command.Parameters.AddWithValue("$id", group.Id.ToString());
        command.Parameters.AddWithValue("$name", group.Name);
        try { return await command.ExecuteNonQueryAsync(ct) == 1 ? Result.Success() : Fail("GROUP_NOT_FOUND"); }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { return Fail("GROUP_NAME_EXISTS"); }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = db.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$id", id.ToString());
        // 显式清理归属，不依赖旧库的 foreign_keys 设置；绝不删除项目或绑定。
        command.CommandText = "DELETE FROM project_group_members WHERE group_id = $id";
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText = "DELETE FROM project_groups WHERE id = $id";
        var count = await command.ExecuteNonQueryAsync(ct);
        transaction.Commit();
        return count == 1 ? Result.Success() : Fail("GROUP_NOT_FOUND");
    }

    public async Task<Result> AssignAsync(Guid projectId, Guid? groupId, CancellationToken ct = default)
    {
        using var connection = db.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.CommandText = "SELECT COUNT(*) FROM projects WHERE id = $project";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 0) return Fail("PROJECT_NOT_FOUND");
        if (groupId is { } id)
        {
            command.Parameters.AddWithValue("$group", id.ToString());
            command.CommandText = "SELECT COUNT(*) FROM project_groups WHERE id = $group";
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 0) return Fail("GROUP_NOT_FOUND");
            command.CommandText = "INSERT INTO project_group_members (project_id, group_id) VALUES ($project, $group) ON CONFLICT(project_id) DO UPDATE SET group_id = excluded.group_id";
        }
        else command.CommandText = "DELETE FROM project_group_members WHERE project_id = $project";
        await command.ExecuteNonQueryAsync(ct);
        transaction.Commit();
        return Result.Success();
    }

    private static Result Fail(string code) => Result.Failure(new DomainError(code));
}
