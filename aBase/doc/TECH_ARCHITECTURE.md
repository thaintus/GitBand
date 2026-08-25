# GitBinder 技术架构设计

> 文档版本：v1.0  
> 适用范围：Windows / macOS / Ubuntu  
> 项目类型：跨平台本地桌面应用  
> 核心定位：Git 仓库与 Git 账号/Profile 的本地绑定管理工具  
> 技术路线：.NET 10 LTS + Avalonia 12 + MVVM + SQLite + System Git/OpenSSH

---

## 1. 架构目标

GitBinder 的目标是提供一个跨平台、纯本地、可离线运行的 Git 账号与仓库绑定管理工具。

核心能力包括：

- Git 账号/Profile 管理；
- Git 项目/仓库管理；
- Project → Account 一对一绑定；
- Git `user.name` / `user.email` 仓库级配置；
- SSH Private Key 仓库级绑定；
- HTTPS Credential 安全管理；
- 默认账号；
- Global Mode 全局覆盖模式；
- Git / SSH 配置检测；
- 用户主动触发的远程连接测试；
- Windows / macOS / Ubuntu 安装包构建；
- 完整 i18n 国际化架构；
- 不依赖任何云端服务。

---

# 2. 技术选型

## 2.1 核心技术栈

| 层级 | 技术 |
|---|---|
| 开发语言 | C# |
| Runtime | .NET 10 LTS |
| GUI | Avalonia 12 |
| UI 架构 | MVVM |
| MVVM Framework | CommunityToolkit.Mvvm |
| Dependency Injection | Microsoft.Extensions.DependencyInjection |
| Configuration | Microsoft.Extensions.Configuration |
| Database | SQLite |
| ORM / Data Access | EF Core 或 Dapper |
| Logging | Serilog |
| Git | 系统 Git CLI |
| SSH | 系统 OpenSSH |
| HTTPS Credential | 自定义 Git Credential Helper |
| Windows Secret Store | Windows Credential Manager / DPAPI |
| macOS Secret Store | Keychain |
| Linux Secret Store | Secret Service / libsecret |
| i18n | RESX / 自定义 Localization Service |
| Tests | xUnit |
| CI/CD | GitHub Actions 或自建多平台 Runner |

---

# 3. 为什么选择 Avalonia + .NET

本项目属于典型的桌面系统工具，核心功能涉及：

- 本地文件系统；
- Git CLI；
- SSH CLI；
- Git Config；
- SSH Key；
- 系统 Credential Store；
- 子进程调用；
- 路径处理；
- 跨平台安装包；
- 本地数据库；
- 系统托盘；
- 文件选择器；
- 权限及操作系统差异。

因此相比 Electron、Tauri 等方案，本项目优先选择：

```text
C#
+
.NET
+
Avalonia
```

优势：

1. UI、业务逻辑、基础设施全部使用 C#；
2. 无需维护 TypeScript ↔ Rust IPC；
3. 对系统 API、进程、文件系统、SQLite 集成更直接；
4. Windows / macOS / Linux 可共用绝大部分代码；
5. Avalonia 可以提供统一跨平台 GUI；
6. Domain/Application 层可以完全平台无关；
7. 适合持续使用 AI 编程工具进行维护和迭代。

---

# 4. 总体架构

```text
┌─────────────────────────────────────────────────────────┐
│                     GitBinder.Desktop                   │
│                                                         │
│        Avalonia Views / ViewModels / Components         │
└──────────────────────────┬──────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                  GitBinder.Application                  │
│                                                         │
│ Accounts / Projects / Bindings / GlobalMode / Testing   │
└──────────────────────────┬──────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                    GitBinder.Domain                     │
│                                                         │
│ Account / Project / Binding / EffectiveAccountResolver  │
└──────────────────────────┬──────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                GitBinder.Infrastructure                 │
│                                                         │
│ Git / SSH / SQLite / Credentials / Logging / Storage    │
└──────────────────────────┬──────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                  GitBinder.Platform                     │
│                                                         │
│        Windows        macOS             Linux           │
│        DPAPI          Keychain           SecretService  │
└─────────────────────────────────────────────────────────┘
```

