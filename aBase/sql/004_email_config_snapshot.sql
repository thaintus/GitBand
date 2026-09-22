-- GitBinder 增量迁移 004：完整邮箱配置快照。
-- 对应 DatabaseInitializer.MigrateRepositoryEmailConfigSnapshot。
-- 应用启动时先检查 pragma_table_info('repository_snapshots')，仅在字段不存在时执行。
-- 本文件是增量 SQL 参考；已自动迁移的环境或按 001 新建的环境无需手动执行。
-- 不重建表，不更新旧行：既有 user_email 仍保留，新增列对旧行维持 SQL NULL。
ALTER TABLE repository_snapshots ADD COLUMN email_config_json TEXT NULL;

-- JSON 固定保存 UserEmail、AuthorEmail、CommitterEmail 三个字段。
-- 字段 null 表示原配置不存在，空字符串表示原配置显式留空；两者恢复方式不同。
-- SQL NULL 仅标记旧快照；损坏 JSON、缺字段、JSON null 均应阻止恢复并报错。
