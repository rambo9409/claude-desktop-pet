# 原神启动 V2.10 验证记录

2026-11-04完成正式构建与21组回归，结果为PASS，记录在 `artifacts/v210/full-test-run.json`。本版按用户参考替换GPT6A龙女形象，并同步紫白菜单、淡紫专注星环与计时牌。

## 素材

正式资源仍为200项，其中199张PNG和1份本子命中框JSON。仅27张 `gpt6a-*.png` 被替换，其余173项与V2.9逐字节一致。完整从源图重建的200项也与正式包逐字节相同，记录在 `artifacts/v210/asset-verification.json`。

27个运行帧对应22个绘制姿态，表情过渡有复用。慢走、跑步、攀爬和施法分别含4个不同姿态，另有下落与被拖拽姿态。512×512画布、帧名、透明边界与图像范围检查通过，角、蝠翼和卷尾完整。逐帧数据在 `artifacts/v210/dragon-frame-verification.json`。

最初密集图集与一次去底重试存在明显半透明背景，未采用。正式使用三张分组图集与一张空中图。采样空隙alpha最高1–2/255，深浅底实际合成未见明显白雾或色块，提取过程未清除alpha或重染角色。原参考、正式源图、完整imagegen提示词和裁切配方保存在 `assets/source/`、`assets/generated/` 与 `build_gpt6a_v210.py`。

## 程序与界面

完整回归包括AiCharacters的219项、AiAbilities的76项和Notebook的122项。新形象沿用原动作键、专注计时及取消规则，珂洛本子与其他角色能力的回归均通过。菜单测试扩展至六角色，检查根菜单、子菜单、悬停、选中、禁用状态和文字对比度。

已目视核对正式资源在深浅背景下的动作预览，以及WPF渲染的GPT6A菜单和专注状态。预览放在 `release/V2.10/previews/`。测试使用路由事件、模拟场景及本程序自建窗口，未对用户其他应用执行窗口魔法，也不代表新增了物理鼠标双屏实测。

正式EXE版本为2.10.0.0。启动后观察3秒，找到本进程桌宠窗口且无新增崩溃日志，再正常关闭测试进程，记录在 `artifacts/v210/startup-smoke.json`。

## 发布

发布ZIP仅包含 `原神启动.exe` 和 `使用说明.txt`。解压内容与发布目录逐项哈希一致，检查记录为 `artifacts/v210/package-content-verification.json`。V2.9历史发布包哈希保持一致，完整哈希清单在 `artifacts/v210/release-hashes.json`。

| 文件 | 字节数 | SHA-256 |
| --- | ---: | --- |
| `release/V2.10/原神启动-V2.10.zip` | 41648340 | `12818f2751e200e86dcfda0fad45f0840bc4d1ba3f4b8a58c268972eb9d0ee99` |
| `release/V2.10/原神启动.exe` | 42038784 | `d07f0e1d450dde8d1ad7864ba5989c5966134716cae9df86f57e2560e1216675` |

```powershell
.\build.cmd
.\test.cmd -SkipPreviews -IncludeVisualSmoke
.\package.cmd -Version 2.10 -InstructionsPath .\docs\usage-v2.10.txt
```

本轮测试跳过历史蝶化和展翼GIF的重新编码。新龙女的动作GIF已由素材构建脚本生成，WPF静态渲染和断言均正常执行。
