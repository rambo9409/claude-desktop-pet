# 原神启动 V2.11 验证记录

记录日期：2026-11-04。本版新增珂洛拖拽松手后的空中传送、重做持本/空手慢走，并加入待机换装DeepSeek彩蛋。正式程序集版本为2.11.0.0。

正式构建、23组完整回归、启动、资源比较和发布包检查全部完成并通过。最终顺序回归起止为 `2026-11-04T18:14:17.5463707+08:00` 至 `2026-11-04T18:15:17.3879185+08:00`，22组默认回归加VisualSmoke共23组全部PASS，记录为 `artifacts/v211/full-test-run.json`，控制台记录为 `artifacts/v211/full-test-console.log`。

## 已完成的定向回归

`artifacts/v211/final-targeted-test-run.json`记录2026-11-04 17:41:21–17:41:31（UTC+08:00）的四项套件全部PASS：PointerInteraction、KoloPortal、KoloEasterEgg、Notebook。该记录独立保存，不会被下一次测试覆盖。

测试宿主修复后，18:03:02–18:03:16再次运行AiCharacters（219项）、Notebook（122项）、KoloPortal（18409项）、KoloEasterEgg（671项）和VisualSmoke，五项全部PASS，记录为 `artifacts/v211/remaining-host-test-run.json`。

- 传送覆盖空中稳住、开门进入、在当前屏幕底部出现、收门、重复抓取及生命周期取消；仅由拖拽释放触发，近地释放走正常落地，自然掉落沿用旧物理。
- 模拟显示器覆盖负坐标、工作区边界、屏幕消失与大小变化。真实WPF路由事件覆盖传送中按住本子暂停、拖离未松手保持暂停、松手后继续，并检查鼠标捕获和本子状态。
- 换装覆盖启动/触发冷却、连续闲置资格、每30秒一次抽签、取消、收回本子、真实角色切换及设置保存。放大、缩小和恢复默认大小均立即取消，继续推进时间也不会延迟切换角色。
- 换装使用正式8帧WPF截图，人物全程穿着完整衣服，以不透明布料遮挡过渡。末帧与正式DeepSeek待机图一致。截图在 `artifacts/v211/easter-egg/`，门户截图在 `artifacts/v211/portal/`。

## 素材检查

`artifacts/v211/asset-verification.json`为PASS：正式资源208项（207张PNG和1份JSON），替换8张持本/空手慢走图、更新4个本子命中框、新增8张换装图，其余191项与V2.10逐字节一致；完整重建的208项也与正式包一致。资源ZIP SHA-256为 `7283f192b453b966f9e92eb9e0768d5e7e61b7e0555811bc9c261d619d7444a4`。

`artifacts/v211/kolo-art-verification.json`记录8张慢走图、8张换装图和4个新命中框。慢走底线统一为486，当前8帧有效高度398–399像素；源图采样空隙alpha为0，末张换装PNG与DeepSeek正式待机PNG相同。生成源图、提示词和裁切配方分别保留在 `assets/generated/kolo-stroll-v211.png`、`assets/generated/kolo-disguise-v211.png`、`assets/generated/kolo-v211-prompts.md` 和 `build_kolo_v211.py`。预览与素材测量图保存在 `artifacts/v211/`。

## 全套与发布状态

最终23组完整回归中，KoloPortal通过18409项、KoloEasterEgg通过671项、AiCharacters通过219项、Notebook通过122项、Wings通过3316项，VisualSmoke也通过。

旧同步测试宿主没有Application.Run消息循环，离屏渲染时未及时处理WPF Dispatcher队列，Wings、AiCharacters及CombatRoster曾出现同类回调异常。18个含离屏绘图的测试入口现已统一使用 `tests/WpfTestRender.cs`：在RenderTargetBitmap渲染前后以Background优先级处理队列，`scripts/common.ps1`编译测试时自动包含，生产EXE不包含此类。保留所有预览帧和断言，不吞异常、减少覆盖或改变正式程序渲染，统一修复后的23组已全部通过。AiCharacters也增加异常退出时的用户角色设置恢复，避免原生WPF回调异常绕过正常finally而留下测试角色。历史诊断记录保留在 `artifacts/v211/`。

最终生产构建已完成，日志为 `artifacts/v211/build-console.log`。正式EXE启动检查为PASS：版本2.11.0.0，启动后观察3秒，找到本进程桌宠窗口，无新增崩溃日志；记录为 `artifacts/v211/startup-smoke.json`。

| 项目 | 当前状态 | 证据 |
| --- | --- | --- |
| 最终正式构建 | PASS | `artifacts/v211/build-console.log`、版本2.11.0.0 |
| 23组完整回归 | PASS | `artifacts/v211/full-test-run.json`，18:14:17–18:15:17（UTC+08:00） |
| 正式EXE启动 | PASS | `artifacts/v211/startup-smoke.json`，观察3秒无新增崩溃日志 |
| 发布ZIP内容 | PASS | `artifacts/v211/package-verification.json`，仅EXE和使用说明，与构建及文档哈希匹配 |
| EXE与ZIP哈希 | PASS | `artifacts/v211/release-hashes.json`，V2.10历史ZIP哈希不变 |

```powershell
.\build.cmd
.\test.cmd -SkipPreviews -IncludeVisualSmoke
.\package.cmd -Version 2.11 -InstructionsPath .\docs\usage-v2.11.txt
```

发布包为 `release/V2.11/原神启动-V2.11.zip`，预览独立放在该版本的 `previews/` 目录。ZIP仅包含 `原神启动.exe` 与 `使用说明.txt`，内容分别与生产构建和 `docs/usage-v2.11.txt` 一致。

| 文件 | 字节数 | SHA-256 |
| --- | ---: | --- |
| `release/V2.11/原神启动-V2.11.zip` | 43336066 | `2b8c2a3ac11a9f02d6881543fd716067a503bda084ea1bc4816999b0a692ab0c` |
| `release/V2.11/原神启动.exe` | 43738624 | `2a9171cb7fecff56a4f7a8dfa129dbf427554eb129ef1f250ac4a7047f6436aa` |

V2.10历史发布ZIP SHA-256仍为 `12818f2751e200e86dcfda0fad45f0840bc4d1ba3f4b8a58c268972eb9d0ee99`，没有覆盖旧发布。

## 覆盖范围与限制

WPF渲染、纯状态/几何检查和路由事件测试覆盖已列出的行为。没有完成物理鼠标跨不同DPI双屏拖拽、实际拔插显示器的手测；模拟布局不能替代这些实测。测试会备份并还原涉及的用户设置，窗口能力测试只使用测试自建窗口，不对用户其他应用施法。

`-SkipPreviews`只跳过历史GIF重新编码，仍执行WPF渲染和断言。概率测试使用假时钟与固定随机值，不代表真实运行中一定在某个具体时刻触发彩蛋。
