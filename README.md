# GitBind

> 让每个仓库，用对账号。

面向 **Windows** 的本地 Git 身份与凭据管理工具。把个人、公司和不同托管平台的账号集中管理，为每个仓库绑定合适的提交身份与认证凭据，减少反复修改 Git 配置的麻烦。

**Windows x64 · SSH / HTTPS · 中文 / English · Apache-2.0**

[下载与安装](#下载与安装) · [快速上手](#快速上手) · [工作原理](#工作原理) · [常见问题](#常见问题) · [参与贡献](#参与贡献)

## 为什么需要 GitBind

同一台电脑上，你可能同时维护个人 GitHub 项目、公司的云效仓库，以及其他 Git 服务上的代码。GitBind 帮你把“这个仓库应该用哪个账号”变成明确、可查看、可切换的配置。

它专注于**账号、仓库和绑定关系**，不是新的 Git 图形客户端：提交、拉取、推送、分支和合并仍由你熟悉的终端或 IDE 完成。

> [!IMPORTANT]
> **提交署名不等于远程权限。** `user.name` 和 `user.email` 决定提交记录中的作者信息；远程服务器根据 SSH Key 或 HTTPS 凭据决定是否允许访问。GitBind 不会替你创建平台账号，也不会授予或撤销服务器上的权限。

## 主要功能

| 功能 | 可以做什么 |
| --- | --- |
| 多账号管理 | 维护账号别名、提交姓名与邮箱、默认账号、HTTPS 凭据和 SSH 私钥 |
| 项目绑定 | 登记本机已有 Git 仓库，选择账号后立即改绑，并查看“绑定账号”和“实际生效账号” |
| 即时搜索 | 账号、项目、平台页面支持输入即过滤已有数据，不区分大小写，清空后恢复完整列表 |
| 全局模式 | 临时使用统一账号，写入 Git 全局配置并同步覆盖已绑定仓库；关闭后恢复原全局配置及各项目绑定 |
| 远程地址维护 | 修改仓库 `origin`，在常规 SSH / HTTPS 地址之间快捷转换 |
| 连接测试 | 使用当前生效配置读取远程引用，辅助排查认证问题 |
| 平台管理 | 维护平台名称与 Host，支持自建服务、启用与禁用；已启用平台优先显示 |
| 托盘与界面 | 默认关闭窗口后驻留托盘，可在设置中调整；支持简体中文与 English |
| 删除保护 | 账号、项目、平台和 SSH 私钥移除入口统一为右上角 `×`，并提供二次确认 |

## 下载与安装

### 使用安装包

在仓库的 [Releases 页面](https://github.com/thaintus/GitBand/releases)查看发布记录和附件，选择 `GitBinder-<版本号>-setup.exe`。如果尚无发布附件，可按下方的[源码构建](#源码构建)说明自行生成。

- 当前发行目标为 **Windows x64**；暂未提供 macOS / Linux 支持。
- 安装包自带 .NET 运行时，使用者无需单独安装 .NET SDK 或 Runtime。
- 需要已安装系统 Git；使用 SSH 地址时还需要可用的 OpenSSH。
- 安装向导需要管理员权限，支持选择安装目录及创建快捷方式。
- 当前打包流程未配置代码签名。请核对下载来源；若发布页提供 SHA-256，可与本地文件哈希比对。

安装目录与用户数据目录相互独立。升级前请从托盘菜单退出旧版本，再运行安装程序。

### 快速上手

1. **检查环境**：打开“设置”，确认 Git / SSH 路径；需要时点击“重新检测”。
2. **添加账号**：进入“账号”，填写提交姓名、邮箱和所属平台，按需要填写 HTTPS 用户名与凭据，或导入 SSH 私钥。平台要求 Token 时，请使用相应平台签发的 Token。
3. **登记仓库**：进入“项目”并点击“添加仓库”，选择本机已有 Git 仓库。已有启用的默认账号时，新登记项目会尝试自动绑定。
4. **选择账号**：在项目卡片中选择“绑定账号”，选择后即应用，无需额外保存。全局模式开启时，实际生效账号仍以全局账号为准。
5. **确认连接**：点击账号框旁的“测试”，再回到终端或 IDE 执行自己的 Git 操作。

数据较多时，可使用账号、项目、平台页面顶部的搜索框。输入部分名称、邮箱、路径、远程地址或主机等对应字段即可过滤；搜索只作用于已加载数据，不发起远程查询，也不会修改记录。

> “测试”会重新应用当前生效账号的配置，并执行 `git ls-remote origin`。它会连接远程服务器，但不会提交或推送代码；测试成功只说明可以读取远程引用，不代表拥有推送权限，公开仓库也可能允许匿名读取。

## 工作原理

### 有效账号与 Git 配置

应用内部的账号选择顺序为：

```text
全局模式账号 > 项目绑定账号 > 默认账号
```

GitBind 通过仓库本地 Git 配置、Git 全局配置与 `gitbinder-credential.exe` 配合系统 Git 工作，**当前没有 Git Shim，也不需要替换系统 `git.exe`**。

- 普通模式下，为项目应用 `user.name`、`user.email` 及与 Remote 协议对应的认证配置；首次应用前保存相关配置快照。
- 解绑会恢复保存的本地配置。移除项目会先执行解绑，再删除登记记录，**不会删除本地源码或 Git 仓库**。
- 默认账号用于应用内的默认选择和新项目自动绑定，不等于普通模式下接管所有未登记仓库。
- 终端或 IDE 需要使用遵循这些配置的 Git。工具自己的凭据管理、环境变量或命令行覆盖参数可能改变最终行为。

### 全局模式

开启全局模式时，会保存并修改当前 Windows 用户的 Git 全局 `user.name`、`user.email`、`core.sshCommand`、`credential.helper`，同时对已绑定仓库应用全局账号。

关闭后恢复原全局配置，并重新应用各项目原有绑定。全局模式不会把项目的绑定关系改成全局账号。

未登记仓库仍遵循 Git 的配置优先级：如果它已有本地身份或认证设置，这些设置可能覆盖全局设置。因此，全局模式不是对所有 Git 工具和仓库的强制拦截。

### SSH 与 HTTPS

账号可以同时录入 SSH 与 HTTPS 认证信息，**实际使用哪一种由项目的 `origin` 地址决定**，不是按填写顺序或固定优先级选择。

```text
SSH    git@github.com:your-org/your-repo.git
HTTPS  https://github.com/your-org/your-repo.git
```

在“修改地址”中使用协议切换按钮，只会改变编辑框内容；点击“保存地址”后才会写入仓库。自定义端口等非标准地址需要手动填写。

修改地址不会搬迁远程代码或自动更换绑定账号。迁移托管平台后，请确认目标仓库已准备好、地址和绑定账号正确，再点击“测试”刷新并验证认证配置。

> [!WARNING]
> 平台分类不是权限隔离边界，GitBind 允许跨平台绑定账号。只向可信的 Remote 使用账号凭据，尤其是在开启全局模式或修改 HTTPS 地址时：当前按账号固定的 HTTPS Helper 不会以账号所属平台的 Host 限制凭据返回。

## 数据与安全

账号和项目管理不依赖自建后端，不需要注册 GitBind 云端账号；本地管理可离线进行，远程连接测试以及你自己的拉取、推送仍需要网络。当前未接入遥测或自动更新服务。

Windows 用户数据默认位于：

```text
%LOCALAPPDATA%\GitBinder\
├── gitbinder.db   # 账号、项目、绑定、配置快照及机密引用等元数据
├── secrets\      # Windows DPAPI CurrentUser 加密后的机密
└── keys\         # 导入的 SSH 私钥文件副本
```

- HTTPS 密码 / Token 通过机密存储加密保存，数据库记录其引用，不把这些凭据写进 Git 配置。
- **SSH 私钥是导入文件的副本，不是 DPAPI 加密文件。** 请保护私钥和数据目录的访问权限，不要把它们提交到仓库。
- DPAPI 密文与当前 Windows 用户上下文相关，不要把直接复制数据目录当作可靠的跨用户、跨设备凭据迁移方案。
- 退出应用不会撤销已经写入的 Git 配置。卸载前，建议先关闭全局模式，并解绑需要交回系统管理的仓库，避免配置继续指向被卸载的凭据组件。
- 反馈问题时请先脱敏：不要上传 Token、密码、私钥、真实用户数据库或含机密的完整 Git 配置。

## 常见问题

### 为什么切换了 GitHub 账号，仍能向云效提交或推送？

本地 `commit` 的作者信息和远程 `push` 的认证是两件事。把账号分类设为 GitHub 不会自动撤销它在其他服务器上的权限。如果实际使用的密钥或凭据也被云效接受，推送仍可能成功。请同时核对项目的 Remote、实际生效账号和服务器端授权。

### 能直接克隆仓库吗？

当前项目页用于登记本机已有仓库。请先用 Git 或 IDE 克隆，再添加到 GitBind；GitBind 不负责代码托管、克隆向导或提交历史管理。

### 关闭窗口后为什么还在运行？

默认行为是隐藏到系统托盘。可从托盘菜单选择“退出 GitBind”，或在设置中关闭托盘驻留选项。关闭窗口或退出进程都不等于关闭全局模式。

### 项目目录移动了怎么办？

当前不提供“重新定位”。移除旧登记后重新添加新路径；应用不会替你移动或删除代码。目录已移动时，请检查新位置的实际 Git 配置，不要假定旧路径的配置快照已成功恢复。

## 源码构建

### 开发环境

- Windows、.NET 10 SDK、系统 Git；SSH 相关功能还需 OpenSSH。
- 生成安装包时需要 Inno Setup 6，并确保其 `Languages` 目录包含 `ChineseSimplified.isl`。
- 首次还原依赖需要能够访问 NuGet。

```powershell
git clone https://github.com/thaintus/GitBand.git
cd GitBand

dotnet build GitBinder.slnx -c Debug
dotnet test GitBinder.slnx -c Debug --no-build
```

也可以使用仓库脚本完成还原、构建和测试：

```powershell
.\build\build.ps1
```

### 本地运行与发布

完整的 HTTPS 认证需要桌面程序与 `gitbinder-credential.exe` 位于同一目录。推荐用发布脚本同时生成两个程序，再启动桌面程序；仅对 Desktop 执行 `dotnet run` 不会自动准备凭据组件。

```powershell
# 版本号为示例，可按本次发布计划调整
.\build\publish.ps1 -Version 1.0.13
.\build\publish\app\GitBinder.Desktop.exe
```

发布输出位于 `build/publish/app/`；发布脚本会重新生成该临时目录。

> [!CAUTION]
> 开发版与安装版默认共用当前 Windows 用户的数据目录。应用启动时会重新应用已有绑定及全局模式，不是隔离的演示环境。涉及真实 Git 的开发验证请使用专用 Windows 测试用户和临时仓库；集成测试必须隔离 `GIT_CONFIG_GLOBAL`，不要修改开发机真实全局配置。

### 生成安装包

```powershell
# 同时发布 Desktop 与 Credential Helper，再生成 Windows x64 安装包
.\build\build-installer.ps1 -Version 1.0.13
```

输出为 `dist/GitBinder-1.0.13-setup.exe`。请为新发行版指定新的版本号，避免覆盖同名历史产物。该命令负责发布和打包，**不执行单元测试**。

如需串联构建、测试和打包：

```powershell
.\build\build.ps1 -Configuration Release -Package -Version 1.0.13
```

## 项目结构

项目使用 C# / .NET 10、Avalonia、CommunityToolkit.Mvvm、SQLite，以及系统 Git / OpenSSH。

```text
GitBinder.slnx
├── src/
│   ├── GitBinder.Desktop/           # 窗口、页面、ViewModel、本地化与组合根
│   ├── GitBinder.Application/       # 用例编排与端口接口
│   ├── GitBinder.Domain/            # 实体、领域规则与有效账号决策
│   ├── GitBinder.Infrastructure/    # Git CLI、SQLite 等外部能力实现
│   ├── GitBinder.Platform/          # Windows 路径与 DPAPI 等平台适配
│   └── GitBinder.CredentialHelper/  # Git Credential Helper 可执行程序
├── tests/GitBinder.Tests/          # 单元测试、Fake 与隔离的 Git 集成测试
├── build/                         # 构建、发布和 Inno Setup 脚本
├── aBase/                         # 设计、迭代及 Schema 参考资料
└── LICENSE                        # Apache License 2.0
```

核心依赖方向为 `Desktop → Application → Domain`。Infrastructure 和 Platform 实现端口，通过依赖注入组装；Domain 不依赖 UI、数据库或平台 API。

## 项目文档

| 文档 | 内容 |
| --- | --- |
| [设计资料索引](aBase/doc/设计文档索引.md) | 设计入口与已落地决策 |
| [产品设计](aBase/doc/GitBind设计文档.md) | 产品目标、业务模型和设计背景，包含尚未落地的方案 |
| [技术架构](aBase/doc/TECH_ARCHITECTURE.md) | 分层、存储、本地化及安全设计 |
| [界面视觉规范](aBase/doc/UI_VISUAL_GUIDELINES.md) | 配色、布局及交互约定 |
| [迭代记录](aBase/iteration/V1-首版开发.md) | 实际变更、验证边界和安装包记录 |
| [数据库资料](aBase/sql/README.md) | Schema 与增量 SQL 参考；运行时以初始化及迁移代码为准 |
| [文档维护规则](aBase/README.md) | aBase 目录职责和维护方式 |

## 参与贡献

欢迎通过 [Issues](https://github.com/thaintus/GitBand/issues)反馈问题、提出建议，或提交 Pull Request。

- **反馈缺陷**：提供应用版本、Windows / Git 版本、使用协议、全局模式状态、复现步骤与脱敏后的错误信息。
- **提出功能**：先说明使用场景；较大的改动建议先开 Issue 沟通，保持项目聚焦于身份与凭据管理。
- **提交修改**：保持职责分层，补充相关测试；用户可见文案同时维护中文和英文，并按 [aBase 规则](aBase/README.md)更新迭代记录。
- **保护隐私**：不要提交真实账号数据、密钥、凭据、个人 Git 配置，以及 `bin/`、`obj/`、`build/publish/` 等生成产物。安装包适合作为 Release 附件分发。
- **说明验证范围**：在 PR 中注明实际执行过的检查与测试；静态检查、单元测试和真实远程认证不能互相替代。

涉及安全问题时，请勿在公开 Issue 中粘贴敏感信息或可直接利用的细节。

## 许可证

本项目采用 **Apache License 2.0**，完整条款见 [LICENSE](LICENSE)，标准文本来自 [Apache Software Foundation](https://www.apache.org/licenses/LICENSE-2.0.txt)。第三方依赖及其资源仍适用各自的许可证。