核心原则：

> Domain 和 Application 层不得依赖 Windows、macOS、Linux 的任何具体实现。

---

# 5. Solution 结构

推荐目录：

```text
GitBinder/
│
├── GitBinder.sln
│
├── src/
│   │
│   ├── GitBinder.Desktop/
│   │   ├── App.axaml
│   │   ├── Program.cs
│   │   │
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── Controls/
│   │   ├── Dialogs/
│   │   ├── Converters/
│   │   ├── Assets/
│   │   └── Localization/
│   │
│   ├── GitBinder.Application/
│   │   ├── Accounts/
│   │   ├── Projects/
│   │   ├── Bindings/
│   │   ├── GlobalMode/
│   │   ├── Testing/
│   │   ├── Localization/
│   │   └── Common/
│   │
│   ├── GitBinder.Domain/
│   │   ├── Accounts/
│   │   ├── Projects/
│   │   ├── Bindings/
│   │   ├── Credentials/
│   │   ├── GlobalMode/
│   │   └── Services/
│   │
│   ├── GitBinder.Infrastructure/
│   │   ├── Git/
│   │   ├── Ssh/
│   │   ├── Persistence/
│   │   ├── Credentials/
│   │   ├── Logging/
│   │   └── Configuration/
│   │
│   ├── GitBinder.Platform/
│   │   ├── Common/
│   │   ├── Windows/
│   │   ├── MacOS/
│   │   └── Linux/
│   │
│   └── GitBinder.CredentialHelper/
│       └── Program.cs
│
├── tests/
│   ├── GitBinder.Domain.Tests/
│   ├── GitBinder.Application.Tests/
│   ├── GitBinder.Infrastructure.Tests/
│   └── GitBinder.IntegrationTests/
│
├── build/
│   ├── windows/
│   ├── macos/
│   └── linux/
│
├── docs/
│
└── .github/
    └── workflows/
```

---

# 6. 核心领域模型

## 6.1 Account

```text
Account
├── Id
├── Alias
├── Platform
├── Host
├── Username
├── GitName
├── GitEmail
├── AuthenticationType
├── SshPrivateKeyPath
├── SshPublicKeyPath
├── PassphraseSecretId
├── HttpsUsername
├── HttpsSecretId
├── IsDefault
├── Enabled
├── CreatedAt
└── UpdatedAt
```

账号对象同时描述：

```text
Commit Identity
+
Authentication Identity
```

Commit Identity：

```text
user.name
user.email
```

Authentication Identity：

```text
SSH Key
或
HTTPS Username + Token/Password
```

---

## 6.2 Project

```text
Project
├── Id
├── Name
├── RepositoryPath
├── CanonicalPath
├── GitDir
├── OriginUrl
├── RemoteHost
├── RemoteProtocol
├── CurrentBranch
├── CreatedAt
└── UpdatedAt
```

Project 仅表示：

> 本机已经存在的 Git Repository。

GitBinder 不复制 Repository 内容。

### Remote 协议切换

项目“修改地址”编辑区可在标准 Remote 地址间切换：`https://host/group/repo.git` 转为 `git@host:group/repo.git`，反向切换则生成 HTTPS 地址。切换只修改编辑框内容，用户仍须点击“保存地址”才会执行本地 `git remote set-url`。

带 HTTPS 用户信息、查询参数、片段或非默认端口的地址不自动转换，避免错误猜测 SSH/HTTPS 服务端口；此类地址由用户手动维护。

---

## 6.3 Binding

```text
Binding
├── Id
├── ProjectId
├── AccountId
├── Status
├── AppliedAt
├── VerifiedAt
└── LastTestResult
```

数据库必须约束：

```text
UNIQUE(project_id)
```

即：

> 一个 Project 只能绑定一个 Account。

一个 Account 可以绑定多个 Project。

---

# 7. Effective Account Resolver

任何 Git 身份相关操作都必须通过统一服务：

```text
EffectiveAccountResolver
```

规则：

