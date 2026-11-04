# 原神启动 V2.12 验证记录

记录日期：2026-11-04。正式程序集版本为2.12.0.0。本版取消珂洛跑步，将换装改为累计可见使用180秒必定触发，统一DeepSeek记事窗主题，并重绘龙女整套资源以匹配另外两位AI。

正式构建、23组完整回归、资源比较、EXE启动及发布包检查均已完成并通过。最终顺序回归为 `2026-11-04T22:25:28.3018771+08:00` 至 `2026-11-04T22:26:25.9266004+08:00`，22组默认回归加VisualSmoke共23组全部PASS，记录为 `artifacts/v212/full-test-run.json`，控制台记录为 `artifacts/v212/full-test-console.log`。

## 功能与界面回归

定向6组在2026-11-04 22:24:26–22:24:41（UTC+08:00）全部通过，记录为 `artifacts/v212/targeted-test-run.json`：AiAbilities 88项、AiCharacters 219项、KoloEasterEgg 279项、Gait 286项，以及Motion和MenuTheme。

最终23组中这些套件再次通过，Notebook通过122项、KoloPortal通过18409项，其他原有移动、滑翔、夜魂、蹬墙跳、资讯、预测、战斗助手及VisualSmoke均PASS。

实际记事窗的正常、空态及430×450小尺寸WPF截图已检查，使用与DeepSeek其他工具相同的浅蓝主题，正文及保存按钮未被裁切。定向截图在 `artifacts/v28/abilities-222427034/deepseek-notes.png`、`deepseek-notes-empty.png`和`deepseek-notes-small.png`，最终全套再次生成于 `artifacts/v28/abilities-222610381/`。

## 资源与可复现性

`artifacts/v212/asset-verification.json`为PASS：正式包208项中27张龙女资源替换，181个其他条目与V2.11逐字节相同，完整重建的208项也与正式包一致。资源ZIP SHA-256为 `35e7ccb177575b9d7850c7bf06b42ef97bc5188293b70bdcaa92a9e83b367868`。

`artifacts/v212/dragon-art-verification.json`为PASS：27个运行帧采用512×512 RGBA画布，四边保持透明；对应22张不同PNG，部分表情复用正式姿态。源图只进行矩形裁切、等比缩放与画布定位，没有对生成alpha或颜色做滤镜处理。源图为 `assets/generated/gpt6a-v212-ground.png`、`gpt6a-v212-motion.png`，提示词与裁切配方为 `gpt6a-v212-prompts.md`、`gpt6a-v212-crops.json`，脚本为 `build_gpt6a_v212.py`。

预览包括 `artifacts/v212/gpt6a-v212-contact.png`、`gpt6a-v212-gait.png`、`gpt6a-v212-motion.gif`及三位AI对比图 `ai-v212-style-comparison.png`。保留白银发、淡紫眼、龙角、蝠翼、卷尾及白紫裙装，同时统一头身比例、线条与分层上色。

## 本版已覆盖行为

- 珂洛的菜单、自动移动、旧调用和遗留gait状态均使用慢走，持本与空手帧匹配；其他角色跑步保持。
- 彩蛋不再随机抽签。移动、安静模式中的可见使用计时，隐藏/最小化暂停，切换或重新开启开关重置，180秒阈值保持到期资格；拖拽、菜单、传送和施法结束后安全触发。普通移动停止后换装，中断保留资格，完成后正式切换DeepSeek。
- DeepSeek记事窗使用同角色工具的调色板、圆角外框、主题输入/历史选择/按钮/滚动条。窗口控制、430×450最小布局及保存/修改/删除失败后的草稿保留均通过实际WPF检查。
- 记事列表的Preview和UpdatedLabel只供显示，`ScriptIgnore`保证它们不进入存档。新关闭按钮沿用未保存确认，不绕过原保护。
- 继续使用共享 `WpfTestRender` 安全边界处理离屏绘图，不跳过帧、删断言或吞异常。

## 最终构建与发布

| 项目 | 当前状态 | 证据 |
| --- | --- | --- |
| 最终正式构建 | PASS | `artifacts/v212/build-console.log`、EXE版本2.12.0.0 |
| 23组完整回归 | PASS | `artifacts/v212/full-test-run.json`，22:25:28–22:26:25（UTC+08:00） |
| 正式EXE启动 | PASS | `artifacts/v212/startup-smoke.json`，找到本进程桌宠窗口、观察3秒无新增崩溃日志，并关闭测试启动的进程 |
| 发布ZIP内容 | PASS | `artifacts/v212/package-verification.json`，仅EXE和使用说明，内容与生产构建及usage文档逐字节一致 |
| 发布与历史哈希 | PASS | `artifacts/v212/release-hashes.json`，当前EXE/ZIP字节数及SHA-256，历史V2.11 ZIP保持不变 |

```powershell
.\build.cmd
.\test.cmd -SkipPreviews -IncludeVisualSmoke
.\package.cmd -Version 2.12 -InstructionsPath .\docs\usage-v2.12.txt
```

发布包为 `release/V2.12/原神启动-V2.12.zip`，预览独立存放在同目录 `previews/`。正式包仅含 `原神启动.exe` 与 `使用说明.txt`，内容分别与生产构建和 `docs/usage-v2.12.txt` 一致。最终证据已保留在 `artifacts/v212/`，不要依赖会被后续测试覆盖的latest-run。

| 文件 | 字节数 | SHA-256 |
| --- | ---: | --- |
| `release/V2.12/原神启动-V2.12.zip` | 43101226 | `9e732676e80b420c818a5d999d9bacc2cf2d84535be4d350851a982f8ee94fab` |
| `release/V2.12/原神启动.exe` | 43522560 | `017cc5de541766c05e7e60c3d875e7b85590f9c153920fea5b1ec4bb178b8f66` |

历史V2.11 ZIP SHA-256仍为 `2b8c2a3ac11a9f02d6881543fd716067a503bda084ea1bc4816999b0a692ab0c`，未覆盖旧发布。

## 覆盖范围与限制

WPF离屏渲染、路由事件和模拟几何不能替代物理鼠标跨不同DPI双屏的拖拽或实际拔插显示器手测。当前尚未完成这些实机操作。`-SkipPreviews`仅跳过历史GIF重新编码，不跳过WPF渲染与断言。

180秒阈值与延期触发使用可控时钟及真实WPF入口验证，没有另行运行一次等待180秒的人工实机观察。最终触发仍服从拖拽/传送等安全边界；隐藏、最小化及休眠或UI阻塞超过5秒的间隙不计入使用时长。进度不跨程序重启保存。
