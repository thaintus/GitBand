# Git 仓库账号绑定 GUI 项目设计文档

> 项目：**GitBind**
> 文档版本：v1.0
> 项目定位：本地 Git 账号、仓库及账号绑定关系管理工具
> 运行模式：纯本地 GUI 应用，无云端服务、无账号服务器、无遥测

------

# 1. 项目背景

开发人员通常同时拥有多个 Git 托管平台及多个 Git 账号，例如：

- GitHub
- Gitee
- 阿里云云效 Codeup
- 腾讯云代码托管
- GitLab
- 企业自建 Git 服务

不同项目可能要求使用不同的：

- Git 登录账号
- `user.name`
- `user.email`
- SSH Private Key
- HTTPS Username / Password / Access Token

当本地存在多个 Git 身份时，仅依赖全局 `.gitconfig`、`~/.ssh/config` 或 `ssh-agent` 容易出现：

- 使用错误账号提交 Commit；
- Commit Author 邮箱错误；
- Push 时使用错误 SSH Key；
- 多个 GitHub/Gitee 账号之间发生 SSH 身份冲突；
- 公司项目误用个人身份；
- 每个仓库手工维护 `.git/config` 成本较高；
- 临时需要统一使用某个账号时，需要修改大量配置。

因此开发一个独立 GUI 应用，对：

**Git Account → Repository → Binding**

进行统一管理。

------

# 2. 项目目标

GitBind 主要解决三个核心问题：

1. **Git 账号统一管理**
2. **Git 仓库统一管理**
3. **仓库与 Git 账号的绑定管理**

并提供一个特殊的：

**Global Mode（全局模式）**

用于临时忽略所有仓库原有绑定关系，使所有受 GitBind 管理的 Git CLI 行为统一使用指定账号。

------

# 3. 核心设计原则

## 3.1 Local First

所有数据均保存在本机。

不建设：

- 后端服务器
- 云数据库
- 用户中心
- 云同步服务
- 遥测服务
- 在线配置中心

应用启动和日常管理不依赖互联网。

------

## 3.2 “纯离线”的定义

本项目中的“纯离线”指：

> GitBind 本身不依赖任何互联网服务，不主动访问互联网。

以下操作属于用户主动执行的 Git 网络行为，不视为违反纯离线原则：

- `git fetch`
- `git pull`
- `git push`
- `git clone`
- `git ls-remote`
- SSH 认证测试
- HTTPS Git 认证测试

例如：

```text
GitBind GUI
    │
    │ 不访问互联网
    │
    ├── 本地数据库
    ├── 本地 Git
    ├── 本地 SSH
    └── 本地凭据库

用户点击“测试连接”
        │
        ▼
      git.exe
        │
        ▼
GitHub / Gitee / 云效 / 腾讯云
```

应用禁止实现：

- 自动检查更新；
- 在线统计；
- 在线头像；
- CDN 资源；
- 在线字体；
- 后台自动 Fetch；
- 崩溃日志上传。

所有前端资源必须随应用打包。

------

# 4. 核心业务模型

整个系统围绕三个核心实体构建：

```text
Account
   │
   │ 1
   │
   │
   ▼
Binding
   ▲
   │
   │ 1
   │
Project
```

一个账号可以绑定多个项目。

一个项目只能绑定一个账号。

即：

```text
Account A
├── Project 1
├── Project 2
└── Project 3

Account B
├── Project 4
└── Project 5
```

不允许：

```text
Project 1
├── Account A
└── Account B
```

------

# 5. Git 身份模型

需要明确区分 Git 中两个完全不同的身份概念。

## 5.1 Commit Identity

决定 Git Commit 中显示谁提交：

```text
user.name
user.email
```

例如：

```ini
[user]
    name = Zhang San
    email = zhangsan@company.com
```

Commit 最终显示：

```text
Author:
Zhang San <zhangsan@company.com>
```

------

## 5.2 Authentication Identity

决定 Pull / Fetch / Push 时使用哪个远程账号。

主要分为：

### SSH

```text
Private Key
```

例如：

```text
id_ed25519_github_personal
id_ed25519_github_company
id_ed25519_gitee
id_ed25519_yunxiao
```

### HTTPS

可能包括：

```text
Username
Password
Personal Access Token
Access Token
```

因此一个完整 Git Account 应同时包含：

```text
Git Account
├── Commit Identity
│   ├── user.name
│   └── user.email
│
└── Authentication
    ├── SSH Private Key
    └── HTTPS Credential
```

------

# 6. 账号管理

## 6.1 Account 数据结构

建议设计如下：

```text
Account
├── id
├── alias
├── platform
├── host
├── username
├── commit_name
├── commit_email
│
├── https
│   ├── enabled
│   ├── username
│   └── credential_ref
│
├── ssh
│   ├── enabled
│   ├── ssh_user
│   ├── private_key_path
│   ├── private_key_ref
│   └── passphrase_ref
│
├── is_default
├── enabled
├── created_at
└── updated_at
```

------

## 6.2 alias

账号支持配置别名。

例如：

```text
账号名：
zhangsan

别名：
云效-公司
```

如果用户没有填写 Alias：

```text
alias = username
```

例如：

```text
username = ice
alias = ice
```

------

# 7. 平台支持

平台字段建议只作为辅助信息，不应把业务逻辑强绑定到具体平台 API。

内置：

