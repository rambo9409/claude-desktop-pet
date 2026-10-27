# 原神启动 V2.4 验证记录

日期：2026-10-27。生产版本：2.4.0.0。构建无警告。

## 完成的行为

珂洛保留5分钟窗口能力冷却。有效双击减少60秒，最低归零，立即保存并反馈剩余时间；已就绪时不积攒减免。主循环与双击共用同一个能力控制器，双击处理本身不枚举或操作窗口。WPF提供系统双击时间和位置规则，输入跟踪额外排除真实拖拽，并将连续点击按2、4、6等不重叠的点击对处理。空中不移动鼠标的抓放也可算点击，同时保留原有放手下落行为。

甘雨不再进入爬墙状态，优先规划至少两次碰壁借力的多段跳跃，在屏幕边和窗口侧边、或两扇窗口侧边之间交替向上，最后落在可见屋顶。每次碰壁停留90毫秒压腿，随后换向蹬离。无走廊时可进行直接短跳；没有安全终点则站稳、折返或滑翔。屏幕步行触边点已提前至身体能容纳的位置，不靠瞬移调整起点。

路线校验可见窗口、真实几何、桌面联合区域与跨屏接缝。背景最大化窗口不堵死前景小窗口旁的通道，前景最大化窗口仍遮挡后面的墙面和落脚处。目标移动、消失、遮挡或路线中断后转滑翔；清除路线后禁止恢复旧单跳逻辑。

## 检查结果

15组全部通过。最终运行：北京时间11:01:36–11:02:30。日志在 `artifacts/tests/`，本版运行清单另存为 `artifacts/v24/full-test-run.json`。

| 范围 | 结果 |
| --- | --- |
| WindowAbility | 333项；5→4→3→2→1→0、短剩余归零、不储存减免、持久化、其它角色不生效、起手中断、共享控制器、原生缩小与牵引 |
| PointerInteraction | 120项；真实WPF按下/松开链、单击对话、连续双击、拖拽排除、空中抓放、切角色、重复松开和右键排除 |
| GanyuTraversal | 6744项；左右交替借力、屏幕/窗口及双窗口、不同大小、负坐标、多屏接缝、遮挡、真实步行触边、连续轨迹、落脚和中断 |
| DesktopMovement | 45454项；其余跨屏、跳跃、普通角色攀爬、瓦雷莎跑墙/顶部/转角、取消与窗口消失回归保留 |
| Effects / Gait / Wings | 分别66、284、3316项通过，胡桃蝶化、走跑节奏及展翼未回归 |
| GlideFall / Nightsoul | 分别62745、1542项通过，缓降和夜魂行为保留 |
| MenuTheme / Motion / SpeechLayout | 四角色菜单与子菜单、动作渲染、固定文字位置通过 |
| Highlights / GptReset / VisualSmoke | 47项、94项以及10栏看点WPF交互布局检查通过 |

18组不同大小/屏幕位置的完整路线规划总计22毫秒，16窗口压力场景约7毫秒，为本机这组固定场景的测量值。

输入测试没有合成用户鼠标事件。WPF ClickCount元数据由测试注入；中途移动及返回起点的路径通过手势分类器注入，再交由真实松开处理链判定。窗口能力的原生检查只操作测试自建子进程的专用窗口；未修改用户应用窗口。涉及配置写入的测试保存并恢复原值，甘雨几何测试仅使用离线场景。

## 动画与启动

`artifacts/v24/ganyu-walljump.gif` 由122张真实WPF角色渲染帧按40毫秒编码，完整演示屏幕边→窗口侧边→屏幕边→窗口顶。背景窗口、路线辅助线和碰壁标记是预览场景；角色动作与轨迹来自生产代码。已检查中间跳跃、碰壁和最后落脚帧。发布副本在 `release/V2.4/previews/`。

最终EXE隐藏启动存活3秒，按测试PID找到实际“原神启动 · 甘雨”窗口，产品名与2.4.0.0版本正确，没有新增崩溃日志，然后关闭该测试进程。记录在 `artifacts/v24/startup-smoke.json`。启动检查在首次施法等待结束前完成。

本版沿用角色素材，资源ZIP SHA-256：`932eae3140ea536e134b60d59f1420955d818ffb58fb52ef3f88dbe55aa6ccf4`。

## 发布文件

- ZIP：`release/V2.4/原神启动-V2.4.zip`，25,453,211字节，仅含EXE与使用说明。
- EXE：25,661,440字节；根目录与发布副本哈希一致。
- ZIP SHA-256：`67b2e1fb35f1dd15da3c02940b8ffd865946e7d39c92c263428b7fe6b0247b12`
- EXE SHA-256：`7a218a51e7a840fc17569ee8e13f72adac02097f8ce7d51e958ae9c583780531`
- V2.3历史ZIP哈希仍为 `2bbd7cc03174cecd0724909095770288879471870cb7028b76da781eff01aef3`。

```powershell
& ./scripts/build.ps1
& ./scripts/test.ps1 -Suite DesktopMovement,Effects,Gait,MenuTheme,Motion,SpeechLayout,Wings,Highlights,GlideFall,Nightsoul,WindowAbility,GptReset,PointerInteraction,GanyuTraversal,VisualSmoke
& ./artifacts/tests/GanyuTraversalTests.exe --preview
& ./scripts/package.ps1 -Version 2.4 -InstructionsPath './docs/usage-v2.4.txt'
```
