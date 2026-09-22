namespace GitBinder.Infrastructure.Persistence;

/// <summary>
/// SQLite Schema 定义。
/// </summary>
internal static class Schema
{
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS platforms (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL UNIQUE,
            host TEXT NOT NULL DEFAULT '',
            sort_order INTEGER NOT NULL DEFAULT 0,
            enabled INTEGER NOT NULL DEFAULT 1,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS accounts (
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

        CREATE TABLE IF NOT EXISTS projects (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL DEFAULT '',
            repository_path TEXT NOT NULL DEFAULT '',
            canonical_path TEXT NOT NULL DEFAULT '',
            git_dir TEXT NOT NULL DEFAULT '',
            origin_url TEXT NOT NULL DEFAULT '',
            remote_host TEXT NOT NULL DEFAULT '',
            remote_protocol INTEGER NOT NULL DEFAULT 0,
            current_branch TEXT NOT NULL DEFAULT '',
            last_test_result TEXT NOT NULL DEFAULT '',
            last_test_at TEXT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS project_groups (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL COLLATE NOCASE UNIQUE
        );
        CREATE TABLE IF NOT EXISTS project_group_members (
            project_id TEXT PRIMARY KEY REFERENCES projects(id) ON DELETE CASCADE,
            group_id TEXT NOT NULL REFERENCES project_groups(id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS idx_project_group_members_group_id ON project_group_members(group_id);

        CREATE TABLE IF NOT EXISTS bindings (
            id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL UNIQUE,
            account_id TEXT NOT NULL,
            status INTEGER NOT NULL DEFAULT 0,
            applied_at TEXT NOT NULL,
            verified_at TEXT NULL,
            last_test_result TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS repository_snapshots (
            id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL UNIQUE,
            user_name TEXT NOT NULL DEFAULT '',
            user_email TEXT NOT NULL DEFAULT '',
            email_config_json TEXT NULL,
            ssh_command TEXT NOT NULL DEFAULT '',
            credential_helper TEXT NOT NULL DEFAULT '',
            credential_use_http_path INTEGER NOT NULL DEFAULT 0,
            captured_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS settings (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS operation_logs (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            operation TEXT NOT NULL,
            repository TEXT NOT NULL DEFAULT '',
            account_alias TEXT NOT NULL DEFAULT '',
            git_command_type TEXT NOT NULL DEFAULT '',
            result TEXT NOT NULL DEFAULT '',
            duration_ms INTEGER NOT NULL DEFAULT 0
        );

        CREATE INDEX IF NOT EXISTS idx_projects_canonical_path ON projects(canonical_path);
        CREATE INDEX IF NOT EXISTS idx_bindings_account_id ON bindings(account_id);
        """;
}