```text
GitHub
Gitee
GitLab
Aliyun Codeup
Tencent
Custom
```

例如：

```text
Platform: Aliyun Codeup
Host: codeup.aliyun.com
```

或者：

```text
Platform: Custom
Host: git.company.internal
```

GitBind 不调用这些平台的 REST API。

平台认证统一通过：

```text
Git
+
SSH / HTTPS
```

完成。

这样未来无需针对每个平台开发 SDK。

------

# 8. 默认账号

系统必须存在一个 Default Account。

例如：

```text
Accounts

★ Personal
  Company-A
  Company-B
  Gitee
```

其中：

```text
Personal
```

为默认账号。

规则：

```text
系统最多只能存在一个默认账号。
```

新录入项目时：

```text
Project.account_id = DefaultAccount.id
```

------

## 8.1 默认账号删除规则

如果用户删除当前默认账号：

必须先选择新的默认账号。

禁止出现：

```text
Default Account = NULL
```

只要系统中存在 Account，就必须有一个 Default Account。

------

# 9. 项目管理

Project 表示一个已经存在于本机的 Git Repository。

GitBind 不负责保存代码。

只保存：

```text
项目路径
+
Git 元数据
+
账号绑定关系
```

------

## 9.1 Project 数据结构

```text
Project
├── id
├── name
├── root_path
├── git_dir
├── remote_name
├── remote_url
├── remote_protocol
├── remote_host
├── account_id
├── enabled
├── last_test_status
├── last_test_at
├── created_at
└── updated_at
```

------

# 10. 添加项目

支持：

```text
添加项目
```

用户选择：

```text
D:\Projects\xxx
```

系统执行：

```bash
git -C "D:\Projects\xxx" rev-parse --show-toplevel
```

判断是否为 Git Repository。

如果不是：

```text
该目录不是有效的 Git 仓库。
```

如果是，则读取：

```bash
git remote
```

默认优先：

```text
origin
```

然后：

```bash
git remote get-url origin
```

获得：

```text
git@github.com:xxx/project.git
```

或者：

```text
https://github.com/xxx/project.git
```

自动解析：

```text
Protocol
Host
Repository
```

------

# 11. 新项目默认绑定

项目第一次添加后：

```text
Project
    ↓
Default Account
```

例如：

```text
Default Account = Personal
```

添加：

```text
Project A
```

系统自动：

```text
Project A → Personal
```

用户之后可以手动修改。

------

# 12. Binding 管理

Binding 表示：

```text
Repository
      ↓
Account
```

虽然数据库层面可以直接使用：

```text
Project.account_id
```

实现，但业务层建议仍然定义独立：

```text
BindingService
```

负责所有绑定行为。

这样未来可以加入：

- 绑定历史
- 自动绑定规则
- 批量绑定
- Binding Audit
- Remote 匹配规则

------

# 13. Binding 规则

必须遵守：

```text
一个 Project
只能绑定
一个 Account
```

例如：

```text
Project A → Account A
```

重新绑定：

```text
Project A → Account B
```

意味着：

```text
Account A 绑定解除

Project A
    ↓
Account B
```

不允许同时绑定。

------

# 14. 正常模式

Global Mode 关闭时：

```text
Git Command
     │
     ▼
当前目录属于哪个 Repository？
     │
     ├── 已登记
     │      │
     │      ▼
     │   获取 Binding
     │      │
     │      ▼
     │   获取 Account
     │
     └── 未登记
            │
            ▼
       不进行干预
```

即：

```text
Global Mode OFF

Registered Project
    → 使用绑定 Account

Unknown Repository
    → 使用用户原始 Git 配置
```

这是一个重要原则：

> GitBind 不应该默认接管所有未登记的 Git 仓库。

------

# 15. 全局模式

Global Mode 为系统最高优先级规则。

开启：

```text
Global Mode = ON
```

必须指定：

```text
Global Account
```

默认：

```text
Global Account = Default Account
```

用户可以选择其他账号。

------

# 16. Global Mode 优先级

统一定义：

```text
1. Global Mode Account
        ↓
2. Project Binding Account
        ↓
3. Native Git Configuration
```

算法：

```text
if global_mode == true:
    account = global_account

else if repository is managed:
    account = repository.bound_account

else:
    account = null
    passthrough
```

Global Mode 开启后：

```text
Project A → Personal
Project B → Company A
Project C → Company B
```

全部临时失效。

如果：

```text
Global Account = Company A
```

则：

```text
Project A → Company A
Project B → Company A
Project C → Company A
```

关闭 Global Mode：

```text
Project A → Personal
Project B → Company A
Project C → Company B
```

立即恢复。

原来的 Binding 数据不发生任何修改。

------

# 17. 为什么不能简单修改 global .gitconfig

不能通过：

```bash
git config --global user.name xxx
```

简单实现 Global Mode。

因为 Git 配置优先级通常为：

```text
System
   <
Global
   <
Local Repository
   <
Command
```

如果仓库内部已经存在：

```ini
[user]
    name = Personal
```

即使：

```text
Global user.name = Company
```

当前 Repository 仍然优先使用：

```text
Personal
```

因此 Global Mode 必须使用：

**Command Level Override**

而不是普通 Global Config。

------

# 18. 核心技术：Git Shim

为了实现真正的仓库绑定和 Global Mode，GitBind 应提供一个：

```text
Git Shim
```

例如 Windows：

