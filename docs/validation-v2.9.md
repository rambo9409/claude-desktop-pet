# 原神启动 V2.9 验证记录

2026年11月4日，在 Windows 上使用 .NET Framework 4.8 完成回归。固有角色能力、战斗助手、每日看点与新本子交互共21组通过，结果见 `artifacts/v29/full-test-run.json`。本轮也运行了本机 OCR 和蝶化、展翼动画预览编码。

## 可拖的小本本

Notebook 套件通过122项检查。集成测试加载正式资源，在已显示但透明的 WPF 桌宠窗口中创建独立本子窗口，验证按住时角色位置固定、拖离后空手、拖回或双击后恢复持本。21份持本动作命中框与实际图像键一一对应，缩放、镜像和倾斜均有覆盖。

测试复现了鼠标捕获期间 WPF 同步派发移动事件的情形。现在捕获成功后才开始本子手势，松开时完成放置，手势中意外失去捕获时归还。独立窗口的捕获、释放和双击归还通过路由鼠标事件验证。双屏位置使用含负坐标的测试布局，未进行物理鼠标跨屏实测。

窗口目标使用模拟对象验证。珂洛持本时，查找目标窗口和修改窗口位置的调用均为零。拖离后，魔法仍受开关、安静模式及冷却约束。归还会立即取消未完成的施法。原生窗口能力另有337项回归，其中实际缩小与拉近只作用于自建测试窗口。每日看点的珂洛头像随持本状态更新。

## 形象与资源

DeepSeek 的27帧改为参考图中的蓝发鲸尾女仆，覆盖慢走、快跑、攀爬、灵感泡泡、拖拽及下落。珂洛提供25张空手动作图、21张持本动作图和1张独立本子图。资源包共199张 PNG 和1份命中框 JSON。用户参考图、内置 imagegen 提示词和裁切配方保存在 `assets/source/` 与 `assets/generated/`。

其他四位角色的125份资源与 V2.8 逐条对比。完整重建的结果记录在 `artifacts/v29/asset-verification.json`。两角色并排预览取自正式资源包，见 `release/V2.9/previews/reference-characters-preview.png`。

末轮核对修正了珂洛两帧带入的相邻图格碎片。裁切配方记录两个精确排除区域，分别只影响跑步第2帧和持本攀爬第3帧，其他45张珂洛素材及21个命中框一致。源图未改动，对比及逐像素验证保存在 `artifacts/v29/fragment-check/`。

资源刷新后另行复测 Notebook 与 Motion，两组通过，记录在 `artifacts/v29/post-art-test-run.json`。完整重建还暴露了 Pillow 13.0.0 在横向放大、纵向缩小时改变采样轴顺序的问题。`transform_frame` 现对这一情况显式先横后纵，并只做一次预乘透明度转换，使三位原神角色的9张历史表情图重新与旧包逐字节相同。

## 复现

```powershell
.\build.cmd
.\test.cmd -SkipPreviews -IncludeVisualSmoke
.\package.cmd -Version 2.9 -InstructionsPath .\docs\usage-v2.9.txt
```

发布 ZIP 内为 `原神启动.exe` 和 `使用说明.txt`，预览单独放在 `previews/`。正式 EXE 启动探针记录在 `artifacts/v29/startup-smoke.json`，发布文件 SHA-256 记录在 `artifacts/v29/release-hashes.json`。

最终 EXE 版本为2.9.0.0。启动后观察3秒，找到本进程的桌宠窗口且没有新增崩溃日志，然后正常关闭测试进程。ZIP内两项的解压内容与发布目录逐项校验通过，记录为 `artifacts/v29/package-content-verification.json`。完整素材重建200项逐字节一致，旧角色125项和V2.8发布包哈希保持一致。

| 发布文件 | 字节数 | SHA-256 |
| --- | ---: | --- |
| `release/V2.9/原神启动-V2.9.zip` | 41258321 | `f71f583e135897383a8e9a358b051cec2444780f39e01b565b4169c71b1e46cd` |
| `release/V2.9/原神启动.exe` | 41650176 | `b6faa890f0754a92661f65768783ef903e5ca97c5140e685e7cc3ab6885e124c` |
