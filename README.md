# AnimeGames Bar

AnimeGames Bar 是一个 WinUI 3 桌面状态栏，用来集中查看常用二游账号状态、自动刷新数据，并按设置执行每日签到。

## 当前版本

- 当前开发版本：v1.2
- v1.0 稳定点：明日方舟、明日方舟：终末地支持
- v1.1：新增鸣潮支持
- v1.2：新增异环支持
- 数据来源：森空岛、库街区、塔吉多等官方应用接口

## 功能

- 顶部滑块切换游戏。
- 每个游戏独立保存账号凭据。
- 每个游戏可独立开启/关闭自动刷新，并设置独立自动刷新频率。
- 启动时可自动签到，并通过 Windows 通知显示结果。
- 每日 00:01 可执行自动签到。
- 设置页支持明暗模式、启动时自动签到、每日自动签到、手动签到范围、Windows 通知、开机自启。
- 支持 Server 酱外部通知，当前与 Windows 通知触发时机同步。

## 已支持游戏

### 明日方舟

- 理智与回满时间。
- 无人机与回满时间。
- 训练室干员、技能、剩余时间、完成时间。
- 订单进度、制造进度、干员疲劳。
- 每周剿灭。
- 保全派驻数据增补仪、数据增补条与刷新时间。
- 森空岛签到。

### 明日方舟：终末地

- 理智。
- 每日活跃度。
- 每周事务。
- 通行证等级。
- 森空岛签到。

### 鸣潮

- 结晶波片。
- 结晶单质。
- 每日活跃度。
- 周度游历。
- 战歌重奏次数。
- 先约电台等级。
- 逆境深塔周期重置时间。
- 冥歌海墟周期重置时间。
- 终焉矩阵结束时间。
- 库街区游戏签到。

鸣潮使用库街区 APP Token 接入。可以点击“登录”通过库街区移动端短信验证码获取 Token，也可以在鸣潮页的账号信息栏手动填写“库街区 Token”。

### 异环

- 本性像素。
- 都市活力。
- 活跃度。
- 周本次数。
- 塔吉多 App 签到。
- 异环游戏签到。

异环使用塔吉多账号接入。可以点击“登录”通过塔吉多短信验证码获取 Access Token，也可以在异环页手动填写塔吉多 Access Token 和 Refresh Token。

## 安全说明

- 凭据默认保存在 Windows Credential Locker。
- 如果 Credential Locker 不可用，会写入 `%LOCALAPPDATA%\AnimeGamesBar` 下的 DPAPI 加密备用文件。
- 不要提交真实 token、cookie、调试 dump 或包含账号敏感信息的截图。
- 不要提交 Server 酱 SendKey。
- `.refs/` 是本地参考仓库目录，不应提交到 GitHub。

## 构建

需要 Visual Studio 2022、.NET 8、Windows App SDK。

```powershell
dotnet build -c Release -p:Platform=x64
```

运行 Release 版：

```powershell
.\src\AnimeGamesBar.App\bin\x64\Release\net8.0-windows10.0.19041.0\AnimeGamesBar.App.exe
```

## 项目结构

- `src/AnimeGamesBar.App`：WinUI 3 桌面应用。
- `src/AnimeGamesBar.App/Services/Skland`：森空岛请求、凭据、签到。
- `src/AnimeGamesBar.App/Services/Arknights`：明日方舟与终末地数据适配。
- `src/AnimeGamesBar.App/Services/Kuro`：库街区与鸣潮数据适配。
- `src/AnimeGamesBar.App/Services/Tajiduo`：塔吉多与异环数据适配。
- `src/AnimeGamesBar.App/Services/Settings`：本地设置。
- `src/AnimeGamesBar.App/Services/Notifications`：Windows 通知。
- `src/AnimeGamesBar.App/Services/Startup`：开机自启。

## 更新日志

### v1.2

- 新增异环支持。
- 新增塔吉多短信验证码登录入口。
- 新增异环本性像素、都市活力、活跃度、周本次数展示。
- 新增塔吉多 App 签到与异环游戏签到。
- 新增 Server 酱外部通知渠道，暂时与 Windows 通知同步发送。
- 顶部游戏切换扩展为四段滑块。
- 异环自动刷新频率独立保存。
- 自动刷新开关与刷新频率均改为按游戏独立保存。

### v1.1

- 新增鸣潮支持。
- 新增库街区 Token 凭据保存、绑定角色读取、数据展示。
- 新增鸣潮库街区游戏签到。
- 新增鸣潮库街区移动端短信验证码登录入口，用于获取 APP Token。
- 自动刷新频率改为每个游戏独立设置。
- 手动签到默认改为当前页面游戏，并可在设置中切回全部游戏签到。
- 修复鸣潮周度游历和终焉矩阵时间解析。
- 鸣潮周度游历补充角色基础数据接口兜底，先约电台等级上限固定为 70。
- 自动签到改为启动时自动签到与每日 00:01 自动签到两个独立开关。
- 修复顶部“每 X 分钟刷新”文本垂直不居中。

### v1.0

- 支持明日方舟与明日方舟：终末地。
- 支持森空岛登录、凭据持久化、跨游戏独立账号。
- 支持理智、无人机、训练室、订单、制造、干员疲劳、剿灭、保全派驻等数据。
- 支持终末地理智、每日活跃度、每周事务、通行证等级。
- 支持森空岛自动签到、Windows 通知、明暗模式、开机自启。