```text
C:\Program Files\GitBind\shim\git.exe
```

实际 Git：

```text
C:\Program Files\Git\cmd\git.exe
```

系统调用：

```text
git
 ↓
GitBind Shim
 ↓
Policy Resolver
 ↓
Real Git
```

------

# 19. Git Shim 工作流程

```text
                    git command
                         │
                         ▼
                    GitBind Shim
                         │
                         ▼
                 Global Mode ?
                  /          \
               YES            NO
                │              │
                ▼              ▼
       Global Account     Detect Repository
                               │
                        Managed Project ?
                         /           \
                       YES            NO
                        │              │
                        ▼              ▼
                 Binding Account    Passthrough
                        │
                        └──────┬───────┘
                               ▼
                          Real git.exe
```

------

# 20. Commit Identity 注入

例如当前账号：

```text
commit_name  = Zhang San
commit_email = zhang@company.com
```

Shim 调用真实 Git 时等价于：

```bash
git \
  -c user.name="Zhang San" \
  -c user.email="zhang@company.com" \
  <original arguments>
```

`-c` 属于命令级配置。

可以覆盖：

```text
global
local
includeIf
```

因此 Global Mode 可以可靠覆盖仓库已有的：

```text
.git/config
```

而且不会修改 Repository 本身。

------

# 21. SSH Account 注入

如果 Remote 为：

```text
SSH
```

例如：

```text
git@github.com:company/project.git
```

账户：

```text
SSH Key:
C:\Users\User\.gitbind\keys\company
```

Shim 可以通过命令级配置执行：

```text
core.sshCommand =
ssh -i "<private-key>" -o IdentitiesOnly=yes
```

核心参数：

```text
IdentitiesOnly=yes
```

必须存在。

其作用是：

> 禁止 SSH 自动尝试 ssh-agent 中的其他 Key，只允许当前 Account 指定的 SSH Identity。

否则机器上存在多个 Key 时可能认证到错误账号。

------

# 22. HTTPS Credential

对于 HTTPS：

```text
https://github.com/company/project.git
```

禁止把：

```text
username
password
token
```

写入：

```text
.git/config
```

或者：

```text
remote URL
```

禁止：

```text
https://username:password@host/repository.git
```

GitBind 应实现：

```text
gitbind-credential-helper
```

通过 Git Credential Helper 协议提供认证数据。

流程：

```text
git
 │
 ▼
Credential Helper
 │
 ▼
Account ID
 │
 ▼
OS Secure Credential Store
 │
 ▼
Username / Token
```

------

# 23. Password 与 Token

Account UI 不应只设计：

```text
Password
```

建议统一命名：

```text
HTTPS Credential
```

类型：

```text
Password
Personal Access Token
Access Token
```

因为部分 Git 平台已经不支持直接使用网站登录密码执行 Git HTTPS 操作。

GitBind 不需要理解具体 Token 类型。

只需要按照：

```text
Username
+
Credential
```

提供给 Git Credential Protocol。

------

# 24. Private Key 管理

支持两种 SSH Key 模式。

## 24.1 Reference

引用已有：

```text
C:\Users\User\.ssh\id_ed25519_company
```

GitBind 只记录路径。

优点：

```text
不复制私钥
兼容用户原有 SSH 体系
```

------

## 24.2 Import

用户选择：

```text
Import Private Key
```

GitBind 将私钥复制到：

```text
%LOCALAPPDATA%\GitBind\keys\
```

例如：

```text
%LOCALAPPDATA%\GitBind\keys\
├── 0ae7937b.key
├── 137bc15d.key
└── 8d32177e.key
```

必须设置当前 Windows 用户专属 ACL。

禁止：

```text
Everyone
Users
Guest
```

读取 Private Key。

------

# 25. Secret Storage

数据库禁止直接保存：

```text
Password
Token
Private Key Passphrase
```

数据库只保存：

```text
credential_ref
```

例如：

```text
account_id:
76ce...

credential_ref:
gitbind/account/76ce/https
```

Windows 第一版推荐：

```text
Windows Credential Manager
```

或：

```text
Windows DPAPI CurrentUser
```

用于保护敏感数据。

------

# 26. 数据库存储

推荐：

```text
SQLite
```

路径：

```text
%LOCALAPPDATA%\GitBind\data\gitbind.db
```

SQLite 只存：

```text
Accounts metadata
Projects
Bindings
Settings
Audit metadata
```

不保存明文密码。

------

# 27. 建议数据库结构

## accounts

```sql
accounts
-------
id
alias
platform
host
username
commit_name
commit_email

ssh_enabled
ssh_user
ssh_private_key_path
ssh_private_key_ref
ssh_passphrase_ref

https_enabled
https_username
https_credential_ref

is_default
enabled

created_at
updated_at
```

------

## projects

```sql
projects
--------
id
name
root_path
git_dir

remote_name
remote_url
remote_protocol
remote_host

account_id

last_test_status
last_test_at

created_at
updated_at
```

其中：

```text
account_id
```

为：

```text
accounts.id
```

外键。

必须：

```text
NOT NULL
```

正常情况下所有已登记 Project 均必须有绑定账号。

------

## settings

```sql
settings
--------
key
value
updated_at
```

至少包括：

```text
global_mode_enabled
global_account_id
real_git_path
shim_enabled
```

------

# 28. Runtime Policy

Git Shim 的启动速度非常重要。

不建议每次：