```text
if GlobalMode.Enabled:
    return GlobalMode.Account

if Project.Binding exists:
    return Project.Binding.Account

return DefaultAccount
```

流程：

```text
             Global Mode?
                  │
          ┌───────┴───────┐
          │               │
         Yes              No
          │               │
          ▼               ▼
 Global Account     Project Binding?
                          │
                  ┌───────┴───────┐
                 Yes              No
                  │               │
                  ▼               ▼
           Bound Account    Default Account
```

禁止 ViewModel、GitService、SSHService 等模块自行实现账号选择逻辑。

---

# 8. Git 集成架构

## 8.1 使用系统 Git

V1 不使用 LibGit2Sharp / libgit2 作为核心 Git 引擎。

统一调用：

```text
git
```

理由：

- 与 Git CLI 行为一致；
- 与 IDEA / VS Code / SourceGit 等工具读取同一份 Git Config；
- 更容易验证配置是否真正生效；
- 避免重新实现 Git Credential / SSH 行为；
- 跨平台一致。

---

## 8.2 GitLocator

定义：

```csharp
public interface IGitLocator
{
    Task<string?> LocateAsync();
}
```

查找顺序：

1. 用户设置指定路径；
2. PATH；
3. 当前平台常见安装目录。

---

## 8.3 GitService

建议接口：

```text
IGitService
├── ValidateRepository
├── GetRepositoryRoot
├── GetRemotes
├── GetOrigin
├── GetCurrentBranch
├── ReadLocalIdentity
├── ApplyAccount
├── RestoreConfig
├── ReadEffectiveConfiguration
├── TestRemote
└── GetGitVersion
```

---

# 9. SSH 架构

## 9.1 使用系统 OpenSSH

优先调用：

```text
ssh
ssh-keygen
```

而不是自己实现 SSH 协议。

---

## 9.2 仓库级 SSH Key

SSH Repository 推荐将 Key 绑定写入：

```text
.git/config
```

通过：

```bash
git config --local core.sshCommand "ssh -i <key> -o IdentitiesOnly=yes"
```

例如：

```ini
[core]
    sshCommand = ssh -i "/Users/user/.ssh/company" -o IdentitiesOnly=yes
```

这样能够保证：

```text
Repo A → SSH Key A
Repo B → SSH Key B
```

即使两个仓库位于同一个 Host：

```text
github.com
```

也不会混用账号。

必须默认：

```text
IdentitiesOnly=yes
```

避免 ssh-agent 中其他 Key 被自动尝试。

当仓库选择的账号没有 SSH 私钥时，仍要写入使用 Windows 空配置/空身份文件（`-F NUL -i NUL`）且禁用 Agent、交互式认证的 `core.sshCommand`，而不是清除该项后回退到系统默认 Key。这样允许跨平台改绑，但远程认证只能使用当前选中账号提供的认证信息；未配置对应私钥时应认证失败。

---

# 10. HTTPS Credential 架构

HTTPS Account 包含：

```text
Username
Credential Secret
```

禁止：

```text
https://username:password@example.com/repo.git
```

禁止将 Token 写入 `.git/config`。

建议单独构建：

```text
GitBinder.CredentialHelper
```

输出：

```text
gitbinder-credential
```

Git：

```text
git
 │
 ▼
credential.helper
 │
 ▼
gitbinder-credential
 │
 ▼
SecretStore
 │
 ▼
username + password/token
```

GUI 不运行时 Credential Helper 仍可工作。

仓库级 `credential.helper` 必须先写入空值以重置继承的 Helper 链，再追加带 `--account-id <Guid>` 的 GitBinder Helper 绝对路径。该 Guid 固定到当前选中账号，Helper 不应再按 Host、默认账号或系统已有凭据回退；账号没有 HTTPS Secret 时只保留空 Helper，认证应失败。

---

# 11. Secret Store 架构

统一接口：

```csharp
public interface ISecretStore
{
    Task SetAsync(string key, string value);
    Task<string?> GetAsync(string key);
    Task DeleteAsync(string key);
}
```

不同平台：

