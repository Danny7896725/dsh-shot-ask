# 贡献指南

感谢你愿意改进「截图提问助手」！这个项目刻意保持了很小的技术栈：**不引入任何运行时依赖**，
所以贡献门槛很低，但也意味着有几条约定必须遵守。

## 开发环境

| 部分 | 需要什么 |
| --- | --- |
| 浮窗（`src/`） | 只要 Windows 自带的 `csc.exe`；**不需要** .NET SDK |
| 插件（`plugin/`） | 纯 ESM JavaScript，无 npm 依赖；Node.js ≥ 18 用于语法检查 |
| 联调 | 一个正在运行的 DSH（`dsh web`） |

编译：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

直接打接口（不用开浮窗）：

```bash
node scripts/test-ask.mjs docs/demo-slide.png "这页在讲什么"
```

## 代码约定

**C#（`src/`）**

- 目标框架是 .NET Framework 4.8，编译器是系统自带的 `csc.exe`，**只支持 C# 5 语法**。
  不要用 `?.`、字符串插值、表达式体成员、`record`、`switch` 表达式等新语法——本地编译不过。
- 布局全部手写（没有设计器文件），改动界面请同时考虑 **150% / 200% DPI** 下的缩放
  （`MainForm.ScaleAll`）。
- 新增界面文字一律中文，保持 `Theme` 里的配色与字号。

**JavaScript（`plugin/`）**

- 不引入第三方依赖；只使用 DSH 提供的 `ctx.llm`、`ctx.webServer`、`ctx.attachments`、`ctx.agentDefaultModel`。
- 接口保持 SSE：`meta` / `delta` / `done` / `error` 四种事件，字段名不要随意改（浮窗依赖它们）。
- 修改后至少跑一次 `node --check plugin/index.js` 和 `scripts/test-ask.mjs`。

## 提交前自检

- [ ] `build.ps1` 编译无 error
- [ ] 热键截图 → 选区 → 提问 → 流式回答 全流程可用
- [ ] 折叠 / 展开 / 缩放 / 托盘菜单正常
- [ ] 150% 缩放下界面不重叠、不溢出
- [ ] 没有把个人截图、`config.json`、`dist/` 提交进来

## 提交信息

采用简化版 Conventional Commits：

```
feat: 支持多屏快捷选择
fix: 修复 200% 缩放时回答区溢出
docs: 补充端口配置说明
refactor: 抽出截图编码逻辑
```

## Pull Request

1. 先开 Issue 描述问题或想法，避免大改动白做；
2. 一个 PR 只做一件事，附带截图或录屏说明界面变化；
3. 涉及界面改动的 PR，请同时提供 100% 与 150% 缩放下的截图。
