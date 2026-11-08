# AutoFlow - 自动联动启动管理器

| 项目 | 内容 |
| --- | --- |
| 产品名 | AutoFlow |
| 中文名 | 自动联动启动管理器 |
| 英文全称 | AutoFlow Application Manager |
| 可执行文件 | `AutoFlow.exe` |
| 软件图标 | [app.ico](app.ico)（由桌面 `lg.png` 生成的多尺寸图标） |

## 1. 软件介绍

AutoFlow（自动联动启动管理器）是一个常驻 Windows 托盘的轻量级「程序联动」工具。它监听指定进程的启动与关闭事件，并按预设规则自动执行后续动作，用于把没有内置联动能力的软件串起来。

典型场景：

- 企业微信（WXWork.exe）启动后，延迟 5 秒自动拉起 ERP 批处理脚本。
- 企业微信关闭后，同步关闭记事本等相关程序。
- 启动某程序时隐藏命令行黑框、以管理员身份启动、目标已在运行则跳过。
- 延迟执行期间如果源程序已经退出，自动取消本次动作。

主要功能：

| 能力 | 说明 |
| --- | --- |
| 触发条件 | `start`（进程启动时）/ `stop`（进程关闭时），按进程名匹配，可附加完整路径精确校验 |
| 执行动作 | `launch` 启动程序 / `close` 关闭程序，同一条规则可配置多个动作 |
| 启动选项 | 参数、工作目录、延迟（毫秒）、隐藏窗口、以管理员身份启动、目标已运行则跳过、源进程退出则取消 |
| 关闭选项 | 优雅关闭（超时后强杀）、直接强杀、结束进程树 |
| 界面 | 规则列表（勾选即时启停）、添加/编辑/删除/复制、测试执行、暂停监听、运行日志面板 |
| 常驻能力 | 关闭窗口最小化到托盘、双击托盘恢复、开机自启、单实例运行、一键创建桌面快捷方式 |
| 易用性 | 现代化扁平界面；进程名输入框内置说明，提示以任务管理器「详细信息」中的名称为准 |
| 配置与日志 | XML 配置带备份与损坏隔离、按天滚动日志、日志保留天数可配置 |

程序运行后会在界面底部显示当前监听状态（WMI 事件 / 轮询兜底），并实时输出动作执行日志。

## 2. 运行环境

- **操作系统**：Windows 7 / 8 / 8.1 / 10 / 11（32 位与 64 位均可，程序以 AnyCPU 编译）。
- **运行时**：.NET Framework 4.5.2 或更高版本（Windows 10/11 默认自带，Win7/8 需自行安装）。
- **权限**：需要管理员权限。程序清单 [app.manifest](app.manifest) 中声明了 `requireAdministrator`，启动时会弹 UAC；原因是 WMI 进程事件订阅（`Win32_ProcessStartTrace`）需要管理员身份。
  - 未以管理员身份运行时程序仍可启动（例如清单被移除后），但会降级为轮询模式并提示用户，触发存在数秒延迟。
- **部署方式**：免安装，绿色拷贝。将 `bin` 目录下的 `AutoFlow.exe`、`AutoFlow.exe.config`、`AutoFlow.exe.manifest` 放在同一目录即可运行。
- **文件位置**：
  - 配置：默认 `AutoFlow.exe` 同目录的 `AutoFlow.config.xml`；若该目录不可写（如装在 `Program Files`），自动回退到 `%ProgramData%\AutoFlow\AutoFlow.config.xml`。
  - 日志：配置目录下的 `logs\yyyyMMdd.log`。
