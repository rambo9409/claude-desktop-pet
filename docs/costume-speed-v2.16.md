# V2.16 珂洛换装加速

2026-11-06，程序集2.16.0.0。按用户要求将换装动画快放1.5倍：`KoloEasterEggSession.DurationSeconds`由4.8改为3.2，原8个阶段由每个约0.6秒变为0.4秒。自动和手动触发共用此时长。

自动可见使用180秒、主动四组双击、续点保护及拖拽中断规则保持。仍使用原8张素材，本次没有增加过渡帧；播放加快不等于重绘动作序列。

## 验证

正式构建及22组完整回归通过，时间2026-11-06 16:11:37.914–16:12:43.142（UTC+08:00），见`artifacts/v216/full-test-run.json`。KoloEasterEgg 370项、PointerInteraction 268项、AiCharacters 297项、Notebook 122项、KoloPortal 18409项。

`timing-verification.json`从正式程序集读取3.2秒与180秒阈值，并核对8阶段每个0.4秒、倍率1.5；`startup-smoke.json`确认2.16.0.0正式EXE观察3秒无新增崩溃日志。

发布于2026-11-06 16:14:08.993（UTC+08:00）。`package-verification.json`和`release-hashes.json`确认ZIP只含最终EXE与使用说明，逐字节一致；历史V2.15包及角色素材不变。

| 文件 | 字节数 | SHA256 |
| --- | ---: | --- |
| `release/V2.16/原神启动.exe` | 43537920 | `7719fdd0c87989ef8eec9ddd9e11a56a4d9634222cb68705bc6fd0f496a14bc2` |
| `release/V2.16/原神启动-V2.16.zip` | 43131104 | `956a52b469792a575a2210e389e6ff540550eb47cb39ec1cd96dc8db2fba83a7` |
