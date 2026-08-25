-- ============================================================================
-- GitBinder 增量迁移 002：平台目录与账号平台字段重构
-- 对应代码：src/GitBinder.Infrastructure/DependencyInjection.cs (DatabaseInitializer)
-- 说明：应用启动时 DatabaseInitializer 会自动执行等价迁移，本文件作为
--       可审计的增量 SQL 参考。已执行过自动迁移的环境无需重复执行。
-- ============================================================================

-- 1. 新增平台目录表（若不存在）。
CREATE TABLE IF NOT EXISTS platforms (
    id          TEXT PRIMARY KEY,
    name        TEXT NOT NULL UNIQUE,
    host        TEXT NOT NULL DEFAULT '',
    sort_order  INTEGER NOT NULL DEFAULT 0,
    enabled     INTEGER NOT NULL DEFAULT 1,
    created_at  TEXT NOT NULL,
    updated_at  TEXT NOT NULL
);

-- 2. platforms 表补 enabled 列（旧库）。
--    应用内迁移：ALTER TABLE platforms ADD COLUMN enabled INTEGER NOT NULL DEFAULT 1;

-- 3. accounts 表：platform（整数枚举）→ platform_id / platform_name。
--    应用内迁移通过 accounts_new 重建表完成，旧枚举值映射为平台名称：
--      0 GitHub / 1 Gitee / 2 GitLab / 3 阿里云云效 Codeup / 4 腾讯云代码托管 / 其他 自定义
--    参考 SQL（与 DatabaseInitializer.MigrateAccountsPlatform 等价）：
--      CREATE TABLE accounts_new (... platform_id TEXT NULL, platform_name TEXT NOT NULL DEFAULT '', ...);
--      INSERT INTO accounts_new SELECT id, alias, NULL, CASE platform ... END, ... FROM accounts;
--      DROP TABLE accounts;
--      ALTER TABLE accounts_new RENAME TO accounts;