# TopAwake

轻量级 PowerToys 替代品，只保留两个功能：**窗口置顶** 和 **防休眠 (Awake)**。

- 单文件 exe，不包含 .NET 运行时（本机需安装 .NET 9 Desktop Runtime）
- 无需安装、无需管理员权限、无后台服务
- 常驻内存约 50 MB（PowerToys 全套通常 300+ MB）

## 功能

### 窗口置顶

- 按 `Win + Ctrl + T` 切换当前窗口"始终置顶"（快捷键与 PowerToys 一致）
- 置顶成功显示蓝色边框闪烁，取消置顶显示灰色边框
- 桌面、任务栏等系统窗口自动跳过

### 防休眠 (Awake)

右键托盘图标 → **防休眠 (Awake)**：

- **关闭**：恢复正常电源策略
- **一直保持唤醒**：无限期阻止系统睡眠
- **定时唤醒 30 分钟 / 1 小时 / 2 小时**：到时自动恢复（悬停托盘图标可查看剩余时间）
- **保持屏幕常亮**：勾选后同时阻止关屏；取消勾选则只阻止系统睡眠、允许关屏（更省电）

双击托盘图标可在"关闭 / 一直唤醒"之间快速切换。唤醒状态下托盘图标右上角显示橙色圆点。

### 其他

- **开机自启动**：托盘菜单勾选（写入 HKCU Run 注册表项）
- 设置保存在 `%APPDATA%\TopAwake\settings.json`
- 单实例运行，重复启动不会开多个

## 使用

直接运行 `TopAwake.exe`，图标出现在系统托盘（蓝色"顶"字图标）。

> 若要置顶以管理员身份运行的窗口（如任务管理器），TopAwake 也需要以管理员身份运行。

## 从源码构建

构建需要 .NET 9 SDK；运行需要安装 .NET 9 Desktop Runtime（不随程序打包）：

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

产物为 `publish\TopAwake.exe`。
