using GitBinder.Application.Accounts;
using GitBinder.Desktop;
using GitBinder.Desktop.Localization;
using GitBinder.Desktop.ViewModels;
using GitBinder.Infrastructure;
using GitBinder.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace GitBinder.Tests;

public sealed class PlatformRepositoryMigrationTests
{
    [Fact]
    public async Task New_account_editor_loads_legacy_platform_schema_after_migration()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"gitbinder-platform-{Guid.NewGuid():N}.db");

        try
        {
            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE platforms (
                        id TEXT PRIMARY KEY,
                        name TEXT NOT NULL UNIQUE,
                        host TEXT NOT NULL DEFAULT '',
                        created_at TEXT NOT NULL,
                        updated_at TEXT NOT NULL,
                        enabled INTEGER NOT NULL DEFAULT 1
                    );
                    INSERT INTO platforms (id, name, host, created_at, updated_at, enabled)
                    VALUES ('00000000-0000-0000-0000-000000000001', 'GitHub', 'github.com',
                            '2026-08-23T08:13:28.4421916+00:00', '2026-08-23T08:13:28.4421920+00:00', 1);
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var context = new DatabaseContext(databasePath);
            new DatabaseInitializer(context).Initialize();

            var platforms = await new PlatformRepository(context).GetAllAsync();
            var platform = Assert.Single(platforms);

            Assert.Equal("GitHub", platform.Name);
            Assert.Equal("github.com", platform.Host);
            Assert.Equal(0, platform.SortOrder);
            Assert.True(platform.Enabled);
            Assert.Equal(new DateTimeOffset(2026, 8, 23, 8, 13, 28, TimeSpan.Zero).AddTicks(4_421_916), platform.CreatedAt);
            Assert.Equal(new DateTimeOffset(2026, 8, 23, 8, 13, 28, TimeSpan.Zero).AddTicks(4_421_920), platform.UpdatedAt);

            var editor = new AccountEditViewModel(
                accountService: null!,
                platformService: new PlatformService(new PlatformRepository(context), new SettingsRepository(context)),
                localization: new LocalizationService(),
                filePicker: () => Task.FromResult<string?>(null));
            await editor.LoadPlatformsAsync();
            editor.InitializeForCreate();

            var editorPlatform = Assert.Single(editor.Platforms);
            Assert.Equal("GitHub", editorPlatform.Name);
            Assert.Equal(editorPlatform.Id, editor.SelectedPlatform?.Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