```text
Windows
   ↓
Credential Manager / DPAPI

macOS
   ↓
Keychain

Ubuntu / Linux
   ↓
Secret Service / libsecret
```

数据库中只允许保存：

```text
SecretId
```

不得明文保存：

- Password；
- Token；
- SSH Passphrase；
- Private Key Content。

SSH Private Key 默认仅记录文件路径。

---

# 12. Platform Adapter

平台差异全部放入：

```text
GitBinder.Platform
```

建议接口：

```text
IPlatformService
ISecretStore
IApplicationDataPath
IPathNormalizer
IFilePermissionService
IGitLocator
ISshLocator
IShellService
```

启动时：

```csharp
if (OperatingSystem.IsWindows())
{
    services.AddWindowsPlatform();
}
else if (OperatingSystem.IsMacOS())
{
    services.AddMacOSPlatform();
}
else if (OperatingSystem.IsLinux())
{
    services.AddLinuxPlatform();
}
```

---

# 13. 路径架构

禁止：

```text
"C:\\Users\\..."
```

散落在业务代码中。

禁止简单使用：

```text
Replace("\\", "/")
```

路径统一经过：

```text
IPathNormalizer
```

处理：

```text
Normalize
Canonicalize
ResolveSymlink
Compare
```

尤其需要考虑：

- Windows 默认大小写不敏感；
- Linux 大小写敏感；
- macOS 文件系统可能大小写敏感或不敏感；
- 符号链接；
- 网络磁盘；
- UNC Path；
- Repository Worktree。

---

# 14. Command Executor

所有 Git / SSH 外部命令统一经过：

```text
ICommandExecutor
```

接口：

```csharp
Task<CommandResult> ExecuteAsync(
    string executable,
    IReadOnlyList<string> arguments,
    string? workingDirectory,
    CancellationToken cancellationToken);
```

必须统一处理：

- stdout；
- stderr；
- Exit Code；
- Timeout；
- Cancellation；
- Encoding；
- Environment Variables；
- Secret Masking；
- Process Kill；
- Command Logging。

禁止大量直接使用：

```csharp
Process.Start(...)
```

更禁止拼接：

```text
cmd.exe /c "..."
```

参数必须使用参数数组，降低 Shell Injection 风险。

---

# 15. 数据存储

数据库使用：

```text
SQLite
```

不同平台数据目录：

### Windows

```text
%LOCALAPPDATA%/GitBinder/
```

### macOS

```text
~/Library/Application Support/GitBinder/
```

### Linux

```text
~/.local/share/GitBinder/
```

目录结构：

```text
GitBinder/
├── gitbinder.db
├── config.json
├── logs/
└── backup/
```

---

# 16. GUI 架构

采用标准 MVVM：

```text
View
 │
 ▼
ViewModel
 │
 ▼
Application Service
 │
 ▼
Domain
 │
 ▼
Infrastructure
```

禁止：

```text
Button_Click
    ↓
Process.Start("git")
```

应为：

```text
Button
  ↓
ICommand
  ↓
ViewModel
  ↓
ApplicationService
  ↓
GitService
```

---

# 17. GUI 页面结构

```text
Dashboard

Accounts
Projects
Bindings

Global Mode

Logs
Settings
```

建议全局导航保持平台一致，不为不同 OS 维护不同业务页面。

系统级交互允许使用平台适配：

- 文件选择器；
- 系统托盘；
- 打开 Finder / Explorer / File Manager；
- 打开 Terminal；
- 系统 Credential Store。

---

# 18. i18n / l10n 国际化架构

国际化必须从项目第一版就作为基础架构能力实现，禁止后期再通过大量替换硬编码文本补救。

目标：

```text
同一套业务代码
      │
      ├── zh-CN
      ├── en-US
      └── 后续其他语言
```

V1 推荐至少提供：

```text
zh-CN
en-US
```

---

# 19. i18n 核心原则

## 19.1 UI 禁止硬编码用户可见文本

禁止：

```xml
<Button Content="保存" />
```

或者：

```csharp
MessageBox.Show("保存成功");
```

