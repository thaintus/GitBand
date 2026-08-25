using GitBinder.Application.Settings;
using Microsoft.Data.Sqlite;

namespace GitBinder.Infrastructure.Persistence;

/// <summary>
/// 基于 SQLite 的 settings 仓储实现。
/// </summary>
public sealed class SettingsRepository : ISettingsRepository
{
    private readonly DatabaseContext _db;

    public SettingsRepository(DatabaseContext db) => _db = db;

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key = $key LIMIT 1";
        command.Parameters.AddWithValue("$key", key);

        var result = await command.ExecuteScalarAsync(ct);
        return result?.ToString();
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings (key, value, updated_at)
            VALUES ($key, $value, $updated_at)
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                updated_at = excluded.updated_at
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM settings WHERE key = $key";
        command.Parameters.AddWithValue("$key", key);
        await command.ExecuteNonQueryAsync(ct);
    }
}