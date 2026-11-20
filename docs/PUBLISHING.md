# 第一次发布到 GitHub

这份清单面向第一次开源的人，按顺序做完就能得到一个能被人安装、能自动发版的仓库。

## 0. 发布前检查（别跳过）

- [ ] `test/` 里的真实桌面截图已删除或换成合成素材（**真实截图里有你的课件、浏览器标签、用户名**）
- [ ] 没有 `config.json`、`startup.log`、`dist/` 被误提交
- [ ] README 里的仓库地址占位符已替换成你自己的
- [ ] `LICENSE` 里的版权署名换成你的名字或 ID
- [ ] 找一个不装 DSH 的朋友，让他照 README 走一遍（最容易发现「只有你能装」的问题）

## 1. 装 Git 和 GitHub CLI

```powershell
winget install --id Git.Git -e --source winget
winget install --id GitHub.cli -e --source winget
```

装完**重开一个终端**，确认：

```powershell
git --version
gh --version
```

## 2. 在 GitHub 建仓库

用网页或命令行都行（网页更直观）：

```powershell
gh auth login          # 选 GitHub.com → HTTPS → 浏览器登录
gh repo create dsh-shot-ask --public --source=. --remote=origin --push
```

**不要**勾选「Add a README / .gitignore / license」，否则会和本地文件冲突。

## 3. 本地初始化并推送

在项目根目录（有 `README.md` 的那层）执行：

```powershell
git init
git add .
git status            # 重点检查：有没有 test/ 下的真实截图、config.json
git commit -m "feat: 截图提问助手 v0.1.0"
git branch -M main
git remote add origin https://github.com/<你的用户名>/dsh-shot-ask.git
git push -u origin main
```

## 4. 打第一个版本

```powershell
git tag v0.1.0
git push origin v0.1.0
```

推 tag 会触发 `.github/workflows/build.yml` 的 release 任务：自动编译 exe、打包 zip、创建 Release。
第一次跑大约 1–2 分钟，去仓库的 **Actions** 页看结果。

> 如果 Actions 失败，最常见原因是 `build.ps1` 在 runner 上找不到 `csc.exe` 或中文文件名。
> 先看日志里的具体步骤，再在本地用同样的命令复现。

## 5. 仓库设置（提升第一印象）

- **About**：填一句话描述 + 勾选 Topics：`deepseek`、`dsh-plugin`、`screenshot`、`ppt`、`windows`、`csharp`
- **Releases**：确认 v0.1.0 附带了 zip，Description 里写清楚「需要 DSH ≥ 0.2.2」
- **Issues**：确认模板生效（新建 Issue 时能看到 Bug / 功能建议）
- 开启 **Discussions**（可选，用于使用问答）

## 6. 发布文案

首发渠道按性价比排序：

1. **GitHub**：仓库描述 + Release 说明写清楚「给谁用、解决什么、怎么装」。
2. **V2EX**：节点「分享创造」，标题带痛点，正文放 3 张图 + 安装步骤。
3. **少数派 / 即刻 / 小红书**：偏「效率工具」视角，30 秒 GIF 最有效。
4. **HelloGitHub**：投稿邮箱见其官网，适合中文开源项目。
5. **B 站**：1 分钟实操视频（热键 → 框选 → 提问 → 答案）。

示例开头（可直接改）：

> 看网课、看 PPT 时想问 AI，总是要截图 → 切窗口 → 粘贴 → 打字。
> 我做了个 Windows 小浮窗：`Ctrl+Alt+Q` 框选屏幕任意区域，直接在浮窗里提问，
> 答案流式打出来。截图只发本机，模型复用你已登录的 DeepSeek Harness。

## 7. 发布之后

- 前两周每天看一次 Issue，回复越快，项目越容易活下来；
- 把高频问题补进 README 的 FAQ；
- 用 Roadmap issue 收集需求，而不是每个需求都立刻实现；
- 每发一版更新 `CHANGELOG.md`，并在 Release 里注明兼容的 DSH 版本。