所有用户可见文本必须使用：

```text
Localization Key
```

例如：

```text
Common.Save
Common.Cancel

Accounts.Title
Accounts.Add
Accounts.Edit

Projects.Title

Binding.Test.Success

GlobalMode.EnabledWarning
```

---

# 20. Localization Service

推荐建立：

```csharp
public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }

    IReadOnlyList<CultureInfo> SupportedCultures { get; }

    string GetString(string key);

    string GetString(string key, params object[] args);

    Task SetCultureAsync(CultureInfo culture);
}
```

业务层不得直接读取 RESX。

所有动态语言切换统一经过：

```text
ILocalizationService
```

---

# 21. Resource 目录

推荐：

```text
src/GitBinder.Desktop/Localization/
│
├── Resources.resx
├── Resources.zh-CN.resx
├── Resources.en-US.resx
│
└── LocalizationKeys.cs
```

也可以按模块拆分：

```text
Localization/
├── Common/
│   ├── Common.zh-CN.resx
│   └── Common.en-US.resx
│
├── Accounts/
├── Projects/
├── Bindings/
├── GlobalMode/
└── Settings/
```

项目规模扩大后推荐模块化资源。

---

# 22. Localization Key 规范

禁止使用中文本身作为 Key：

```text
保存=Save
```

应该使用语义 Key：

```text
Common.Save
Common.Cancel
Common.Confirm

Account.Add.Title
Account.Delete.Confirm

Project.Binding.Success

GlobalMode.Enable.Warning
```

Key 必须：

- 与语言无关；
- 具有业务语义；
- 不包含 UI 布局位置；
- 不依赖具体中文文本。

---

# 23. 动态语言切换

Settings 页面提供：

```text
Language

○ Follow System
○ 简体中文
○ English
```

默认：

```text
Follow System
```

启动时：

```text
用户已配置语言
      ↓
使用用户语言

否则
      ↓
检测 CurrentUICulture

如果支持
      ↓
使用系统语言

否则
      ↓
fallback → en-US
```

语言切换原则上不要求重启应用。

Avalonia UI 通过绑定 Localization Service，在 Culture Changed 后通知相关 ViewModel / Resource Binding 更新。

---

# 24. Culture 与 Language 分离

必须区分：

```text
UI Language
```

和：

```text
Formatting Culture
```

例如用户可以：

```text
UI = English
System Region = China
```

因此：

- UI 文案可以是英文；
- 时间、数字等格式可以遵循系统区域设置。

不要假设：

```text
Language == Region
```

---

# 25. 日期、时间、数字格式

禁止：

```csharp
date.ToString("yyyy-MM-dd HH:mm:ss")
```

直接用于所有 GUI。

展示层应根据：

```text
CultureInfo
```

格式化。

例如：

```text
zh-CN:
2026/08/22 13:30

en-US:
8/22/2026 1:30 PM
```

但是：

- 数据库存储；
- 日志机器字段；
- JSON；
- API/Internal DTO；

统一使用：

```text
ISO 8601
```

例如：

```text
2026-08-22T13:30:00+08:00
```

---

# 26. 日志与 UI 文案分离

日志不要直接存：

```text
“项目绑定成功”
```

建议记录：

```text
EventId = BindingApplied
ProjectId = ...
AccountId = ...
```

UI 展示时：

```text
BindingApplied
      ↓
Localization
      ↓
项目绑定成功
```

英文：

```text
Binding applied successfully.
```

这样：

- 日志可稳定搜索；
- 切换 UI 语言不影响历史日志；
- 日志格式不依赖语言。

诊断日志可以同时记录英文技术信息，但不得使用本地化字符串作为唯一机器标识。

---

# 27. 错误模型国际化

Domain/Application 不应该抛出：

```text
throw new Exception("仓库不存在");
```

推荐：

```text
DomainError
├── Code
├── Arguments
└── TechnicalDetails
```

例如：

```text
Code:
PROJECT_REPOSITORY_NOT_FOUND

Arguments:
path
```

GUI：

```text
Error Code
    ↓
Localization Key
    ↓
Projects.Error.RepositoryNotFound
```

