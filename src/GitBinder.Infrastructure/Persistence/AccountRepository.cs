using GitBinder.Application.Accounts;
using GitBinder.Domain.Accounts;
using Microsoft.Data.Sqlite;

namespace GitBinder.Infrastructure.Persistence;

/// <summary>
/// 基于 SQLite 的账号仓储实现。
/// </summary>
public sealed class AccountRepository : IAccountRepository
{
    private readonly DatabaseContext _db;

    public AccountRepository(DatabaseContext db) => _db = db;

    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM accounts WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<Account?> GetDefaultAsync(CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM accounts WHERE is_default = 1 AND enabled = 1 LIMIT 1";

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken ct = default)
    {
        var result = new List<Account>();
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM accounts ORDER BY is_default DESC, alias ASC";

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(Map(reader));
        }

        return result;
    }

    public async Task AddAsync(Account account, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO accounts (
                id, alias, platform_id, platform_name, host, username, git_name, git_email,
                auth_type, ssh_private_key_path, ssh_public_key_path,
                passphrase_secret_id, https_username, https_secret_id,
                is_default, enabled, created_at, updated_at
            ) VALUES (
                $id, $alias, $platform_id, $platform_name, $host, $username, $git_name, $git_email,
                $auth_type, $ssh_private_key_path, $ssh_public_key_path,
                $passphrase_secret_id, $https_username, $https_secret_id,
                $is_default, $enabled, $created_at, $updated_at
            )
            """;
        AddParameters(command, account);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(Account account, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE accounts SET
                alias = $alias,
                platform_id = $platform_id,
                platform_name = $platform_name,
                host = $host,
                username = $username,
                git_name = $git_name,
                git_email = $git_email,
                auth_type = $auth_type,
                ssh_private_key_path = $ssh_private_key_path,
                ssh_public_key_path = $ssh_public_key_path,
                passphrase_secret_id = $passphrase_secret_id,
                https_username = $https_username,
                https_secret_id = $https_secret_id,
                is_default = $is_default,
                enabled = $enabled,
                updated_at = $updated_at
            WHERE id = $id
            """;
        AddParameters(command, account);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM accounts WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void AddParameters(SqliteCommand command, Account account)
    {
        command.Parameters.AddWithValue("$id", account.Id.ToString());
        command.Parameters.AddWithValue("$alias", account.Alias);
        command.Parameters.AddWithValue("$platform_id", account.PlatformId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$platform_name", account.PlatformName);
        command.Parameters.AddWithValue("$host", account.Host);
        command.Parameters.AddWithValue("$username", account.Username);
        command.Parameters.AddWithValue("$git_name", account.GitName);
        command.Parameters.AddWithValue("$git_email", account.GitEmail);
        command.Parameters.AddWithValue("$auth_type", (int)account.AuthenticationType);
        command.Parameters.AddWithValue("$ssh_private_key_path", account.SshPrivateKeyPath);
        command.Parameters.AddWithValue("$ssh_public_key_path", account.SshPublicKeyPath);
        command.Parameters.AddWithValue("$passphrase_secret_id", account.PassphraseSecretId);
        command.Parameters.AddWithValue("$https_username", account.HttpsUsername);
        command.Parameters.AddWithValue("$https_secret_id", account.HttpsSecretId);
        command.Parameters.AddWithValue("$is_default", account.IsDefault ? 1 : 0);
        command.Parameters.AddWithValue("$enabled", account.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("$created_at", account.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated_at", account.UpdatedAt.ToString("O"));
    }

    internal static Account Map(SqliteDataReader reader)
    {
        return new Account
        {
            Id = Guid.Parse(reader.GetString(0)),
            Alias = reader.GetString(1),
            PlatformId = reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
            PlatformName = reader.GetString(3),
            Host = reader.GetString(4),
            Username = reader.GetString(5),
            GitName = reader.GetString(6),
            GitEmail = reader.GetString(7),
            AuthenticationType = (AuthenticationType)reader.GetInt32(8),
            SshPrivateKeyPath = reader.GetString(9),
            SshPublicKeyPath = reader.GetString(10),
            PassphraseSecretId = reader.GetString(11),
            HttpsUsername = reader.GetString(12),
            HttpsSecretId = reader.GetString(13),
            IsDefault = reader.GetInt32(14) == 1,
            Enabled = reader.GetInt32(15) == 1,
            CreatedAt = DateTimeOffset.Parse(reader.GetString(16)),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(17)),
        };
    }
}