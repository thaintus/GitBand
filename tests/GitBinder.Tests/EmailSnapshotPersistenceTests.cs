using System.Text.Json;
using GitBinder.Domain.GlobalMode;
using GitBinder.Infrastructure;
using GitBinder.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace GitBinder.Tests;

public sealed class EmailSnapshotPersistenceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"gitbinder-email-snapshots-{Guid.NewGuid():N}.db");
    private readonly DatabaseContext _db;

    public EmailSnapshotPersistenceTests() => _db = new DatabaseContext(_path);

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData("user@example.com", "author@example.com", "committer@example.com")]
    [InlineData(null, "", "committer@example.com")]
    [InlineData("", "author@example.com", null)]
    public async Task Save_RoundTripsMissingEmptyAndConfiguredEmailValues(
        string? userEmail, string? authorEmail, string? committerEmail)
    {
        new DatabaseInitializer(_db).Initialize();
        var repository = new RepositorySnapshotRepository(_db);
        var snapshot = CreateSnapshot();
        snapshot.EmailConfig = new GitEmailConfigSnapshot(userEmail, authorEmail, committerEmail);

        await repository.SaveAsync(snapshot);
        new DatabaseInitializer(_db).Initialize();

        var stored = Assert.IsType<RepositorySnapshot>(await repository.GetByProjectIdAsync(snapshot.ProjectId));
        AssertSnapshot(snapshot, stored);
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT email_config_json FROM repository_snapshots WHERE project_id = $project";
        command.Parameters.AddWithValue("$project", snapshot.ProjectId.ToString());
        var json = Assert.IsType<string>(await command.ExecuteScalarAsync());
        using var document = JsonDocument.Parse(json);
        AssertEmailJson(document.RootElement.GetProperty("UserEmail"), userEmail);
        AssertEmailJson(document.RootElement.GetProperty("AuthorEmail"), authorEmail);
        AssertEmailJson(document.RootElement.GetProperty("CommitterEmail"), committerEmail);
    }

    [Fact]
    public async Task Save_NullSnapshotUsesSqlNullAndUpdatesExistingRow()
    {
        new DatabaseInitializer(_db).Initialize();
        var repository = new RepositorySnapshotRepository(_db);
        var snapshot = CreateSnapshot();

        await repository.SaveAsync(snapshot);
        Assert.Null((await repository.GetByProjectIdAsync(snapshot.ProjectId))!.EmailConfig);
        snapshot.EmailConfig = new GitEmailConfigSnapshot(null, "", "committer@example.com");
        await repository.SaveAsync(snapshot);
        Assert.Equal(snapshot.EmailConfig, (await repository.GetByProjectIdAsync(snapshot.ProjectId))!.EmailConfig);
        snapshot.EmailConfig = null;
        await repository.SaveAsync(snapshot);

        var stored = Assert.IsType<RepositorySnapshot>(await repository.GetByProjectIdAsync(snapshot.ProjectId));
        AssertSnapshot(snapshot, stored);
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT email_config_json FROM repository_snapshots WHERE project_id = $project";
        command.Parameters.AddWithValue("$project", snapshot.ProjectId.ToString());
        Assert.Equal(DBNull.Value, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Initialize_UpgradesLegacyTableRepeatedlyAndPreservesOriginalSnapshot()
    {
        var snapshot = CreateSnapshot();
        using (var connection = _db.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            // 仅在本测试专有的临时数据库中建立没有邮箱 JSON 列的旧表。
            command.CommandText = """
                CREATE TABLE repository_snapshots (
                    id TEXT PRIMARY KEY,
                    project_id TEXT NOT NULL UNIQUE,
                    user_name TEXT NOT NULL DEFAULT '',
                    user_email TEXT NOT NULL DEFAULT '',
                    ssh_command TEXT NOT NULL DEFAULT '',
                    credential_helper TEXT NOT NULL DEFAULT '',
                    credential_use_http_path INTEGER NOT NULL DEFAULT 0,
                    captured_at TEXT NOT NULL
                );
                INSERT INTO repository_snapshots (
                    id, project_id, user_name, user_email, ssh_command,
                    credential_helper, credential_use_http_path, captured_at
                ) VALUES ($id, $project, $name, $email, $ssh, $helper, 1, $captured_at);
                """;
            command.Parameters.AddWithValue("$id", snapshot.Id.ToString());
            command.Parameters.AddWithValue("$project", snapshot.ProjectId.ToString());
            command.Parameters.AddWithValue("$name", snapshot.UserName);
            command.Parameters.AddWithValue("$email", snapshot.UserEmail);
            command.Parameters.AddWithValue("$ssh", snapshot.SshCommand);
            command.Parameters.AddWithValue("$helper", snapshot.CredentialHelper);
            command.Parameters.AddWithValue("$captured_at", snapshot.CapturedAt.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        var initializer = new DatabaseInitializer(_db);
        initializer.Initialize();
        initializer.Initialize();
        var repository = new RepositorySnapshotRepository(_db);
        var stored = Assert.IsType<RepositorySnapshot>(await repository.GetByProjectIdAsync(snapshot.ProjectId));
        AssertSnapshot(snapshot, stored);
        Assert.Null(stored.EmailConfig);

        using (var connection = _db.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('repository_snapshots') WHERE name = 'email_config_json'";
            Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
        }

        // 升级表新增列在末尾，新表列在 user_email 后，两个布局均须使用显式列映射。
        snapshot.EmailConfig = new GitEmailConfigSnapshot("", null, "committer@example.com");
        await repository.SaveAsync(snapshot);
        initializer.Initialize();
        AssertSnapshot(snapshot, Assert.IsType<RepositorySnapshot>(await repository.GetByProjectIdAsync(snapshot.ProjectId)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"UserEmail\":null,\"AuthorEmail\":null}")]
    [InlineData("{\"UserEmail\":123,\"AuthorEmail\":null,\"CommitterEmail\":null}")]
    public async Task Get_RejectsMalformedOrIncompleteJson(string json)
    {
        new DatabaseInitializer(_db).Initialize();
        var repository = new RepositorySnapshotRepository(_db);
        var snapshot = CreateSnapshot();
        await repository.SaveAsync(snapshot);
        using (var connection = _db.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE repository_snapshots SET email_config_json = $json WHERE project_id = $project";
            command.Parameters.AddWithValue("$json", json);
            command.Parameters.AddWithValue("$project", snapshot.ProjectId.ToString());
            await command.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<JsonException>(() => repository.GetByProjectIdAsync(snapshot.ProjectId));
    }

    private static RepositorySnapshot CreateSnapshot() => new()
    {
        ProjectId = Guid.NewGuid(),
        UserName = "original user",
        UserEmail = "legacy@example.com",
        SshCommand = "ssh -F original-config",
        CredentialHelper = "original-helper",
        CredentialUseHttpPath = true,
        CapturedAt = new DateTimeOffset(2026, 9, 22, 8, 0, 0, TimeSpan.Zero),
    };

    private static void AssertSnapshot(RepositorySnapshot expected, RepositorySnapshot actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.ProjectId, actual.ProjectId);
        Assert.Equal(expected.UserName, actual.UserName);
        Assert.Equal(expected.UserEmail, actual.UserEmail);
        Assert.Equal(expected.SshCommand, actual.SshCommand);
        Assert.Equal(expected.CredentialHelper, actual.CredentialHelper);
        Assert.Equal(expected.CredentialUseHttpPath, actual.CredentialUseHttpPath);
        Assert.Equal(expected.CapturedAt, actual.CapturedAt);
        Assert.Equal(expected.EmailConfig, actual.EmailConfig);
    }

    private static void AssertEmailJson(JsonElement element, string? expected)
    {
        Assert.Equal(expected is null ? JsonValueKind.Null : JsonValueKind.String, element.ValueKind);
        Assert.Equal(expected, element.GetString());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }
}