中文：

```text
未找到 Git 仓库：{0}
```

英文：

```text
Git repository not found: {0}
```

---

# 28. Git / SSH 原始错误处理

Git 与 SSH 返回的 stderr 不应尝试强行全文翻译。

例如：

```text
Permission denied (publickey).
```

应显示：

```text
认证失败

Git/SSH 原始信息：
Permission denied (publickey).
```

其中：

```text
认证失败
```

属于本地化业务解释。

原始 stderr：

```text
保持原样
```

方便用户排查和搜索资料。

---

# 29. 安装包国际化

安装包语言与应用语言分离。

推荐：

### Windows Installer

至少：

```text
English
Simplified Chinese
```

### macOS

`.app` 本身遵循系统语言。

### Ubuntu

安装包元数据至少提供英文描述，应用启动后使用自己的 i18n。

安装器不得成为业务语言配置的唯一来源。

应用首次启动仍然根据：

```text
CurrentUICulture
```

确定语言。

---

# 30. i18n 测试

必须建立测试：

```text
LocalizationTests
```

检查：

### Key 完整性

```text
zh-CN keys
==
en-US keys
```

防止某种语言缺失文本。

### 空值检测

禁止：

```text
key exists but value empty
```

### 占位符一致性

例如：

```text
zh-CN:
已绑定 {0} 个项目

en-US:
{0} projects bound
```

必须检查两边参数数量一致。

### UI Overflow

CI 无法完全解决，应在 GUI 测试中重点检查：

- 英文通常比中文长；
- 德语未来可能更长；
- Button 不应写死宽度；
- Dialog 应允许自适应；
- TextBlock 应支持 Wrap。

---

# 31. i18n 禁止事项

禁止：

```text
"账号：" + accountName
```

应该：

```text
Localization.Format("Account.Label", accountName)
```

禁止根据中文字符串做业务判断：

```csharp
if (status == "成功")
```

必须使用：

```text
enum / code
```

禁止：

```text
Enum.ToString()
```

直接显示给用户。

例如：

```text
AuthenticationType.Ssh
```

显示时：

```text
AuthenticationType.Ssh
      ↓
Authentication.Ssh
      ↓
SSH
```

---

# 32. Global Mode 架构

Global Mode 是 Overlay，不修改 Binding 数据关系。

普通模式：

```text
Project A → Account A
Project B → Account B
```

开启：

```text
Global Account = Account C
```

有效账号：

```text
Project A → Account C
Project B → Account C
```

数据库中的原 Binding：

```text
保持不变
```

Global Mode 开启时需要：

```text
Snapshot
→ Apply Global Account
→ Verify
```

除 `user.name`、`user.email` 外，还需以 Global Account 覆盖全局 `core.sshCommand` 与 `credential.helper`：SSH 禁止使用系统 Agent/默认 Key 回退，HTTPS 重置已有 Helper 后仅调用带账号 Id 的 GitBinder Helper。关闭时一并恢复进入全局模式前快照中的上述配置。

应用启动时，全局模式关闭也要重新应用所有已登记项目的 Binding；这是认证配置的升级补写步骤，确保旧绑定获得当前的 SSH/HTTPS 无回退规则，而不要求用户逐个重新保存绑定。

关闭：

```text
Drift Detection
→ Restore Snapshot
```

Global Mode 建议使用状态机：

```text
DISABLED
ENABLING
ENABLED
SWITCHING
RESTORING
ERROR
```

而不是只有 Boolean。

---

# 33. Repository Config Snapshot

修改 `.git/config` 前必须 Snapshot。

保存：

```text
user.name
user.email
core.sshCommand
credential.helper
credential.useHttpPath
```

流程：

```text
Read
 ↓
Snapshot
 ↓
Validate
 ↓
Write
 ↓
Read Back
 ↓
Verify
 ↓
Commit DB Transaction
```

失败：

```text
Rollback
```

---

# 34. 配置漂移检测

其他工具可能修改 `.git/config`：

```text
IDEA
VS Code
SourceGit
Git CLI
```

