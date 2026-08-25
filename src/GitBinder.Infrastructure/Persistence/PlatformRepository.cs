using GitBinder.Application.Accounts;
using GitBinder.Domain.Accounts;
using Microsoft.Data.Sqlite;

namespace GitBinder.Infrastructure.Persistence;

/// <summary>
/// 基于 SQLite 的平台目录仓储实现。
/// </summary>
public sealed class PlatformRepository : IPlatformRepository
{
    // 永远显式指定列顺序，避免历史数据库通过 ALTER TABLE 补列后的物理列顺序影响映射。
    private const string SelectColumns = "id, name, host, sort_order, enabled, created_at, updated_at";

    private readonly DatabaseContext _db;

    public PlatformRepository(DatabaseContext db) => _db = db;

    public async Task<GitPlatform?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM platforms WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<GitPlatform?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM platforms WHERE name = $name LIMIT 1";
        command.Parameters.AddWithValue("$name", name);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<IReadOnlyList<GitPlatform>> GetAllAsync(CancellationToken ct = default)
    {
        var result = new List<GitPlatform>();
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM platforms ORDER BY sort_order ASC, name ASC";

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(Map(reader));
        }

        return result;
    }

    public async Task AddAsync(GitPlatform platform, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO platforms (id, name, host, sort_order, enabled, created_at, updated_at)
            VALUES ($id, $name, $host, $sort_order, $enabled, $created_at, $updated_at)
            """;
        AddParameters(command, platform);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(GitPlatform platform, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE platforms SET
                name = $name,
                host = $host,
                sort_order = $sort_order,
                enabled = $enabled,
                updated_at = $updated_at
            WHERE id = $id
            """;
        AddParameters(command, platform);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM platforms WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void AddParameters(SqliteCommand command, GitPlatform platform)
    {
        command.Parameters.AddWithValue("$id", platform.Id.ToString());
        command.Parameters.AddWithValue("$name", platform.Name);
        command.Parameters.AddWithValue("$host", platform.Host);
        command.Parameters.AddWithValue("$sort_order", platform.SortOrder);
        command.Parameters.AddWithValue("$enabled", platform.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("$created_at", platform.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated_at", platform.UpdatedAt.ToString("O"));
    }

    private static GitPlatform Map(SqliteDataReader reader)
    {
        return new GitPlatform
        {
            Id = Guid.Parse(reader.GetString(0)),
            Name = reader.GetString(1),
            Host = reader.GetString(2),
            SortOrder = reader.GetInt32(3),
            Enabled = reader.GetInt32(4) == 1,
            CreatedAt = DateTimeOffset.Parse(reader.GetString(5)),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(6)),
        };
    }
}
