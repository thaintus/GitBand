# 隔离 UI 运行验证

此项目用于人工明确要求运行验证时，加载真实已编译的 Avalonia 页面和样式，生成真实运行时渲染图，并运行少量窗口/控件交互断言。它不加入主解决方案，不会因普通 `dotnet test` 自动弹出窗口。

## 运行

在仓库根目录、具有可交互 Windows 桌面的环境执行：

```powershell
dotnet build tests/GitBinder.UiSmoke/GitBinder.UiSmoke.csproj -c Release -p:NuGetAudit=false --ignore-failed-sources
dotnet tests/GitBinder.UiSmoke/bin/Release/net10.0/GitBinder.UiSmoke.dll
```

默认输出 `tests/GitBinder.UiSmoke/artifacts/`。也可向第二条命令传一个输出目录参数；该目录必须是专用验证目录。重跑会覆盖同名 PNG 与 results.txt，不会清空目录。输出已由仓库 `artifacts/` 忽略规则排除。

仅需当前界面截图时，增加 `--screenshots-only` 参数，可同时传入新的输出目录。此模式只加载并截取五页面的中英双语、两种尺寸界面，保留隔离状态与页面加载检查，跳过搜索、分组编辑、结果弹窗及账号编辑的交互验证；默认完整验证流程不变。

宿主会短暂显示真实验证窗口，遍历页面及弹窗后自动退出。请勿在验证期间操作这些窗口；如需中断，可在启动终端按 Ctrl+C，仅停止本验证进程。不要启动/关闭用户已经安装的软件，也不要结束其他 `dotnet` 进程。

## 覆盖与边界

- 使用实际 App.Initialize 加载真实编译后的资源、样式、ViewLocator；独立重写启动方法且不调用产品启动逻辑，前后断言 App.Services 仍为 null。
- 五页面、中文/英文、1180×780 与 960×640 视口，共 20 张基础渲染图；另附筛选、分组、结果与账号编辑弹窗图。
- 断言界面使用同一个语言服务实例，并验证既有主窗口文案在中英切换及切回中文时立即刷新。
- 真实 TextBox 绑定输入过滤、分组新增/空名校验/重命名/取消、结果正文选择、确认取消、账号新建窗打开/取消。
- 通过控件事件及 ViewModel 命令驱动，不向系统注入鼠标/键盘操作。结果正文只断言选择范围，**不覆盖系统剪贴板**。
- 账号、仓库、分组、设置、Secret、Git 配置和传输全部为内存 Fake；路径和邮箱均为演示数据。不读取 `%LOCALAPPDATA%/GitBinder`，不创建真实数据库、不调用 DPAPI、不访问网络、Git 或用户项目。
- `results.txt` 记录本次 PASS/FAIL；退出码 0 表示断言通过，非 0 表示失败。运行时 Avalonia 警告可写入 `avalonia.log`（没有日志时该文件可能不存在）。截图仍需人工查看确认视觉效果。
- 这不是安装测试、真实账号认证/拉取测试、系统托盘测试或操作系统级键鼠/剪贴板验证；不替代独立 xUnit 用例。