因此数据库 Binding 不能作为唯一真相。

Project 打开或测试时读取实际配置：

```text
user.name
user.email
core.sshCommand
credential.helper
```

状态：

```text
SYNCED
DRIFTED
UNBOUND
ERROR
```

GUI 支持：

```text
重新应用绑定
采用当前仓库配置
```

---

# 35. 数据库建议

核心表：

```text
accounts
projects
bindings
repository_snapshots
global_mode
global_mode_snapshots
operation_logs
settings
```

国际化不要把翻译文本写数据库。

数据库中存：

```text
status_code
error_code
event_code
```

GUI 通过 Localization Service 转换为用户语言。

---

# 36. Settings

Settings 建议包含：

```text
General
├── Language
├── Theme
├── Startup
└── Close to tray on window close

Git
├── Git Executable
└── Git Version

SSH
├── SSH Executable
└── SSH Version

Storage
├── Data Directory
└── Log Directory

Security
└── Secret Store Status
```

语言配置存：

```text
settings.language
```

关闭托盘配置存：

```text
settings.close_to_tray_on_close
```

缺省值为 `true`：用户关闭主窗口时仅隐藏窗口，托盘菜单提供“显示窗口”和“退出 GitBind”；用户在设置中关闭该选项后，关闭窗口即退出应用。

例如：

```text
system
zh-CN
en-US
```

---

# 37. 打包架构

统一使用：

```text
dotnet publish
+
平台原生打包脚本
```

发布采用：

```text
Self-contained
```

避免要求用户提前安装 .NET Runtime。

---

# 38. Windows 构建

Runtime Identifier：

```text
win-x64
win-arm64
```

推荐产物：

```text
GitBinder-x.y.z-win-x64-setup.exe
GitBinder-x.y.z-win-x64.msi

GitBinder-x.y.z-win-x64-portable.zip
```

Portable 版本对于开发工具用户建议保留。

---

# 39. macOS 构建

Runtime Identifier：

```text
osx-x64
osx-arm64
```

产物：

```text
GitBinder.app
GitBinder-x.y.z-macos.dmg
```

正式发布必须考虑：

```text
Code Signing
Notarization
```

推荐分别构建：

```text
Intel
Apple Silicon
```

未来再考虑 Universal Binary。

---

# 40. Ubuntu / Linux 构建

Runtime Identifier：

```text
linux-x64
linux-arm64
```

主要产物：

```text
gitbinder_x.y.z_amd64.deb
gitbinder_x.y.z_arm64.deb
```

可选：

```text
AppImage
```

优先保证 `.deb`。

---

# 41. CI/CD

Release Pipeline：

```text
Git Tag
   │
   ├──────── Windows Runner
   │           ↓
   │        Build/Test
   │           ↓
   │        EXE/MSI/ZIP
   │
   ├──────── macOS Runner
   │           ↓
   │        Build/Test
   │           ↓
   │        Sign/Notarize
   │           ↓
   │        APP/DMG
   │
   └──────── Ubuntu Runner
               ↓
            Build/Test
               ↓
              DEB
```

不要依赖一台 Windows 主机交叉构建所有正式发行包。

---

# 42. 测试架构

## Unit Tests

重点：

```text
Account
Project
Binding
EffectiveAccountResolver
GlobalMode
Localization
```

## Integration Tests

动态建立临时 Git Repository：

```text
git init
```

测试：

- local config；
- SSH Command 写入；
- Snapshot；
- Restore；
- Drift Detection；
- Global Mode。

## Platform Tests

分别在：

```text
Windows
macOS
Ubuntu
```

测试：

- Path；
- Secret Store；
- Git Locator；
- SSH Locator；
- Process Executor；
- Application Data Directory。

---

# 43. 纯离线要求

应用自身禁止：

- 自动检查更新；
- Telemetry；
- Crash Upload；
- Cloud Sync；
- CDN；
- 在线头像；
- 在线 License；
- SaaS Login。

允许用户主动触发：

```text
git clone
git fetch
git pull
git push
git ls-remote
ssh remote test
```