- **升级兼容**：从旧名称 AutoLauncher 升级时，若 `AutoFlow.config.xml` 尚不存在，程序会自动从 exe 目录或 `%ProgramData%\AutoLauncher\` 复制旧配置（旧文件保留不动）；创建开机自启时会顺带删除旧的 `AutoLauncher` 计划任务，避免重复自启。
- **其他**：无网络依赖，不访问任何外部服务；不写注册表（开机自启用 Windows 计划任务实现）。

## 3. 开发工具与开发环境配置

### 3.1 开发工具

- **操作系统**：Windows（构建脚本调用的编译器位于 `C:\Windows\Microsoft.NET\Framework(64)\v4.0.30319\csc.exe`）。
- **编译器 / 框架**：.NET Framework 4.5.2 及其自带 C# 编译器（C# 5 语法）。代码中未使用 C# 6 及以上语法（如字符串插值、`?.`），因此不依赖 Roslyn。
- **IDE（可选）**：
  - Visual Studio 2019 / 2022（需勾选「.NET Framework 4.5.2 目标包」或更高版本的 .NET Framework 开发工具），直接打开 [AutoFlow.csproj](AutoFlow.csproj) 即可。
  - 或 VS Code + C# 扩展，仅作为代码编辑器使用，构建交给 `build.ps1`。
- **PowerShell**：用于执行构建脚本。若脚本被策略拦截，使用 `-ExecutionPolicy Bypass` 运行。

### 3.2 环境配置要点

1. 安装 .NET Framework 4.5.2（或更高）Developer Pack，确保 `csc.exe` 与目标框架引用程序集存在。
2. 项目为传统的 .NET Framework 类库工程格式，引用以下系统程序集（见 csproj）：
   - `System`、`System.Core`、`System.Xml`
   - `System.Windows.Forms`、`System.Drawing`（WinForms 界面与图标）
   - `System.Management`（WMI 进程事件与进程路径查询）
3. 源码包含中文注释与中文字符串，全部以 **UTF-8** 保存；构建脚本使用 `/codepage:65001` 指定源码编码，避免中文乱码。
4. 调试权限：以管理员身份启动调试器（VS 需以管理员身份运行），否则无法验证 WMI 事件路径，只能走轮询兜底。
5. 运行界面需要桌面会话，不能作为 Windows 服务运行（涉及托盘图标与窗体交互）。

### 3.3 冒烟测试

[test/SmokeTest.cs](test/SmokeTest.cs) 是独立入口的控制台自测程序，覆盖进程名规范化、配置序列化往返、配置校验、进程探测与启动/关闭。由于它与 `Program.Main` 同属一个程序集，单独编译时必须显式指定入口：

```powershell
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$srcs = @(Get-ChildItem src -Filter *.cs | ForEach-Object FullName) + 'test\SmokeTest.cs'
& $csc /target:exe /main:SmokeTest /codepage:65001 /out:"$env:TEMP\af_smoke.exe" `
  /r:System.dll /r:System.Core.dll /r:System.Xml.dll /r:System.Drawing.dll `
  /r:System.Windows.Forms.dll /r:System.Management.dll $srcs
