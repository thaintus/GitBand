-- ============================================================================
-- GitBinder 数据库初始化 Schema（V1）
-- 对应实现：src/GitBinder.Infrastructure/Persistence/Schema.cs
-- 数据库文件：%LOCALAPPDATA%\GitBinder\gitbinder.db（SQLite）
-- 说明：本文件仅用于新环境建表，SQLite 应用启动时通过 Schema.cs 幂等创建。
--       若修改 Schema.cs，请同步更新本文件。
-- ============================================================================

-- 平台目录表：用户可维护平台名称与 Host，支持启用/禁用。
-- 内置平台与自建平台地位相同，可自由编辑/删除/启用/禁用。
CREATE TABLE IF NOT EXISTS platforms (
    id          TEXT PRIMARY KEY,              -- GUID
    name        TEXT NOT NULL UNIQUE,          -- 平台显示名称
    host        TEXT NOT NULL DEFAULT '',      -- 平台主机，如 github.com
    sort_order  INTEGER NOT NULL DEFAULT 0,    -- 排序值
    enabled     INTEGER NOT NULL DEFAULT 1,    -- 是否启用（0 禁用 / 1 启用）
    created_at  TEXT NOT NULL,
    updated_at  TEXT NOT NULL
);

-- 账号表：同时承载 Commit Identity 与 Authentication Identity 元数据。
-- Secret（Password/Token/Passphrase）不落入本表，仅保存 SecretId 引用。
CREATE TABLE IF NOT EXISTS accounts (
    id                  TEXT PRIMARY KEY,              -- GUID
    alias               TEXT NOT NULL,                 -- 别名（未填时等于 username）
    platform_id         TEXT NULL,                     -- 所属平台目录 Id（可空）
    platform_name       TEXT NOT NULL DEFAULT '',      -- 平台显示名称（冗余，便于展示）
    host                TEXT NOT NULL DEFAULT '',      -- 托管主机，如 github.com
    username            TEXT NOT NULL DEFAULT '',      -- 平台登录用户名
    git_name            TEXT NOT NULL DEFAULT '',      -- Commit Identity: user.name
    git_email           TEXT NOT NULL DEFAULT '',      -- Commit Identity: user.email
    auth_type           INTEGER NOT NULL DEFAULT 0,    -- 认证方式：0 None 1 Ssh 2 Https 3 Both
    ssh_private_key_path TEXT NOT NULL DEFAULT '',     -- SSH 私钥路径（仅记录路径）
    ssh_public_key_path  TEXT NOT NULL DEFAULT '',     -- SSH 公钥路径（可选）
    passphrase_secret_id TEXT NOT NULL DEFAULT '',     -- SSH Passphrase 的 SecretId
    https_username      TEXT NOT NULL DEFAULT '',      -- HTTPS 用户名
    https_secret_id     TEXT NOT NULL DEFAULT '',      -- HTTPS Credential 的 SecretId
    is_default          INTEGER NOT NULL DEFAULT 0,    -- 是否默认账号（全表至多一个为 1）
    enabled             INTEGER NOT NULL DEFAULT 1,    -- 是否启用
    created_at          TEXT NOT NULL,                 -- ISO 8601
    updated_at          TEXT NOT NULL                  -- ISO 8601
);