```text
git status
```

都启动 GUI 或复杂业务层。

GUI 每次修改 Account / Project / Binding 后，生成：

```text
runtime-policy.json
```

例如：

```json
{
  "globalMode": false,
  "globalAccountId": "xxx",
  "projects": {
    "D:\\Projects\\A": "account-a",
    "D:\\Projects\\B": "account-b"
  }
}
```

Shim：

```text
read policy
→ resolve account
→ execute real git
```

该文件：

- 不包含 Password
- 不包含 Token
- 不包含 Private Key 内容

只包含引用信息。

------

# 29. Policy 更新

Runtime Policy 必须采用：

```text
Write Temp
    ↓
fsync
    ↓
Atomic Rename
```

避免 GUI 正在写文件时：

```text
git.exe
```

读取半截配置。

------

# 30. Repository Detection

Shim 必须能够正确识别：

```text
git status
```

执行时所在 Repository。

优先处理：

```text
git -C D:\Project status
```

然后：

```text
Current Working Directory
```

同时支持：

```text
.git directory
.git file
Git Worktree
```

路径统一转换为：

```text
Canonical Path
```

Windows 下路径比较应：

- 忽略大小写；
- 规范 `\` 与 `/`；
- 去除尾部目录分隔符；
- 解析符号链接时谨慎处理。

------

# 31. Binding Test

项目绑定账号后提供：

```text
测试
```

按钮。

测试必须分阶段执行。

------

## 31.1 Local Test

检查：

```bash
git rev-parse --show-toplevel
```

确认 Repository 有效。

------

## 31.2 Remote Test

读取：

```bash
git remote get-url origin
```

检查：

```text
Remote URL
Protocol
Host
```

------

## 31.3 Account Compatibility

例如：

```text
Account Host:
github.com

Project Remote:
git@gitee.com:user/project.git
```

GUI 应警告：

```text
账号 Host 与项目 Remote Host 不一致。
```

但允许高级用户强制测试。

因为存在：

```text
SSH Alias
企业反向代理
自建 Host
```

等情况。

------

# 32. Read Permission Test

使用当前 Account 临时执行：

```bash
git ls-remote <remote>
```

注意：

测试时不能修改 Repository 配置。

必须使用：

```text
临时 Command Override
```

测试：

```text
Authentication
+
Repository Read Permission
```

成功：

```text
✓ Git 认证成功
✓ Repository 可访问
✓ Fetch/Pull 权限可用
```

------

# 33. Push Permission Test

可提供高级测试：

```text
测试写权限
```

使用：

```bash
git push --dry-run --no-verify
```

但 GUI 必须提示：

> Dry Run 可以用于验证大部分 Push 认证及权限问题，但不同 Git 服务端实现不同，因此不能视为绝对写权限证明。

默认只执行：

```text
Read Test
```

避免不必要行为。

------

# 34. Test Result

建议 UI：

```text
连接测试

✓ 本地仓库             正常
✓ Remote               origin
✓ Protocol             SSH
✓ Host                 codeup.aliyun.com
✓ Account              Company-A
✓ SSH Key              可读取
✓ Authentication       成功
✓ Read Permission      成功

测试耗时：823 ms
```

失败时：

```text
✕ Authentication Failed

原因：
Permission denied (publickey)

当前账号：
Company-A

当前 SSH Key：
company_a_ed25519
```

禁止显示：

```text
Password
Token
Private Key Content
Passphrase
```

------

# 35. GUI 页面设计

建议包含：

```text
Dashboard
Accounts
Projects
Bindings
Settings
```

------

# 36. Dashboard

首页重点显示：

```text
GitBind

Global Mode
[ OFF ]

Default Account:
Personal

Accounts:
5

Repositories:
26

Binding Problems:
2
```

如果 Global Mode 开启：

```text
━━━━━━━━━━━━━━━━━━━━━━━━━━━━

⚠ GLOBAL MODE ENABLED

Current Account:
Company-A

所有受 GitBind Shim 管理的 Git 操作
都将强制使用 Company-A。

[ Change Account ]
[ Disable Global Mode ]

━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

必须使用明显视觉状态，防止用户忘记 Global Mode 已开启。

------

# 37. Accounts 页面

示例：

```text
Accounts

★ Personal
  GitHub
  ice
  ice@example.com

  Company-A
  Aliyun Codeup
  zhangsan
  zhangsan@company.com

  Company-B
  Tencent
  zhangsan
  zhangsan@company-b.com
```

操作：

```text
Add
Edit
Delete
Set Default
Duplicate
```

------

# 38. Account Editor

```text
Basic

Platform
[ Aliyun Codeup ▼ ]

Host
[ codeup.aliyun.com ]

Username
[ zhangsan ]

Alias
[ Company-A ]

Commit Identity

Name
[ Zhang San ]

Email
[ zhangsan@company.com ]


Authentication

SSH
[x] Enable

Private Key
[ C:\Users\...\company_a ]

[ Import Key ]


HTTPS
[x] Enable

Username
[ zhangsan ]

Credential
[ ************** ]

Type
[ Access Token ▼ ]


[ Save ]
```

------

# 39. Projects 页面

```text
Projects

Project                 Account       Remote
------------------------------------------------
zx_wechat_api           Company-A     Codeup
android-client          Company-A     Codeup
opensource-demo         Personal      GitHub
internal-tool           Company-B     Tencent
```

支持：

