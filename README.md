# GitBinder

> Git 仓库账号绑定管理工具 —— 纯本地、跨平台、可离线运行的 Git Identity & Credential Manager。

本地统一管理 Git 账号、仓库及账号绑定关系，解决多账号环境下提交身份错误、SSH Key 混用、HTTPS 凭据错配等痛点。

---

## 项目定位

GitBinder **不是**一个新的 Git GUI Client，而是一个以 **Repository → Account Binding** 为核心的 Git 身份与凭据管理工具。

核心竞争点不是 Commit / Diff / Branch / Merge，而是：

```text
Account（账号）
   +
Credential（凭据）
   +
Repository（仓库）
   +
Binding（绑定）
   +
Global Override（全局覆盖）
```

最终用户只需要维护：

```text
Accounts
├── Personal
├── Company-A
├── Company-B
└── Gitee

Repositories
├── Project-A → Personal
├── Project-B → Company-A
├── Project-C → Company-A
└── Project-D → Company-B
```

此后无论从 CMD、PowerShell、Git Bash、VS Code、IntelliJ IDEA 还是 Android Studio 执行 Git，只要其 Git executable 经过 GitBinder，即可自动获得正确的 Commit Identity 与 SSH/HTTPS Authentication Identity。

---

## 核心特性

| 能力 | 说明 |
|---|---|
| 账号管理 | Account CRUD、Alias、默认账号、提交身份（user.name / user.email）、SSH 私钥、HTTPS 凭据 |
| 项目管理 | 登记本机已有 Git 仓库、自动解析 Remote；移除仅删记录不删源码，路径变动后可重新添加 |
| 项目账号 | 在项目卡片内选择账号、保存绑定、解绑及测试远程认证 |
| Global Mode | 临时写入 Git 全局身份，使未登记仓库也生效；已绑定仓库同步覆盖本地身份，关闭后自动恢复原配置（不修改绑定数据） |
| 安全 | Secret 不明文落库、DPAPI 加密、参数数组防注入、日志脱敏 |
| 纯离线 | 无后端、无遥测、无自动更新、无 CDN，应用自身不访问互联网 |

### 有效账号优先级

```text
Global Mode Account  （最高）
        ↓
Project Binding Account
        ↓
Default Account
```

由统一的 `EffectiveAccountResolver` 决策，任何 Git 身份相关操作都不得自行实现账号选择逻辑。

---

## 技术栈

| 层级 | 技术 |
|---|---|
| 开发语言 | C# |
| Runtime | .NET 10 LTS |
| GUI | Avalonia 12 |
| UI 架构 | MVVM |
| MVVM Framework | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| 数据库 | SQLite（Microsoft.Data.Sqlite） |
| 日志 | Serilog |
| 机密存储（Windows） | DPAPI CurrentUser |
| Git | 系统 Git CLI |
| SSH | 系统 OpenSSH |

> 当前版本仅支持 **Windows**，界面支持**简体中文 / English** 切换（设置页可即时切换，重启后保留）。

---

## 解决方案结构

```text
GitBinder.slnx
│
├── src/
│   ├── GitBinder.Desktop/          # Avalonia 桌面应用（Views / ViewModels / Localization）
│   ├── GitBinder.Application/      # 应用服务层（用例编排、端口接口）
│   ├── GitBinder.Domain/           # 领域层（实体、EffectiveAccountResolver、错误模型）
│   ├── GitBinder.Infrastructure/   # 基础设施层（SQLite、Git CLI、SSH、日志）
│   ├── GitBinder.Platform/         # 平台适配层（Windows DPAPI、路径、Shell）
│   └── GitBinder.CredentialHelper/ # Git Credential Helper（输出 gitbinder-credential）
│
├── aBase/
│   ├── doc/       # 设计资料（GitBind设计文档.md、TECH_ARCHITECTURE.md、设计文档索引.md）
│   ├── iteration/ # 迭代记录（V1-首版开发.md）
│   ├── sql/       # 数据库表结构及变更 SQL（001/002）
│   └── trash/     # 逻辑删除回收站
│
├── build/         # 构建/打包脚本（build.ps1、installer.iss、publish.ps1）
├── tests/         # 单元测试（GitBinder.Tests）
└── 一键构建.bat    # 双击构建入口
```

