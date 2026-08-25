# 数据库初始化说明

`001_initial_schema.sql` 用于新环境创建 GitBinder 所需的数据表，包括：

- `platforms`       平台目录（用户可维护平台名称与 Host，支持启用/禁用）
- `accounts`        账号（Commit Identity + Authentication Identity 元数据，含 platform_id/platform_name）
- `projects`        项目（本机 Git 仓库登记）
- `bindings`        绑定（Project → Account 一对一）
- `repository_snapshots`  仓库配置快照（用于回滚）
- `settings`        设置（键值存储，含 language、global_mode、platforms.seeded）
- `operation_logs`  操作日志（脱敏审计）

增量迁移：

- `002_platform_refactor.sql`  平台目录引入与 accounts 平台字段重构（旧库迁移参考）

## 关键约定

- 数据库为 SQLite，文件位于 `%LOCALAPPDATA%\GitBinder\gitbinder.db`。
- 应用启动时由 `GitBinder.Infrastructure.Persistence.Schema` 幂等建表，本 SQL 文件是与代码保持同步的**权威表结构参考**。
- 修改 `Schema.cs` 时，务必同步更新本目录下的 `001_initial_schema.sql`。
- **Secret（Password / Token / SSH Passphrase）不落入本库**，数据库仅保存 `*_secret_id` 引用，明文由 DPAPI 加密后存放于 `secrets\` 目录。

## 已建索引

- `idx_projects_canonical_path`：按规范化路径快速查找项目。
- `idx_bindings_account_id`：按账号快速统计绑定数量。

## 变更规范

- 新增表：在 `001_initial_schema.sql` 中追加 `CREATE TABLE IF NOT EXISTS`。
- 修改已有表结构：更新 `001_initial_schema.sql`，并对已有环境追加对应的增量 SQL（新文件，如 `002_xxx.sql`）。
- 执行顺序按文件编号递增。