```text
Add Repository
Remove
Open Folder
Open Terminal
Change Binding
Test
```

Remove 只删除 GitBind 中记录。

绝不能：

```text
rm repository
```

删除本地源码。

------

# 40. Binding 页面

用于集中管理：

```text
Repository                  Bound Account
------------------------------------------------
zx_wechat_api               Company-A
android-client              Company-A
personal-demo               Personal
test-project                Personal
```

支持：

```text
批量选择
        ↓
Bind to Account
```

例如：

```text
Project A
Project B
Project C

       ↓

Bind To
Company-A
```

------

# 41. Global Mode GUI

Header 建议始终存在：

```text
Global Mode   [ OFF ]
```

开启：

```text
Global Mode   [ ON ]

Account
[ Personal ▼ ]
```

第一次开启默认：

```text
Default Account
```

之后用户可以选择：

```text
Company-A
```

切换立即生效。

------

# 42. Global Mode 不修改 Binding

非常重要：

开启 Global Mode 后，不允许：

```text
UPDATE projects
SET account_id = global_account_id
```

Global Mode 只是 Runtime Policy。

原 Binding：

```text
Project A → A
Project B → B
```

始终保留。

Global Mode 只是运行时：

```text
Effective Account = Global Account
```

------

# 43. Git Shim 安装

第一次运行提供：

```text
Enable System Git Integration
```

检测真实 Git：

```bash
where git
```

例如：

```text
C:\Program Files\Git\cmd\git.exe
```

记录：

```text
real_git_path
```

然后安装：

```text
GitBind\shim\git.exe
```

并将：

```text
GitBind\shim
```

加入 PATH，优先级高于真实 Git。

------

# 44. Shim 透明性要求

Shim 必须做到：

```text
Arguments 原样传递
stdin 原样传递
stdout 原样传递
stderr 原样传递
exit code 原样返回
```

例如：

```bash
git status
```

用户不应该感知：

```text
GitBind
```

存在。

执行：

```bash
git --version
```

结果必须仍然来自真实 Git。

------

# 45. 防递归

Shim 内部不能再次执行：

```text
git
```

否则：

```text
git shim
→ git shim
→ git shim
→ ...
```

必须通过绝对路径调用：

```text
C:\Program Files\Git\cmd\git.exe
```

并增加：

```text
GITBIND_SHIM_ACTIVE=1
```

作为防递归保护。

------

# 46. GUI 与 Shim 解耦

GitBind GUI 不需要一直运行。

架构：

```text
GUI
 │
 ├── SQLite
 ├── Secure Store
 └── runtime-policy
          │
          ▼
       Git Shim
          │
          ▼
       Real Git
```

GUI 关闭后：

```text
git pull
git push
git commit
```

仍然应该正常应用账号绑定策略。

这是核心设计要求之一。

------

# 47. IDE 兼容性

以下软件通常允许选择 Git executable：

- IntelliJ IDEA
- Android Studio
- VS Code
- Rider
- PyCharm
- WebStorm

应将 Git executable 指向：

```text
GitBind Shim
```

这样：

```text
IDE
 ↓
GitBind Shim
 ↓
Real Git
```

即可应用绑定关系。

------

# 48. 系统全局模式的能力边界

“系统全部 Git 行为”需要明确技术边界。

GitBind 可以可靠控制：

```text
Terminal
PowerShell
CMD
Git Bash
IDE 调用外部 git.exe
其他调用 PATH 中 git.exe 的程序
```

无法绝对保证控制：

```text
直接使用 libgit2 的第三方程序
内置 JGit 的程序
硬编码真实 git.exe 绝对路径的程序
绕过 PATH 的 Git 客户端
```

因此产品定义应表述为：

> Global Mode 强制覆盖所有经 GitBind Git Shim 执行的系统 Git CLI 行为。

不能宣称：

> 可以拦截操作系统中任何第三方 Git 实现。

------

# 49. 配置优先级

最终统一定义：

```text
               Highest
                  │
                  ▼
          Global Mode Account
                  │
                  ▼
        Repository Bound Account
                  │
                  ▼
        Native Git Configuration
                  │
                  ▼
               Lowest
```

------

# 50. Repository 多 Remote

一个 Repository 可能有：

```text
origin
upstream
backup
```

MVP 中账号绑定应以：

```text
Repository
```

为单位。

即：

```text
Project A → Account A
```

Project A 所有 Git 操作默认都使用：

```text
Account A
```

暂不实现：

```text
origin   → Account A
upstream → Account B
```

该能力可以作为后续版本：

```text
Remote-level Binding
```

------

# 51. SSH 与 HTTPS 同时存在

一个 Account 可以同时保存：

```text
SSH Credential
+
HTTPS Credential
```

运行时根据 Remote URL 自动选择。

例如：

```text
git@github.com:xxx/test.git
```

选择：

```text
SSH Key
```

而：

```text
https://github.com/xxx/test.git
```

选择：

```text
HTTPS Credential
```

用户不需要手工切换认证协议。

------

# 52. Account 删除

删除 Account 前检查：

```text
Bound Projects
```

如果：

```text
Company-A
├── Project A
├── Project B
└── Project C
```

则禁止直接删除。

提示：

```text
该账号当前绑定 3 个项目。

请选择：

○ 将项目重新绑定至默认账号
○ 选择其他账号
○ 取消
```

------

# 53. Project Path 变化

