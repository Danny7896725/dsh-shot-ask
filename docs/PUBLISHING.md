# 第一次发布到 GitHub

仓库已经在本机 `git init` 并完成首次提交，下面分「用 GitHub Desktop」和「用命令行」两条路，任选一条。

## 发布前检查（必做）

- [ ] `dist/`、`config.json`、`startup.log` 没有被提交（`.gitignore` 已处理，但要确认一眼）
- [ ] 没有个人桌面截图的测试文件
- [ ] `LICENSE` 里的版权署名换成你的名字或 ID
- [ ] 找个人照 README 走一遍安装流程，最容易发现「只有你能装」的问题

## 路线 A：用 GitHub Desktop（推荐，不用命令行）

GitHub Desktop 自带 Git，不需要额外安装。

1. 打开 **GitHub Desktop**，确认右上角已登录你的账号。
2. 菜单 **File → Add Local Repository…**，选择本文件夹。
   如果提示「这不是一个 Git 仓库」，点 **create a repository** 并保持路径不变。
3. 左侧切到 **Changes**，确认文件列表里**没有** `dist/`、`config.json`、`startup.log`。
4. 右上角点 **Publish repository**：
   - Name：`dsh-shot-ask`
   - Description：`看 PPT 时框选屏幕区域，直接向 DeepSeek Harness 提问的 Windows 悬浮窗`
   - **取消勾选**「Keep this code private」
   - 点 **Publish repository**
5. 打开仓库网页，点 **About** 旁的齿轮，补描述与 Topics：
   `deepseek`、`dsh-plugin`、`screenshot`、`ppt`、`windows`、`csharp`。
6. 发版（创建 tag 会触发自动构建）：
   仓库页右侧 **Releases → Draft a new release** →
   **Choose a tag** 输入 `v0.1.0` → 点 **Create new tag: v0.1.0 on publish** →
   标题填 `v0.1.0`，描述写「需要 DSH ≥ 0.2.2，模型需支持图片输入」→ **Publish release**。
7. 去仓库 **Actions** 页确认 `build` 工作流通过；通过后 Release 里会自动多出一个 zip。

> 日常更新：在 Desktop 里改完代码 → 填 Summary → **Commit to main** → **Push origin**。
> 发新版本重复第 6 步，把 tag 换成 `v0.1.1`。

## 路线 B：用命令行

```powershell
winget install --id Git.Git -e --source winget
winget install --id GitHub.cli -e --source winget
```

重开终端后，在项目根目录：

```powershell
git init -b main
git add .
git status                      # 重点检查有没有个人截图、config.json
git commit -m "feat: 截图提问助手 v0.1.0"
gh auth login
gh repo create dsh-shot-ask --public --source=. --remote=origin --push
git tag v0.1.0
git push origin v0.1.0          # 触发自动发版
```

## 发布之后

- **About / Topics**：决定别人能不能搜到你，别跳过。
- **Releases**：确认 zip 已附带，说明里写清兼容的 DSH 版本。
- **第一周**：每天看一次 Issue，回复越快项目越容易活下来。
- **Roadmap**：把高频需求记成 issue，而不是每个都立刻实现。

## 发布文案

首发渠道按性价比排序：

1. **GitHub**：仓库描述 + Release 说明写清「给谁用、解决什么、怎么装」。
2. **V2EX**：节点「分享创造」，标题带痛点，正文放 3 张图 + 安装步骤。
3. **少数派 / 即刻 / 小红书**：偏效率工具视角，30 秒 GIF 最有效。
4. **HelloGitHub**：中文开源项目投稿。
5. **B 站**：1 分钟实操视频（热键 → 框选 → 提问 → 答案）。

示例开头：

> 看网课、看 PPT 时想问 AI，总是要截图 → 切窗口 → 粘贴 → 打字。
> 我做了个 Windows 小浮窗：`Ctrl+Alt+Q` 框选屏幕任意区域，直接在浮窗里提问，
> 答案流式打出来。截图只发本机，模型复用你已登录的 DeepSeek Harness。
