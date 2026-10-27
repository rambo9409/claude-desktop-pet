# 原神启动 V2.5 验证记录

日期：2026-10-27。生产版本：2.5.0.0。

## 修复范围

甘雨的路线规划允许在相邻显示器交界两侧主动借力，交界仍是可穿越区域。单个窗口旁也能向外蹬出、转身返回更高的位置，不再强制至少两次交替碰壁；优先选择较短的可达路线。新增同一支撑面上的小跑靠近，到位后重新规划。移动中的失败重试间隔缩短为1.2秒。

相邻、同高的屏幕地面与同一窗口屋顶分段可连续行走；不同底边高度不直接水平穿过。目标可见屋顶按实际身体高度检查，落脚验证检查完整身体的容纳范围，并允许浮点坐标误差。完整窗口几何、遮挡和桌面联合区域检查保留。拖拽、隐藏、换角色、调整大小及移动模式均清理靠近路线；自动路线在关闭自动活动或开启安静模式后取消。目标变化时停止靠近，空中路线失效时展开滑翔翼。

纯规划使用80毫秒搜索预算，靠近搜索共享110毫秒预算，避免复杂桌面上长时间占用界面线程；这是检查点预算，不代表硬实时保证。通常固定测试场景仅需数毫秒。

## 双屏回归

测试使用本机读取到的工作区尺寸：主屏2560×1400、副屏1920×1160，副屏起点X=2560，两屏底边相差240像素。

独立42组场景包括交界两侧、单墙、距离接触点30像素、悬空窗口、较短副屏与上下错位屏幕；每组覆盖100%、125%、150%几何转换和负坐标排列。使用V2.4备份源码重新编译的对照中，42组均无法规划所需路线，记录在 `artifacts/v25/dual-screen-v24-baseline.txt`。

新版测试从实际 `TryStartGanyuJump` 进入，逐帧运行 `UpdateMovement`，检查位置连续、身体处于桌面范围、未穿窗口以及最终落脚。补充固定DIP角色、靠近后起跳、目标消失取消、同高双屏双向行走和跨屏窗口屋顶分段等检查。

## 动画和启动

`artifacts/v25/dual-screen-jump.gif` 包含134张真实WPF角色渲染帧，演示从主屏较低地面借力上升，跨过交界后落到副屏窗口顶部。背景与路线辅助线为离线场景；角色运动和姿态来自生产代码。已检查中途弹出、跨屏与最终落脚帧。发布预览位于 `release/V2.5/previews/`。

最终EXE通过3秒启动检查，按测试PID找到“原神启动 · 甘雨”窗口，产品名和2.5.0.0版本正确，没有新增崩溃日志。随后仅关闭该测试进程，记录在 `artifacts/v25/startup-smoke.json`。测试在首次施法等待结束前完成。

角色素材沿用原版，资源ZIP SHA-256：`932eae3140ea536e134b60d59f1420955d818ffb58fb52ef3f88dbe55aa6ccf4`。V2.4发布ZIP保持原哈希：`67b2e1fb35f1dd15da3c02940b8ffd865946e7d39c92c263428b7fe6b0247b12`。

双屏与路线测试采用离线窗口几何，没有操作用户窗口。原生窗口能力回归只操作测试自建窗口。涉及配置的测试保存并恢复原值。

## 最终回归结果

16组全部通过，最终清单保存在 `artifacts/v25/full-test-run.json`，逐组日志位于 `artifacts/tests/`。构建无警告。

| 测试 | 结果 |
| --- | --- |
| GanyuDualScreen | 61989项；42组双屏场景和完整运动、靠近、取消、分段落脚检查，479毫秒 |
| GanyuTraversal | 4158项；18组缩放与负坐标路线26毫秒、靠近搜索3毫秒、16窗口场景7毫秒 |
| DesktopMovement | 45454项 |
| Effects / Gait / Wings | 66 / 284 / 3316项 |
| GlideFall / Nightsoul | 62745 / 1542项 |
| WindowAbility / PointerInteraction | 333 / 120项 |
| Highlights / GptReset | 47 / 94项 |
| MenuTheme / Motion / SpeechLayout / VisualSmoke | 四角色主题、动作、固定词条位置、10栏看点交互与布局全部通过 |

逐帧检查数随随机小跑速度略有变化，上述为本次最终运行值。耗时为本机固定场景的测量值。

## 发布文件

- ZIP：`release/V2.5/原神启动-V2.5.zip`，25,455,898字节，仅含EXE与使用说明。
- EXE：25,667,072字节；工程根目录与发布副本哈希一致。
- ZIP SHA-256：`283191edca1b0dc8b89dc9ab9c88cd947317b61f8457de9220fde1820de700bd`
- EXE SHA-256：`8a925d2fde99a4b10625e14f0c800ce1fc40a8658214893d8f33d566965f2f12`

## 复现命令

```powershell
& ./scripts/build.ps1
& ./scripts/test.ps1 -Suite DesktopMovement,Effects,Gait,MenuTheme,Motion,SpeechLayout,Wings,Highlights,GlideFall,Nightsoul,WindowAbility,GptReset,PointerInteraction,GanyuTraversal,GanyuDualScreen,VisualSmoke
& ./artifacts/tests/GanyuDualScreenTests.exe --preview
& ./scripts/package.ps1 -Version 2.5 -InstructionsPath './docs/usage-v2.5.txt'
```