如果 Project 被移动：

```text
D:\Old\Project
```

变成：

```text
D:\New\Project
```

Dashboard 标记：

```text
Repository Missing
```

提供：

```text
Locate Repository
```

重新选择路径。

------

# 54. 工作树支持

需要正确识别：

```text
Git Worktree
```

因为 Worktree 的：

```text
.git
```

可能是文件，而不是目录。

不能简单通过：

```text
exists(".git directory")
```

判断 Git Repository。

必须调用：

```bash
git rev-parse
```

作为最终判断。

------

# 55. 日志

本地日志：

```text
%LOCALAPPDATA%\GitBind\logs\
```

记录：

```text
timestamp
operation
repository
account alias
git command type
result
duration
```

禁止记录：

```text
Password
Token
Private Key
Passphrase
Authorization Header
```

URL 中如含凭据：

```text
https://user:token@host/repository
```

日志必须脱敏：

```text
https://***:***@host/repository
```

------

# 56. 安全要求

必须遵守：

### 禁止 Shell 拼接

禁止：

```text
"git -c user.name=" + username
```

然后直接通过 shell 执行。

必须使用：

```text
Process API
+
Argument Array
```

防止 Command Injection。

------

### 私钥权限

Imported SSH Key 必须限制文件权限。

------

### Secret 永不落日志

包括：

```text
Password
Token
Passphrase
Private Key Content
```

------

### Secret 不存 SQLite 明文

SQLite 只存 Secret Reference。

------

# 57. 技术架构建议

Windows 第一版推荐：

```text
Tauri 2
+
Rust
+
Vue 3
+
TypeScript
+
SQLite
```

整体：

```text
┌──────────────────────────────┐
│          Vue 3 GUI           │
│                              │
│ Accounts / Projects / Binding│
│ Global Mode / Settings       │
└───────────────┬──────────────┘
                │ Tauri Command
                ▼
┌──────────────────────────────┐
│          Rust Core           │
│                              │
│ AccountService               │
│ ProjectService               │
│ BindingService               │
│ PolicyService                │
│ GitService                   │
│ CredentialService            │
└───────────────┬──────────────┘
                │
       ┌────────┼─────────┐
       ▼        ▼         ▼
    SQLite   Secure    Runtime
             Store      Policy
                         │
                         ▼
                     Git Shim
                         │
                         ▼
                     Real Git
```

------

# 58. 为什么推荐 Rust

本项目需要：

- GUI 本地程序；
- Process 管理；
- Git Shim；
- Credential Helper；
- Windows API；
- DPAPI；
- 文件权限；
- SSH Key 管理；
- SQLite；
- 安全处理敏感数据。

Rust 可以让：

```text
GUI Backend
Git Shim
Credential Helper
```

共用大量代码。

最终可以形成：

```text
gitbind.exe
git.exe
gitbind-credential.exe
```

三个本地二进制。

------

# 59. 推荐 Workspace

```text
gitbind/
│
├── apps/
│   └── desktop/
│       ├── src/
│       └── src-tauri/
│
├── crates/
│   ├── gitbind-core/
│   ├── gitbind-database/
│   ├── gitbind-git/
│   ├── gitbind-policy/
│   ├── gitbind-security/
│   └── gitbind-platform/
│
├── binaries/
│   ├── gitbind-shim/
│   └── gitbind-credential/
│
├── migrations/
│
├── docs/
│
└── README.md
```

------

# 60. Core Service 划分

## AccountService

负责：

```text
Create Account
Update Account
Delete Account
Set Default
Validate Account
```

------

## ProjectService

负责：

```text
Add Repository
Remove Repository
Detect Repository
Locate Repository
```

------

## BindingService

负责：

```text
Bind
Rebind
Resolve Binding
Test Binding
```

------

## PolicyService

负责：

```text
Resolve Effective Account
Global Mode
Generate Runtime Policy
```

------

## GitService

负责：

```text
Git detection
Repository detection
Remote parsing
ls-remote
push dry-run
Git process execution
```

------

## CredentialService

负责：

```text
Password
Token
SSH Passphrase
Secure Store
Credential Helper
```

------

# 61. Effective Account Resolver

核心函数建议设计为：

```text
resolve_effective_account(repository_path)
```

逻辑：

```text
if globalMode.enabled:
    return globalMode.account

project = findManagedProject(repository_path)

if project exists:
    return project.boundAccount

return None
```

`None` 表示：

```text
Pass Through Native Git
```

------

# 62. Global Mode 状态转换

## OFF → ON

```text
读取 Default Account
        ↓
设置 global_account_id
        ↓
global_mode = true
        ↓
刷新 runtime policy
```

------

## Global Account Change

```text
Company A
    ↓
Company B
    ↓
刷新 runtime policy
```

不修改任何 Project Binding。

------

## ON → OFF

```text
global_mode = false
        ↓
刷新 runtime policy
        ↓
Repository Binding 自动重新生效
```

无需恢复任何 `.git/config`。

------

# 63. 不建议直接修改 Repository `.git/config`

虽然：

```text
git config --local
```

可以实现仓库身份绑定，但本项目不建议把它作为核心机制。

原因：

```text
会污染仓库本地配置
全局模式需要反复覆盖
关闭全局模式还需要恢复
存在恢复失败风险
多个工具可能相互修改
```

推荐：

```text
Binding DB
+
Git Shim
+
Command Level Override
```

