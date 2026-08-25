using GitBinder.Application.GlobalMode;
using GitBinder.Domain.GlobalMode;
using Microsoft.Data.Sqlite;

namespace GitBinder.Infrastructure.Persistence;

/// <summary>
/// 基于 SQLite 的仓库快照仓储实现。
/// </summary>
public sealed class RepositorySnapshotRepository : IRepositorySnapshotRepository
{
    private readonly DatabaseContext _db;

    public RepositorySnapshotRepository(DatabaseContext db) => _db = db;

    public async Task<RepositorySnapshot?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM repository_snapshots WHERE project_id = $project_id LIMIT 1";
        command.Parameters.AddWithValue("$project_id", projectId.ToString());

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task SaveAsync(RepositorySnapshot snapshot, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO repository_snapshots (
                id, project_id, user_name, user_email, ssh_command,
                credential_helper, credential_use_http_path, captured_at
            ) VALUES (
                $id, $project_id, $user_name, $user_email, $ssh_command,
                $credential_helper, $credential_use_http_path, $captured_at
            )
            ON CONFLICT(project_id) DO UPDATE SET
                user_name = excluded.user_name,
                user_email = excluded.user_email,
                ssh_command = excluded.ssh_command,
                credential_helper = excluded.credential_helper,
                credential_use_http_path = excluded.credential_use_http_path,
                captured_at = excluded.captured_at
            """;
        command.Parameters.AddWithValue("$id", snapshot.Id.ToString());
        command.Parameters.AddWithValue("$project_id", snapshot.ProjectId.ToString());
        command.Parameters.AddWithValue("$user_name", snapshot.UserName);
        command.Parameters.AddWithValue("$user_email", snapshot.UserEmail);
        command.Parameters.AddWithValue("$ssh_command", snapshot.SshCommand);
        command.Parameters.AddWithValue("$credential_helper", snapshot.CredentialHelper);
        command.Parameters.AddWithValue("$credential_use_http_path", snapshot.CredentialUseHttpPath ? 1 : 0);
        command.Parameters.AddWithValue("$captured_at", snapshot.CapturedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteByProjectIdAsync(Guid projectId, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM repository_snapshots WHERE project_id = $project_id";
        command.Parameters.AddWithValue("$project_id", projectId.ToString());
        await command.ExecuteNonQueryAsync(ct);
    }

    private static RepositorySnapshot Map(SqliteDataReader reader)
    {
        return new RepositorySnapshot
        {
            Id = Guid.Parse(reader.GetString(0)),
            ProjectId = Guid.Parse(reader.GetString(1)),
            UserName = reader.GetString(2),
            UserEmail = reader.GetString(3),
            SshCommand = reader.GetString(4),
            CredentialHelper = reader.GetString(5),
            CredentialUseHttpPath = reader.GetInt32(6) == 1,
            CapturedAt = DateTimeOffset.Parse(reader.GetString(7)),
        };
    }
}