# 原神启动 V2.13 验证记录

记录日期：2026-11-05。正式程序集版本2.13.0.0。本版修正DeepSeek/GPT6A跑步序列，并新增珂洛连续四组双击主动换装。

正式构建、23组完整回归、末轮素材调整后三组复测、资源一致性检查、动画审看、正式EXE启动及发布校验全部通过。末轮视觉复核将龙女、小肥鱼跑步整体分别缩至首轮的97%、95%，以衔接保留的慢走；本记录的资源、构建和发布哈希均对应最终素材。

## 检查范围

- 资源包仍为208项，仅两位AI的8张`walk-*`跑步帧改变，其余200项与V2.12逐字节相同，尤其慢走`stroll-*`不得改变。完整重建与定向刷新应相同。
- 连续预览应检查领先腿交换、相反过渡姿态、表情稳定及受控体高，不能只依赖帧数或字节不相同。
- `AiCharactersTests`覆盖两位AI、两套步态、左右方向和四个相位；真实`OnTick`选择正确图片，变换只有ScaleX为±1、ScaleY为1的镜像。
- 四组有效双击共八次点击，支持WPF ClickCount 1–8及四组1/2；组间1.5秒边界、超时、拖拽、额外单击、本子命中和跨角色均需覆盖。
- 每组双击仍扣1分钟冷却；主动换装不修改自动开关，地面立即演出，空中/门户延后到安全支撑，新操作及生命周期取消手动请求。自动180秒规则保留。
- 原有笔记、窗口能力、移动、滑翔、夜魂、蹬墙跳、资讯、预测、战斗助手等回归继续保留。

## 最终验证状态

| 项目 | 最终状态 | 证据 |
| --- | --- | --- |
| 正式构建 | PASS，末轮素材调整后重建，程序集2.13.0.0 | `artifacts/v213/build-console.log`及`startup-smoke.json` |
| 23组完整回归 | PASS，2026-11-05 08:54:22–08:55:24（UTC+08:00） | `artifacts/v213/full-test-run.json`与完整控制台日志 |
| 末轮素材调整后三组复测 | PASS，AiCharacters、Motion、Gait | `artifacts/v213/final-asset-test-run.json` |
| 8张跑步替换及200项保留 | PASS，208项完整重建一致，慢走原字节不变 | `artifacts/v213/asset-verification.json` |
| 跑步动画与腿部审看 | PASS，连续预览、腿部放大及与慢走头脸/躯干比例对照完成 | `artifacts/v213/run-art-verification.json`、`legs-review.png`、`run-vs-stroll-face-torso.png`与动画预览 |
| 正式EXE启动 | PASS，最终EXE观察3秒无新增崩溃日志 | `artifacts/v213/startup-smoke.json` |
| 发布内容与哈希 | PASS，2026-11-05 09:02:01.702（UTC+08:00） | `artifacts/v213/package-verification.json`、`release-hashes.json` |

最终素材包SHA256：`3d3eaea72831d16b31e89e186cd8a2a1b90ded0b8d5bd7f4d5ee4eb2effdd0d9`。8张跑步帧确实替换，其余200项与V2.12逐字节一致；完整重建的208项也与定向刷新一致。

最终裁切配方对龙女使用源图缩放0.95545，小肥鱼0.9215，各角色四帧统一比例与身体锚点，保留原RGBA。龙女4帧体高377–384px，脚底y479–485；小肥鱼体高374–376px，脚底y482–485。连续预览及腿部放大已人工检查领先腿交换和相反过渡姿态；未用逐帧拉齐脚底的方式抹去跑步起伏。源图、提示词与配方分别为`assets/generated/ai-runs-v213.png`、`ai-runs-v213-prompts.md`、`ai-runs-v213-crops.json`。

完整回归从2026-11-05 08:54:22.707至08:55:24.706（UTC+08:00），23组全部PASS，其中AiCharacters 299项、PointerInteraction 230项、KoloEasterEgg 328项、Notebook 122项。联调发现的门户测试混用真实屏幕与模拟地板，以及指针测试两次读取真实光标受用户移动影响的问题，均已在测试夹具中隔离，保留原断言。之后只调整跑步素材的整体比例，09:01:26.647–09:01:32.816又复测受影响的AiCharacters、Motion、Gait三组并全部PASS，其中AiCharacters 299项、Gait 286项。

## 发布文件

发布校验记录时间为2026-11-05 09:02:01.702（UTC+08:00）。ZIP只含`原神启动.exe`和`使用说明.txt`，解包内容与最终构建及`docs/usage-v2.13.txt`逐字节一致；说明文件8299字节，SHA256为`2adc6b398020ed0934a00ed050597e9ceba0341e12be61c36d4c5da6677a1b51`。

| 文件 | 字节数 | SHA256 |
| --- | ---: | --- |
| `release/V2.13/原神启动.exe` | 43560448 | `23941289d16e7e31d45d54cfcae193e1e99fa3fcac0489264cc2295fc79737df` |
| `release/V2.13/原神启动-V2.13.zip` | 43138426 | `a2118a73709b8c5ae29c885587b947d6cd6c62aa2127d046f5286773520d8316` |
| `assets/characters.zip` | 43062014 | `3d3eaea72831d16b31e89e186cd8a2a1b90ded0b8d5bd7f4d5ee4eb2effdd0d9` |

历史`release/V2.12/原神启动-V2.12.zip`保持43101226字节及SHA256 `9e732676e80b420c818a5d999d9bacc2cf2d84535be4d350851a982f8ee94fab`，未被覆盖。

```powershell
.\build.cmd
.\test.cmd -SkipPreviews -IncludeVisualSmoke
.\package.cmd -Version 2.13 -InstructionsPath .\docs\usage-v2.13.txt
```

发布包为`release/V2.13/原神启动-V2.13.zip`，预览独立保存到同目录`previews/`，不混入运行包。稳定证据保存在`artifacts/v213/`，后续测试覆盖`artifacts/tests/latest-run.json`不会改动本次记录。

## 覆盖界限

已进行连续跑步预览、腿部放大与慢走比例对照的人工审看，以及可控时钟、WPF路由事件和离屏渲染验证。四组双击和180秒自动资格通过可控输入测试确认；未另行进行真实等待180秒或物理鼠标在不同DPI双屏间拖拽的手测。帧选择测试用于确认运行时，不单独作为画中双腿自然交替的证明。