实现非侵入式账号绑定。

------

# 64. 可选兼容模式

后续可以增加：

```text
Materialize Binding
```

把 GitBind Binding 真正写入：

```text
.git/config
```

用于不经过 Shim 的第三方客户端。

例如：

```ini
[user]
    name = Zhang San
    email = zhangsan@company.com
```

但该功能应：

```text
默认关闭
```

并明确提示属于：

```text
Compatibility Mode
```

而不是核心运行模式。

------

# 65. 系统设置

Settings 页面：

```text
Git

Real Git
C:\Program Files\Git\cmd\git.exe

Git Shim
✓ Enabled

Shim Path
C:\...\GitBind\shim\git.exe


Storage

Database
C:\...\gitbind.db

Keys
C:\...\keys


Behavior

[x] Start minimized
[ ] Start with Windows
[x] Confirm Global Mode
[x] Show Global Mode warning


Logs

[ Open Logs ]
[ Clear Logs ]
```

------

# 66. 数据备份

支持：

```text
Export Configuration
```

默认导出：

```text
Accounts metadata
Projects
Bindings
Settings
```

禁止默认导出：

```text
Password
Token
Private Key
Passphrase
```

可后续提供：

```text
Encrypted Full Backup
```

需要用户设置独立 Backup Password。

------

# 67. MVP 范围

v1.0 建议只实现核心需求。

## Account

- Account CRUD
- Alias
- Default Account
- Commit Name
- Commit Email
- SSH Private Key
- HTTPS Credential

## Project

- Add Repository
- Remove Repository
- Detect Remote
- Refresh
- Repository List

## Binding

- Repository → Account
- Rebind
- Default Binding
- Test Binding

## Git

- Git Shim
- SSH Account Injection
- HTTPS Credential Helper
- Commit Identity Injection

## Global

- Global Mode
- Global Account Selection
- Runtime Policy

## Security

- SQLite
- Windows Credential Manager / DPAPI
- Key ACL
- Log Redaction

------

# 68. 暂不进入 v1.0

以下功能建议后续迭代：

```text
GPG Signing
Remote-level Binding
Account Auto Matching
Repository Auto Scan
Repository Group
Bulk Binding
Tray Account Switcher
Git Clone
Git GUI Commit
Git Diff
Git Branch Manager
Git History
Cloud Sync
Team Configuration
```

GitBind 第一阶段不要发展成：

```text
SourceGit
GitKraken
Sourcetree
```

这样的完整 Git Client。

核心定位必须保持：

> Git Repository Account Binding Manager。

------

# 69. 后续可扩展功能

## Auto Binding Rule

例如：

```text
Remote contains:
github.com/company/

        ↓

Company Account
```

或者：

```text
Path:
D:\Company\**

        ↓

Company Account
```

------

## 批量导入仓库

选择：

```text
D:\Projects
```

递归扫描：

```text
.git
```

自动添加 Repository。

------

## Tray Mode

Windows 托盘：

```text
GitBind
├── Global Mode: OFF
├── Enable Global Mode
│
├── Personal
├── Company A
└── Company B
```

方便快速切 Global Account。

------

# 70. 状态提示

应用需要区分：

```text
Configured Account
Effective Account
```

例如：

```text
Project:
zx_wechat_api

Bound Account:
Company-A

Effective Account:
Company-B

Reason:
Global Mode
```

防止用户混淆。

------

# 71. Binding 页面建议增加 Effective Account

例如：

| Project   | Bound Account | Effective Account | Status          |
| --------- | ------------- | ----------------- | --------------- |
| Project A | Personal      | Company-A         | Global Override |
| Project B | Company-A     | Company-A         | Global Override |
| Project C | Company-B     | Company-A         | Global Override |

Global Mode 关闭：

| Project   | Bound Account | Effective Account | Status |
| --------- | ------------- | ----------------- | ------ |
| Project A | Personal      | Personal          | Normal |
| Project B | Company-A     | Company-A         | Normal |
| Project C | Company-B     | Company-B         | Normal |

------

# 72. 异常处理

必须覆盖以下情况：

### Real Git 不存在

```text
Git executable not found
```

引导重新选择。

### SSH Key 不存在

```text
SSH private key missing
```

Binding 状态变：

```text
Invalid
```

### Default Account 被禁用

禁止禁用。

必须先修改默认账号。

### Global Account 被删除

Global Mode 开启时禁止删除当前 Global Account。

### Repository 不存在

状态：

```text
Missing
```

### Remote 不存在

允许作为 Local Repository 保存，但：

```text
Network Test = Unavailable
```

------

# 73. Account 与 Host 冲突

如果账号：

```text
Platform = GitHub
Host = github.com
```

项目：

```text
Remote Host = codeup.aliyun.com
```

绑定时提示：

```text
⚠ 当前账号 Host 与 Repository Remote Host 不匹配。

Account:
github.com

Repository:
codeup.aliyun.com

仍然绑定？
```

允许用户覆盖。

因为 Custom SSH Alias 场景无法仅根据 Host 完全判断。

------

# 74. 性能目标

Git Shim 是所有 Git 调用的入口，因此性能要求较高。

目标：

```text
Policy Resolve
< 10 ms
```

普通：

```bash
git status
```

额外开销尽量：

```text
< 20 ms
```

Shim 不连接数据库服务器。

不启动 GUI。

不进行网络请求。

------