GUI 必须明确区分：

```text
本地配置测试
```

与：

```text
远程连接测试
```

---

# 44. 安全架构

必须遵守：

1. Private Key Content 不进入数据库；
2. Password / Token 不明文存储；
3. Passphrase 不明文存储；
4. 日志必须 Secret Masking；
5. 外部命令禁止 Shell 字符串拼接；
6. Git Config 修改必须 Snapshot；
7. Global Mode 必须可恢复；
8. HTTPS Token 不得写入 Remote URL；
9. Sensitive UI 默认遮罩；
10. Clipboard Copy Secret 后可考虑延迟清理。

---

# 45. 推荐 NuGet 依赖方向

建议优先使用成熟基础库：

```text
Avalonia
Avalonia.Desktop
CommunityToolkit.Mvvm

Microsoft.Extensions.DependencyInjection
Microsoft.Extensions.Configuration
Microsoft.Extensions.Logging

Microsoft.Data.Sqlite
或
Microsoft.EntityFrameworkCore.Sqlite

Serilog
Serilog.Sinks.File

xUnit
FluentAssertions（可选）
```

避免第一版引入过多 UI 框架和大型第三方依赖。

---

# 46. 开发约束

## Domain

不得依赖：

```text
Avalonia
SQLite
Windows API
macOS API
Linux API
```

## Application

不得：

```text
直接 Process.Start
直接访问 SQLite
直接访问 DPAPI / Keychain
```

## GUI

不得：

```text
直接执行 git
直接操作数据库
直接读取 SSH Key
```

## Infrastructure

负责：

```text
Git
SSH
Persistence
Logging
Credentials
```

## Platform

只负责操作系统差异。

---

# 47. 推荐 MVP

V1：

```text
Accounts
Projects
Bindings
Default Account
SSH
HTTPS
Repository Local Config
Binding Test
Global Mode
Snapshot / Restore
SQLite
Secret Store
zh-CN
en-US
Windows/macOS/Ubuntu Build
```

暂不实现：

```text
完整 Commit GUI
Diff GUI
Merge GUI
Rebase GUI
PR/MR
Issue
Cloud Sync
```

GitBinder 首先应该保持定位：

> Git Repository Account Binding Manager

而不是发展成另一个完整 Git Client。

---

# 48. 最终架构结论

推荐最终技术架构：

```text
                  GitBinder
                      │
          ┌───────────┴───────────┐
          │                       │
 Avalonia Desktop        Credential Helper
          │                       │
         MVVM                    CLI
          │                       │
          └───────────┬───────────┘
                      │
              Application Layer
                      │
          ┌───────────┼───────────┐
          │           │           │
       Account      Project     Binding
          │           │           │
          └───────────┼───────────┘
                      │
                 Domain Core
                      │
           EffectiveAccountResolver
                      │
       ┌──────────────┼──────────────┐
       │              │              │
   GitService      SSHService    SecretStore
       │              │              │
   System Git      OpenSSH           │
                                     │
                   ┌─────────────────┼─────────────────┐
                   │                 │                 │
                Windows            macOS             Linux
            CredentialMgr         Keychain       SecretService
```

UI 国际化：

```text
                 Avalonia UI
                      │
             LocalizationService
                      │
        ┌─────────────┴─────────────┐
        │                           │
      zh-CN                       en-US
        │                           │
        └─────────────┬─────────────┘
                      │
              Culture Changed
                      │
                Dynamic Refresh
```

发行：

```text
.NET 10 LTS
+
Avalonia 12
+
Self-contained

├── Windows
│   ├── x64 / arm64
│   ├── Installer
│   └── Portable
│
├── macOS
│   ├── x64 / arm64
│   └── DMG
│
└── Ubuntu
    ├── amd64 / arm64
    └── DEB
```

该架构的核心原则：

> **业务逻辑跨平台、平台能力通过 Adapter 隔离、Git 行为依赖系统标准 Git/OpenSSH、敏感数据使用操作系统 Secret Store、所有用户可见文本从第一版开始通过 i18n Resource 管理。**