-- 项目表：表示本机已存在的 Git Repository，只保存路径与 Git 元数据。
CREATE TABLE IF NOT EXISTS projects (
    id                  TEXT PRIMARY KEY,              -- GUID
    name                TEXT NOT NULL DEFAULT '',      -- 项目名（取仓库根目录名）
    repository_path     TEXT NOT NULL DEFAULT '',      -- 仓库根路径
    canonical_path      TEXT NOT NULL DEFAULT '',      -- 规范化绝对路径（用于匹配）
    git_dir             TEXT NOT NULL DEFAULT '',      -- .git 目录真实路径
    origin_url          TEXT NOT NULL DEFAULT '',      -- origin remote URL
    remote_host         TEXT NOT NULL DEFAULT '',      -- 解析出的 Remote Host
    remote_protocol     INTEGER NOT NULL DEFAULT 0,    -- 协议：0 Unknown 1 Ssh 2 Https 3 Http 4 File
    current_branch      TEXT NOT NULL DEFAULT '',      -- 当前分支
    last_test_result    TEXT NOT NULL DEFAULT '',      -- 最近一次测试结果
    last_test_at        TEXT NULL,                     -- 最近一次测试时间
    created_at          TEXT NOT NULL,
    updated_at          TEXT NOT NULL
);

-- 绑定表：Project → Account 一对一，project_id 唯一约束。
CREATE TABLE IF NOT EXISTS bindings (
    id                  TEXT PRIMARY KEY,              -- GUID
    project_id          TEXT NOT NULL UNIQUE,          -- 一个项目只能绑定一个账号
    account_id           TEXT NOT NULL,                -- 账号 ID
    status              INTEGER NOT NULL DEFAULT 0,    -- 0 Active 1 RepositoryMissing 2 SshKeyMissing 3 CredentialMissing 4 AccountDisabled 5 Drifted 6 Error
    applied_at          TEXT NOT NULL,                 -- 首次应用时间
    verified_at         TEXT NULL,                     -- 最近验证时间
    last_test_result    TEXT NOT NULL DEFAULT ''       -- 最近测试结果
);

-- 仓库快照表：修改 .git/config 前保存原始值，用于回滚。project_id 唯一。
CREATE TABLE IF NOT EXISTS repository_snapshots (
    id                      TEXT PRIMARY KEY,          -- GUID
    project_id              TEXT NOT NULL UNIQUE,
    user_name               TEXT NOT NULL DEFAULT '',  -- 原 user.name
    user_email              TEXT NOT NULL DEFAULT '',  -- 原 user.email
    email_config_json       TEXT NULL,                 -- 完整邮箱快照 JSON，NULL 表示旧快照；字段 null/空串分别表示未配置/显式留空
    ssh_command             TEXT NOT NULL DEFAULT '',  -- 原 core.sshCommand
    credential_helper       TEXT NOT NULL DEFAULT '',  -- 原 credential.helper
    credential_use_http_path INTEGER NOT NULL DEFAULT 0,
    captured_at             TEXT NOT NULL
);

-- 设置表：键值存储（global_mode、git 路径、语言等）。
CREATE TABLE IF NOT EXISTS settings (
    key         TEXT PRIMARY KEY,
    value       TEXT NOT NULL,
    updated_at  TEXT NOT NULL
);

-- 操作日志表：本地审计（脱敏后）。
CREATE TABLE IF NOT EXISTS operation_logs (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp       TEXT NOT NULL,
    operation       TEXT NOT NULL,
    repository      TEXT NOT NULL DEFAULT '',
    account_alias   TEXT NOT NULL DEFAULT '',
    git_command_type TEXT NOT NULL DEFAULT '',
    result          TEXT NOT NULL DEFAULT '',
    duration_ms     INTEGER NOT NULL DEFAULT 0
);

-- 索引
CREATE INDEX IF NOT EXISTS idx_projects_canonical_path ON projects(canonical_path);
CREATE INDEX IF NOT EXISTS idx_bindings_account_id ON bindings(account_id);

-- 项目分组：只新增表；已有项目默认为未分组，不改动路径、绑定或 Git 配置。
CREATE TABLE IF NOT EXISTS project_groups (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL COLLATE NOCASE UNIQUE
);
CREATE TABLE IF NOT EXISTS project_group_members (
    project_id TEXT PRIMARY KEY REFERENCES projects(id) ON DELETE CASCADE,
    group_id TEXT NOT NULL REFERENCES project_groups(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_project_group_members_group_id ON project_group_members(group_id);
