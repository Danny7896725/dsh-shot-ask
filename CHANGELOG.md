# 更新日志

本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)，格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

## [0.1.0] - 2026-11-20

### 新增

- 桌面浮窗：置顶、无边框、可拖动/缩放、折叠为小胶囊、托盘常驻。
- 热键 `Ctrl+Alt+Q` 全屏冻结 + 拖拽框选，支持多显示器；`Esc` 取消、回车/双击选全屏。
- 浮窗内提问，答案以 SSE 流式返回并做轻量 Markdown 渲染。
- 快捷提问：解释 / 总结 / 翻译 / 出题。
- 连续追问（默认携带最近 6 轮问答）、复制截图、复制回答。
- DSH 插件 `dsh-shot-ask`：`/shot-ask/ping` 与 `/shot-ask/ask`，复用当前默认模型，无需额外 API Key。
- 高分屏（150% / 200% 缩放）布局适配。
- 托盘菜单：显示浮窗、截图提问、始终置顶、开机自启动、打开配置目录、退出。

### 说明

- 仅支持 Windows 10/11；依赖 .NET Framework 4.8（系统自带）。
- 需要 DeepSeek Harness ≥ 0.2.2，且模型支持图片输入。