### 分层职责

```text
Desktop (Avalonia View/ViewModel)
      ↓
Application (用例编排 + 定义接口端口)
      ↓
Domain (业务规则，平台无关)
      ↓
Infrastructure (Git/SSH/SQLite/日志 实现)
      ↓
Platform (操作系统差异适配)
```

约束：

- **Domain** 不依赖 Avalonia、SQLite 或任何平台 API；
- **Application** 不直接 `Process.Start`、不直接访问数据库或 Secret Store；
- **GUI** 不直接执行 git、不直接操作数据库；
- **Infrastructure** 负责 Git/SSH/持久化/日志/凭据；
- **Platform** 只负责操作系统差异。

---

## 快速开始

### 环境要求

- Windows 10/11
- [.NET 10 SDK](https://aka.ms/dotnet/download)（10.0.x）
- 系统 Git（`git.exe`）与 OpenSSH（`ssh.exe`）

### 一键构建（推荐）

双击 `一键构建.bat`，或：

```powershell
.\build\build.ps1                # 还原 + 构建 + 测试
.\build\build.ps1 -Package       # 构建 + 测试 + 生成安装包
```

### 手动构建

```powershell
dotnet build GitBinder.slnx -c Debug
dotnet test tests\GitBinder.Tests\GitBinder.Tests.csproj
```

### 运行

```powershell
dotnet run --project src\GitBinder.Desktop\GitBinder.Desktop.csproj
```

### 发布（Self-contained）

```powershell
dotnet publish src\GitBinder.Desktop\GitBinder.Desktop.csproj -c Release -r win-x64 --self-contained
```

### 生成安装包（可选安装位置）

项目使用 Inno Setup 制作 Windows 安装包，支持安装向导中选择安装目录、创建开始菜单/桌面快捷方式。

前置：安装 Inno Setup

```powershell
winget install JRSoftware.InnoSetup
```

一键打包（发布 + 编译安装包）：

```powershell
.\build\build-installer.ps1 -Version 1.0.0
```

产物输出到 `dist\GitBinder-1.0.0-setup.exe`。双击安装即可，安装向导支持选择安装目录与是否创建桌面快捷方式。

> 安装包默认语言为简体中文。首次打包若提示缺少 `ChineseSimplified.isl`，需手动下载该语言文件到 Inno Setup 的 `Languages\` 目录（打包脚本会在第一次运行时报出具体路径）。

---

## 数据存储位置

应用数据统一存放于（Windows）：

```text
%LOCALAPPDATA%\GitBinder\
├── gitbinder.db   # SQLite 数据库（仅存元数据与 Secret 引用）
├── secrets\       # DPAPI 加密后的凭据
├── keys\          # 导入的 SSH 私钥（受 ACL 限制）
└── logs\          # 滚动日志
```

> 数据库只保存 Credential 的 `SecretId` 引用，明文 Password/Token/Passphrase 永不入库。

---

## 文档索引

| 文档 | 说明 |
|---|---|
| `aBase/doc/GitBind设计文档.md` | 产品功能设计、业务模型、核心规则 |
| `aBase/doc/TECH_ARCHITECTURE.md` | 技术架构、分层、i18n、打包、安全设计 |
| `aBase/doc/设计文档索引.md` | 设计资料索引与已落地设计决策 |
| `aBase/iteration/V1-首版开发.md` | V1/V2 及后续迭代记录 |
| `aBase/sql/001_initial_schema.sql` | 数据库表结构（权威参考） |
| `aBase/sql/002_platform_refactor.sql` | 平台目录重构增量迁移 |

关于目录维护规范，详见 [`aBase/README.md`](aBase/README.md)。

---

## License

待定。
