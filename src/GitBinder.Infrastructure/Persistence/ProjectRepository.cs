using GitBinder.Application.Projects;
using GitBinder.Domain.Projects;
using Microsoft.Data.Sqlite;

namespace GitBinder.Infrastructure.Persistence;

/// <summary>
/// 基于 SQLite 的项目仓储实现。
/// </summary>
public sealed class ProjectRepository : IProjectRepository
{
    private readonly DatabaseContext _db;
    private const string Columns = "id, name, repository_path, canonical_path, git_dir, origin_url, remote_host, remote_protocol, current_branch, last_test_result, last_test_at, created_at, updated_at";

    public ProjectRepository(DatabaseContext db) => _db = db;

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM projects WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default)
    {
        var result = new List<Project>();
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM projects ORDER BY name ASC";

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(Map(reader));
        }

        return result;
    }

    public async Task<Project?> FindByPathAsync(string canonicalPath, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM projects WHERE canonical_path = $path LIMIT 1";
        command.Parameters.AddWithValue("$path", canonicalPath);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task AddAsync(Project project, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO projects (
                id, name, repository_path, canonical_path, git_dir,
                origin_url, remote_host, remote_protocol, current_branch,
                last_test_result, last_test_at, created_at, updated_at
            ) VALUES (
                $id, $name, $repository_path, $canonical_path, $git_dir,
                $origin_url, $remote_host, $remote_protocol, $current_branch,
                $last_test_result, $last_test_at, $created_at, $updated_at
            )
            """;
        AddParameters(command, project);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE projects SET
                name = $name,
                repository_path = $repository_path,
                canonical_path = $canonical_path,
                git_dir = $git_dir,
                origin_url = $origin_url,
                remote_host = $remote_host,
                remote_protocol = $remote_protocol,
                current_branch = $current_branch,
                last_test_result = $last_test_result,
                last_test_at = $last_test_at,
                updated_at = $updated_at
            WHERE id = $id
            """;
        AddParameters(command, project);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$id", id.ToString());
        command.CommandText = "DELETE FROM project_group_members WHERE project_id = $id";
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText = "DELETE FROM projects WHERE id = $id";
        await command.ExecuteNonQueryAsync(ct);
        transaction.Commit();
    }

    private static void AddParameters(SqliteCommand command, Project project)
    {
        command.Parameters.AddWithValue("$id", project.Id.ToString());
        command.Parameters.AddWithValue("$name", project.Name);
        command.Parameters.AddWithValue("$repository_path", project.RepositoryPath);
        command.Parameters.AddWithValue("$canonical_path", project.CanonicalPath);
        command.Parameters.AddWithValue("$git_dir", project.GitDir);
        command.Parameters.AddWithValue("$origin_url", project.OriginUrl);
        command.Parameters.AddWithValue("$remote_host", project.RemoteHost);
        command.Parameters.AddWithValue("$remote_protocol", (int)project.RemoteProtocol);
        command.Parameters.AddWithValue("$current_branch", project.CurrentBranch);
        command.Parameters.AddWithValue("$last_test_result", project.LastTestResult);
        command.Parameters.AddWithValue("$last_test_at", project.LastTestAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$created_at", project.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated_at", project.UpdatedAt.ToString("O"));
    }

    internal static Project Map(SqliteDataReader reader)
    {
        return new Project
        {
            Id = Guid.Parse(reader.GetString(0)),
            Name = reader.GetString(1),
            RepositoryPath = reader.GetString(2),
            CanonicalPath = reader.GetString(3),
            GitDir = reader.GetString(4),
            OriginUrl = reader.GetString(5),
            RemoteHost = reader.GetString(6),
            RemoteProtocol = (RemoteProtocol)reader.GetInt32(7),
            CurrentBranch = reader.GetString(8),
            LastTestResult = reader.GetString(9),
            LastTestAt = reader.IsDBNull(10) ? null : DateTimeOffset.Parse(reader.GetString(10)),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(11)),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(12)),
        };
    }
}
