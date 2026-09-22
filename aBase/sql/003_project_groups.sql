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
