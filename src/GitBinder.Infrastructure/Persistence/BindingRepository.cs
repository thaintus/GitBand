using GitBinder.Application.Bindings;
using GitBinder.Domain.Bindings;
using Microsoft.Data.Sqlite;

namespace GitBinder.Infrastructure.Persistence;

/// <summary>
/// 基于 SQLite 的绑定仓储实现。
/// </summary>
public sealed class BindingRepository : IBindingRepository
{
    private readonly DatabaseContext _db;

    public BindingRepository(DatabaseContext db) => _db = db;

    public async Task<Binding?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM bindings WHERE project_id = $project_id LIMIT 1";
        command.Parameters.AddWithValue("$project_id", projectId.ToString());

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<IReadOnlyList<Binding>> GetAllAsync(CancellationToken ct = default)
    {
        var result = new List<Binding>();
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM bindings ORDER BY applied_at DESC";

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(Map(reader));
        }

        return result;
    }

    public async Task<IReadOnlyList<Binding>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        var result = new List<Binding>();
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM bindings WHERE account_id = $account_id";
        command.Parameters.AddWithValue("$account_id", accountId.ToString());

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(Map(reader));
        }

        return result;
    }

    public async Task<int> CountByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM bindings WHERE account_id = $account_id";
        command.Parameters.AddWithValue("$account_id", accountId.ToString());

        var result = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public async Task AddAsync(Binding binding, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO bindings (
                id, project_id, account_id, status, applied_at, verified_at, last_test_result
            ) VALUES (
                $id, $project_id, $account_id, $status, $applied_at, $verified_at, $last_test_result
            )
            """;
        AddParameters(command, binding);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(Binding binding, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE bindings SET
                account_id = $account_id,
                status = $status,
                applied_at = $applied_at,
                verified_at = $verified_at,
                last_test_result = $last_test_result
            WHERE id = $id
            """;
        AddParameters(command, binding);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteByProjectIdAsync(Guid projectId, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM bindings WHERE project_id = $project_id";
        command.Parameters.AddWithValue("$project_id", projectId.ToString());
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void AddParameters(SqliteCommand command, Binding binding)
    {
        command.Parameters.AddWithValue("$id", binding.Id.ToString());
        command.Parameters.AddWithValue("$project_id", binding.ProjectId.ToString());
        command.Parameters.AddWithValue("$account_id", binding.AccountId.ToString());
        command.Parameters.AddWithValue("$status", (int)binding.Status);
        command.Parameters.AddWithValue("$applied_at", binding.AppliedAt.ToString("O"));
        command.Parameters.AddWithValue("$verified_at", binding.VerifiedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$last_test_result", binding.LastTestResult);
    }

    internal static Binding Map(SqliteDataReader reader)
    {
        return new Binding
        {
            Id = Guid.Parse(reader.GetString(0)),
            ProjectId = Guid.Parse(reader.GetString(1)),
            AccountId = Guid.Parse(reader.GetString(2)),
            Status = (BindingStatus)reader.GetInt32(3),
            AppliedAt = DateTimeOffset.Parse(reader.GetString(4)),
            VerifiedAt = reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5)),
            LastTestResult = reader.GetString(6),
        };
    }
}