# 75. 可靠性原则

GitBind 发生故障时：

```text
Git 本身仍应尽可能可用。
```

例如 Runtime Policy 损坏：

```text
Global Mode 无法解析
```

默认行为：

```text
Fail Safe
→ Passthrough Real Git
```

而不是：

```text
阻止所有 Git 操作
```

但同时 stderr 输出：

```text
GitBind warning:
runtime policy unavailable,
falling back to native Git configuration.
```

------

# 76. 全局模式安全策略

Global Mode 风险较高。

开启时建议二次确认：

```text
开启 Global Mode？

所有经 GitBind 管理的 Git CLI 操作
将强制使用：

Company-A

原有仓库绑定关系将被临时忽略。

[ Cancel ]
[ Enable ]
```

顶部持续显示：

```text
GLOBAL MODE
```

禁止只通过不明显的小图标表示。

------

# 77. 验收标准

## Requirement 1：纯离线

必须满足：

- 无后端服务；
- 无登录；
- 无在线 API；
- 无遥测；
- 无自动更新检查；
- 无 CDN；
- GUI 关闭网络后仍可完成全部管理操作。

------

## Requirement 2：账号管理

必须满足：

- 添加账号；
- 编辑账号；
- 删除账号；
- Alias；
- Alias 默认 Username；
- Username；
- Commit Name；
- Commit Email；
- SSH Private Key；
- HTTPS Credential；
- 设置 Default Account；
- Secret 不明文存储。

------

## Requirement 3：项目管理

必须满足：

- 添加现有 Git Repository；
- 校验 Repository；
- 自动读取 Remote；
- 删除管理记录；
- 不删除源码；
- 项目状态检查。

------

## Requirement 4：绑定管理

必须满足：

```text
One Project → One Account
```

新项目：

```text
→ Default Account
```

支持：

```text
Rebind
Test
Read Authentication Test
```

------

## Requirement 5：Global Mode

必须满足：

```text
Global Mode ON
        ↓
Selected Account
        ↓
Ignore Repository Binding
        ↓
Command-Level Override
```

关闭：

```text
立即恢复 Repository Binding
```

且整个过程：

```text
不修改原 Binding 数据。
```

------

# 78. 核心业务规则汇总

最终需要严格遵循：

```text
RULE 01
一个 Project 只能绑定一个 Account。

RULE 02
一个 Account 可以绑定多个 Project。

RULE 03
所有新 Project 默认绑定 Default Account。

RULE 04
系统最多只有一个 Default Account。

RULE 05
正常模式下 Managed Project 使用 Bound Account。

RULE 06
正常模式下 Unmanaged Repository 不受 GitBind 干预。

RULE 07
Global Mode 优先级最高。

RULE 08
Global Mode 不修改任何 Project Binding。

RULE 09
Global Mode 关闭后 Binding 自动重新生效。

RULE 10
Commit Identity 与 Authentication Identity 均由 Account 管理。

RULE 11
SSH Remote 使用 Account SSH Key。

RULE 12
HTTPS Remote 使用 Account HTTPS Credential。

RULE 13
Secret 禁止以明文保存到 SQLite。

RULE 14
GitBind GUI 不需要常驻运行。

RULE 15
所有策略由 Git Shim 在执行 Git 时动态解析。
```

------

# 79. 最终架构

```text
                         ┌────────────────────┐
                         │    GitBind GUI     │
                         └─────────┬──────────┘
                                   │
             ┌─────────────────────┼─────────────────────┐
             │                     │                     │
             ▼                     ▼                     ▼
          Accounts              Projects              Settings
             │                     │                     │
             └─────────────┬───────┘                     │
                           ▼                             │
                        Binding                          │
                           │                             │
                           └───────────┬─────────────────┘
                                       ▼
                                Runtime Policy
                                       │
                                       ▼
                                   Git Shim
                                       │
                     ┌─────────────────┴─────────────────┐
                     │                                   │
              Global Mode ON                      Global Mode OFF
                     │                                   │
                     ▼                                   ▼
              Global Account                     Managed Project?
                                                         │
                                                ┌────────┴────────┐
                                                │                 │
                                               YES               NO
                                                │                 │
                                                ▼                 ▼
                                         Bound Account       Passthrough
                                                │                 │
                                                └────────┬────────┘
                                                         ▼
                                                    Real Git
                                                         │
                                      ┌──────────────────┴──────────────────┐
                                      │                                     │
                                     SSH                                  HTTPS
                                      │                                     │
                              Private SSH Key                    Credential Helper
                                      │                                     │
                                      └──────────────────┬──────────────────┘
                                                         ▼
                                                    Git Server
```

------

# 80. 项目最终定位

GitBind 不应定位成：

> 一个新的 Git GUI Client。

而应该定位成：

> **一个纯本地、以 Repository → Account Binding 为核心的 Git Identity & Credential Manager。**

核心竞争点不是：

```text
Commit
Diff
Branch
Merge
```

而是：

```text
Account
   +
Credential
   +
Repository
   +
Binding
   +
Global Override
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

此后无论从：

```text
CMD
PowerShell
Git Bash
VS Code
IntelliJ IDEA
Android Studio
```

执行 Git，只要其 Git executable 经过 GitBind Shim，即可自动获得正确的：

```text
Commit Identity
+
SSH / HTTPS Authentication Identity
```

而不再需要开发人员手工切换 Git 账号。