& "$env:TEMP\af_smoke.exe"
```

预期输出末行为 `ALL PASSED`。测试中的进程启停用例会真实启动并关闭 `ping.exe`。

## 4. 构建工具

项目不依赖 NuGet 包、MSBuild 扩展或第三方库，提供两种等效构建方式。

### 4.1 一键脚本（推荐）：build.ps1

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

脚本流程（[build.ps1](build.ps1)）：

1. 定位 `csc.exe`（优先 `Framework64`，不存在则回退 `Framework`）。
2. 收集 `src` 目录下全部 `*.cs`。
3. 以 `winexe`、`anycpu`、`optimize+`、`/codepage:65001` 编译，嵌入 [app.manifest](app.manifest) 与 [app.ico](app.ico)。
4. 输出到 `bin\AutoFlow.exe`。
5. 复制 `app.config` → `bin\AutoFlow.exe.config`，复制 `app.manifest` 与 `AutoFlow.config.sample.xml` 到 `bin`。

构建成功输出 `Build succeeded.`。

### 4.2 MSBuild / Visual Studio

```powershell
msbuild AutoFlow.csproj /p:Configuration=Release
```

注意：csproj 中的 `OutputPath` 为 `bin\Debug\` / `bin\Release\`，与 `build.ps1` 直接输出到 `bin\` 不同，二者产物相互独立。csproj 通过 `<Compile Include="src\*.cs" />` 通配收集源码，新增源文件无需改工程文件。

## 5. 项目目录结构介绍

```text
AutoFlow/
├─ src/                        # 全部源代码（命名空间 AutoFlow）
│  ├─ Program.cs               # 入口：单实例 Mutex、全局异常处理、日志初始化、启动主窗体
│  ├─ MainForm.cs              # 主窗体：规则列表/工具栏/日志面板/托盘图标/启停与暂停
│  ├─ RuleEditForm.cs          # 规则编辑对话框：触发类型、进程选择、动作列表
│  ├─ ActionEditForm.cs        # 动作编辑对话框：launch/close 两套参数面板
│  ├─ SettingsForm.cs          # 设置对话框：轮询兜底、托盘、开机自启、日志保留
│  ├─ Models.cs                # 配置数据模型（AppConfig / RuleConfig / TriggerConfig / ActionConfig）与 XML 映射
│  ├─ ConfigService.cs         # 配置读写、校验、规范化、备份与损坏隔离
│  ├─ TriggerEngine.cs         # 触发引擎：WMI 事件 + 轮询兜底 + 退出事件绑定
│  ├─ ActionExecutor.cs        # 动作执行器：延迟调度、取消条件、环路自抑制
│  ├─ ProcessUtil.cs           # 进程查找/启动/关闭、WMI 取进程路径、进程树结束
│  ├─ AutoStart.cs             # 开机自启（schtasks 计划任务，含旧任务清理）
│  ├─ AppPaths.cs              # 程序目录/配置路径/日志目录解析、写权限回退、旧配置迁移
│  ├─ AppIcon.cs               # 统一加载 exe 内嵌图标（窗体与托盘共用）
│  └─ LogService.cs            # 轻量按天滚动日志
├─ test/
│  └─ SmokeTest.cs             # 控制台冒烟测试（独立入口，不参与主程序构建）
├─ bin/                        # 构建输出目录
│  ├─ AutoFlow.exe             # 主程序
│  ├─ AutoFlow.exe.config      # 运行时配置（来自 app.config）
│  ├─ AutoFlow.exe.manifest
│  ├─ AutoFlow.config.xml      # 实际使用的用户配置（运行时生成/修改）
│  ├─ AutoFlow.config.sample.xml  # 示例配置
│  └─ logs/                    # 运行日志（按天一个文件）
├─ app.config                  # .NET 运行时配置（支持 v4.0 CLR）
├─ app.manifest                # 应用清单：requireAdministrator、DPI 感知、Win7~Win11 兼容、Common Controls v6
├─ app.ico                     # 软件图标（16/24/32/48/64/128/256 多尺寸）
├─ AutoFlow.config.sample.xml  # 配置示例，含 start/stop 两条演示规则
├─ AutoFlow.csproj             # VS/MSBuild 工程文件
├─ build.ps1                   # csc 一键构建脚本
└─ README.md                   # 本说明文档
```

## 6. 核心技术介绍

### 6.1 进程事件监听：WMI 为主，轮询兜底

- 主通道：[TriggerEngine.cs](src/TriggerEngine.cs) 使用 `System.Management` 订阅 `SELECT * FROM Win32_ProcessStartTrace`，进程创建时立即收到事件，延迟最低。
- 兜底通道：当 WMI 不可用（通常是权限不足）或用户开启「轮询兜底」时，按 `PollIntervalMs`（默认 3000 ms，最小 500 ms）对 `Process.GetProcesses()` 做全量快照，与上一次快照做 PID 差集，得到「新出现」与「已消失」的进程。首次快照只建立基线，不触发规则。
- 关闭事件：启动类规则在进程出现时立即触发；关闭类规则在进程出现时通过 `Process.EnableRaisingEvents` + `Exited` 事件按 PID 绑定，进程退出即触发。程序启动时还会调用 `BindExistingStopRules`，为已经运行中的目标进程补绑退出事件。
- 短命进程过滤：记录 PID 首次出现时间，存活不足 3 秒（`MinLifetimeSeconds`）的进程退出不视为「用户关闭程序」，避免 Win11 记事本等程序的辅助进程导致误触发。
- 线程安全：所有共享字典（停止监听表、退出钩子表、快照）由 `lock (_sync)` 保护，轮询使用 `Timer` 且用 `_polling` 标志防止重入。

### 6.2 触发匹配

- 进程名统一规范化：去空白、去路径（取文件名）、不区分大小写、自动补 `.exe`（`ConfigService.NormalizeProcessName`）。
- `TriggerConfig.FilePath` 非空时，再通过 WMI（`Win32_Process.ExecutablePath`，带重试）取进程完整路径做精确比对，实现同名不同路径进程的区分。

### 6.3 动作调度与执行

- 每条动作独立延迟：`DelayMs <= 0` 直接投递到线程池；大于 0 则创建一次性 `Timer`，并登记到列表以便退出时统一释放（[ActionExecutor.cs](src/ActionExecutor.cs)）。
- 串行执行：实际动作在 `_execLock` 内执行，避免多条规则同时命中时并发启动/关闭造成干扰。
- 取消条件：`CancelIfSourceExited` 对 `launch` 动作生效——延迟结束时若源进程 PID 已不存在则放弃执行；`close` 动作不适用该判断（源进程此时本就已退出）。
- 环路抑制：本程序拉起的进程名会被登记 10 秒「抑制窗口」，触发引擎通过 `IsSuppressed` 跳过，防止「A 启动 B、B 的规则又启动 A」的死循环。

### 6.4 进程启动与关闭策略

- 启动（`ProcessUtil.Launch`）：
  - `.bat` / `.cmd` 自动经 `%COMSPEC% /c` 转发，`RunHidden` 时不弹黑框；
  - `RunAsAdmin` 时改用 `UseShellExecute = true` + `Verb = "runas"` 触发 UAC（此路径无法隐藏窗口）；
  - `SkipIfRunning` 在启动前按目标文件名探测同名进程。
- 关闭（`CloseByName`）：默认先 `CloseMainWindow()` 优雅关闭，等待超时（默认 5000 ms）后 `Kill()` 强杀；`Force` 直接强杀；`KillTree` 调用 `taskkill /PID <pid> /T /F` 结束整棵进程树。

### 6.5 配置持久化与容错

- 基于 `XmlSerializer` 的强类型模型（[Models.cs](src/Models.cs)），属性全部显式标注 XML 元素/属性名，配置结构稳定可读。
- 写入采用「临时文件 + 替换」：先写 `*.xml.tmp`，成功后再删除旧文件并 `Move`，避免写入中断损坏配置；写入前额外备份为 `*.xml.bak`。
- 读取失败时把原文件改名为 `*.xml.broken.<时间戳>` 隔离，并生成默认配置继续运行。
- 加载后统一规范化：补齐规则 ID、默认设置值、进程名 `.exe` 后缀。
- `Validate` 区分错误与警告：名称为空、触发类型非法、动作路径缺失等为错误；目标程序不存在、触发条件重复、无动作等为警告。
- 旧版配置迁移在 [AppPaths.cs](src/AppPaths.cs) 首次解析路径时完成：仅在 `AutoFlow.config.xml` 不存在时复制 `AutoLauncher.config.xml`，不覆盖、不删除旧文件。

### 6.6 单实例与窗口唤起

- `Program.Main` 以命名 `Mutex`（`AutoFlow.SingleInstance.Mutex`）实现单实例。
- 第二次启动不再开新窗口，而是通过命名事件 `AutoFlow.SingleInstance.Show` 通知已运行实例；实例内后台线程 `WaitOne` 后用 `BeginInvoke` 回到 UI 线程恢复并激活窗口。

### 6.7 开机自启

- 使用 `schtasks.exe` 创建登录触发的计划任务：`/SC ONLOGON /RL HIGHEST /F`，任务名 `AutoFlow`。
- 相比注册表 `Run` 项，计划任务可以「最高权限」静默启动，避免登录时反复弹 UAC；启用/禁用即创建/删除该任务。
- 首次创建新任务成功后，会静默删除旧版 `AutoLauncher` 计划任务。

### 6.8 日志、路径与图标

- 日志（[LogService.cs](src/LogService.cs)）：按天写入 `logs\yyyyMMdd.log`（UTF-8），带级别过滤（Debug/Info/Warn/Error）与保留天数清理；所有写日志异常被吞掉，日志失败不影响主流程。
- 路径（[AppPaths.cs](src/AppPaths.cs)）：启动时在 exe 目录写临时文件探测可写性，不可写则整体回退到 `%ProgramData%\AutoFlow`（配置与日志一起回退），界面会提示实际配置位置。
- 图标（[AppIcon.cs](src/AppIcon.cs)）：统一通过 `Icon.ExtractAssociatedIcon` 读取 exe 内嵌图标，供主窗体、托盘与各编辑对话框共用；构建时由 `build.ps1` 的 `/win32icon` 嵌入 [app.ico](app.ico)。

### 6.9 界面与稳定性

- 纯代码构建 WinForms 界面，无 Designer 文件与 `*.resx` 资源，工程结构简单、便于脚本编译。
- 主窗体提供规则列表（复选框即时启用/停用并落盘）、工具栏、日志面板、状态栏、托盘菜单（显示、暂停、设置、打开目录、退出）。
- 全局兜底：`Application.ThreadException` 与 `AppDomain.UnhandledException` 统一记录日志；UI 线程更新通过 `BeginInvoke` 封送，避免跨线程访问控件。
