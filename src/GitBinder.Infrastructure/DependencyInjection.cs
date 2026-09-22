using GitBinder.Application.Accounts;
using GitBinder.Application.Bindings;
using GitBinder.Application.Common;
using GitBinder.Application.Git;
using GitBinder.Application.GlobalMode;
using GitBinder.Application.Projects;
using GitBinder.Application.Security;
using GitBinder.Application.Settings;
using GitBinder.Infrastructure.Common;
using GitBinder.Infrastructure.Git;
using GitBinder.Infrastructure.Persistence;
using GitBinder.Infrastructure.Ssh;
using Microsoft.Extensions.DependencyInjection;

namespace GitBinder.Infrastructure;

/// <summary>
/// Infrastructure 层依赖注入注册。
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string databasePath)
    {
        // 数据库。
        services.AddSingleton(new DatabaseContext(databasePath));
        services.AddSingleton<DatabaseInitializer>();

        // 仓储。
        services.AddSingleton<IAccountRepository, AccountRepository>();
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddSingleton<IProjectGroupRepository, ProjectGroupRepository>();
        services.AddSingleton<IBindingRepository, BindingRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IRepositorySnapshotRepository, RepositorySnapshotRepository>();
        services.AddSingleton<IPlatformRepository, PlatformRepository>();

        // Git。
        services.AddSingleton<IGitLocator, GitLocator>();
        services.AddSingleton<GitService>();
        services.AddSingleton<IGitTransfer, GitTransfer>();
        services.AddSingleton<IGitService>(sp => sp.GetRequiredService<GitService>());
        services.AddSingleton<GitConfigApplier>();
        services.AddSingleton<IGitConfigApplier>(sp => sp.GetRequiredService<GitConfigApplier>());
        services.AddSingleton<IGlobalGitConfigApplier>(sp => sp.GetRequiredService<GitConfigApplier>());

        // SSH。
        services.AddSingleton<ISshLocator, SshLocator>();

        // 通用。
        services.AddSingleton<ICommandExecutor>(new CommandExecutor());

        return services;
    }
}

/// <summary>
/// 负责在启动时初始化数据库 Schema 并做旧库迁移。
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly DatabaseContext _db;

    public DatabaseInitializer(DatabaseContext db) => _db = db;

    public void Initialize()
    {
        _db.EnsureCreated();
        MigrateAccountsPlatform();
        MigratePlatformsSortOrder();
        MigratePlatformsEnabled();
        MigrateProjectGroups();
        MigrateRepositoryEmailConfigSnapshot();
    }

    /// <summary>旧快照仅补可空邮箱 JSON 列，保留旧 user_email 及原始快照内容。</summary>
    private void MigrateRepositoryEmailConfigSnapshot()
    {
        using var connection = _db.OpenConnection();
        if (!TableExists(connection, "repository_snapshots") ||
            ColumnExists(connection, "repository_snapshots", "email_config_json"))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE repository_snapshots ADD COLUMN email_config_json TEXT NULL";
        command.ExecuteNonQuery();
    }

    /// <summary>分组表只增不改，兼容旧库且可重复执行。</summary>
    private void MigrateProjectGroups()
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS project_groups (id TEXT PRIMARY KEY, name TEXT NOT NULL COLLATE NOCASE UNIQUE);
            CREATE TABLE IF NOT EXISTS project_group_members (
                project_id TEXT PRIMARY KEY REFERENCES projects(id) ON DELETE CASCADE,
                group_id TEXT NOT NULL REFERENCES project_groups(id) ON DELETE CASCADE);
            CREATE INDEX IF NOT EXISTS idx_project_group_members_group_id ON project_group_members(group_id);
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 旧库迁移：早期 platforms 表没有 sort_order。
    /// 显式补列后，PlatformRepository 即可按稳定字段顺序读取。
    /// </summary>
    private void MigratePlatformsSortOrder()
    {
        using var connection = _db.OpenConnection();
        if (!TableExists(connection, "platforms") || ColumnExists(connection, "platforms", "sort_order"))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE platforms ADD COLUMN sort_order INTEGER NOT NULL DEFAULT 0";
        command.ExecuteNonQuery();
    }

    /// <summary>旧库迁移：platforms 表补 enabled 列。</summary>
    private void MigratePlatformsEnabled()
    {
        using var connection = _db.OpenConnection();
        if (!TableExists(connection, "platforms"))
        {
            return;
        }

        if (ColumnExists(connection, "platforms", "enabled"))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE platforms ADD COLUMN enabled INTEGER NOT NULL DEFAULT 1";
        command.ExecuteNonQuery();
    }

    private static bool TableExists(Microsoft.Data.Sqlite.SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $t";
        command.Parameters.AddWithValue("$t", table);
        var result = command.ExecuteScalar();
        return Convert.ToInt32(result) > 0;
    }

    /// <summary>旧库迁移：accounts.platform（整数枚举）→ platform_id/platform_name。</summary>
    private void MigrateAccountsPlatform()
    {
        using var connection = _db.OpenConnection();

        var hasOld = ColumnExists(connection, "accounts", "platform");
        var hasNew = ColumnExists(connection, "accounts", "platform_id");
        if (!hasOld || hasNew)
        {
            return; // 无需迁移。
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE accounts_new (
                id TEXT PRIMARY KEY,
                alias TEXT NOT NULL,
                platform_id TEXT NULL,
                platform_name TEXT NOT NULL DEFAULT '',
                host TEXT NOT NULL DEFAULT '',
                username TEXT NOT NULL DEFAULT '',
                git_name TEXT NOT NULL DEFAULT '',
                git_email TEXT NOT NULL DEFAULT '',
                auth_type INTEGER NOT NULL DEFAULT 0,
                ssh_private_key_path TEXT NOT NULL DEFAULT '',
                ssh_public_key_path TEXT NOT NULL DEFAULT '',
                passphrase_secret_id TEXT NOT NULL DEFAULT '',
                https_username TEXT NOT NULL DEFAULT '',
                https_secret_id TEXT NOT NULL DEFAULT '',
                is_default INTEGER NOT NULL DEFAULT 0,
                enabled INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            INSERT INTO accounts_new (
                id, alias, platform_id, platform_name, host, username, git_name, git_email,
                auth_type, ssh_private_key_path, ssh_public_key_path,
                passphrase_secret_id, https_username, https_secret_id,
                is_default, enabled, created_at, updated_at
            )
            SELECT id, alias, NULL,
                CASE platform
                    WHEN 0 THEN 'GitHub'
                    WHEN 1 THEN 'Gitee'
                    WHEN 2 THEN 'GitLab'
                    WHEN 3 THEN '阿里云云效 Codeup'
                    WHEN 4 THEN '腾讯云代码托管'
                    ELSE '自定义'
                END,
                host, username, git_name, git_email, auth_type,
                ssh_private_key_path, ssh_public_key_path, passphrase_secret_id,
                https_username, https_secret_id, is_default, enabled, created_at, updated_at
            FROM accounts;

            DROP TABLE accounts;
            ALTER TABLE accounts_new RENAME TO accounts;
            """;
        command.ExecuteNonQuery();
    }

    private static bool ColumnExists(Microsoft.Data.Sqlite.SqliteConnection connection, string table, string column)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $col";
        command.Parameters.AddWithValue("$col", column);
        var result = command.ExecuteScalar();
        return Convert.ToInt32(result) > 0;
    }
}
