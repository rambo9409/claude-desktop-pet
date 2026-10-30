# 原神启动 V2.8 验证记录

日期：2026-10-30（北京时间）；程序集2.8.0.0。

## 实现

新增GPT6A·龙女和DeepSeek·小肥鱼，与原四位角色共存。六角色选择、重启恢复、独立配色、每日看点名称及工具主题均接入。新角色拥有慢行/游动、奔跑、沿墙动作、施法、拖拽和下落素材；采用自然缓降，不套用原神风之翼。

龙息专注支持15/25/45分钟；双击开始25分钟，再双击只报告剩余；UTC截止时间、星环和HUD、一次完成提醒，隐藏/切换/退出取消。灵感泡泡提供本地文字记录、编辑、历史、明确删除、保存特效及未保存保护；隐藏切换保留本次运行内草稿。功能不请求模型API或读取剪贴板。

## 素材

内置imagegen生成4份原图：两角色主图集、空中姿态、龙女慢行补帧。原图和完整提示词在 `assets/generated/`，裁切配方为 `ai-v28-crops.json`。最终资源包177项：原123项逐字节不变，新两角色各27项。龙女慢行补绘明确的迈步/并足相位，修复跑步帧混入邻格龙角的问题。

完整 `build_assets.py` 在隔离输出目录重建，177项PNG与正式包逐字节一致。证据：`artifacts/v28/asset-verification.json`。旧资源备份在 `archive/v2.8-assets/characters-v2.7.zip`。主体透明度来自原图，打包不重画或去除alpha，仅裁切、等比缩放、定位。

## 回归结果

最终全套运行于17:12:23–17:13:26，20组全部通过。默认19组加VisualSmoke；证据 `artifacts/v28/full-test-run.json`，逐组日志位于 `artifacts/tests/`。本轮同时编码蝶化与展翼GIF。

| 重点范围 | 结果 |
| --- | --- |
| AiAbilities | 76项：计时边界/重复启动/取消/暂停后到期一次提醒、便笺安全存储与损坏备份、实际编辑保存删除、未保存草稿保护、特效像素与HUD不遮挡 |
| AiCharacters | 219项：六角色注册/菜单/重启恢复、主题对比度、工具名字、资源与实际动作渲染、自然缓降、真实WPF双击处理、移动/拖拽优先级、切换取消专注和退出草稿保护 |
| CombatPlanner / CombatRoster | 119 / 207项，包含本机OCR及原有拥有表和资料导入 |
| WindowAbility / PointerInteraction | 337 / 120项，原珂洛窗口能力及鼠标逻辑保留 |
| GanyuTraversal / GanyuDualScreen | 4158 / 62063项，42组双屏场景通过 |
| DesktopMovement / GlideFall / Nightsoul | 45454 / 62745 / 1542项 |
| Effects / Gait / Wings | 66 / 284 / 3316项 |
| Highlights / GptReset | 47 / 94项 |
| MenuTheme / Motion / SpeechLayout / VisualSmoke | 原四主题、动作、固定文字、看点折叠排序及布局通过 |

计时边界通过可控时间输入验证，不声称人工等待了45分钟。WPF鼠标事件调用和离线布局测试不等于真实手动桌面验收。原生窗口能力仅操作测试创建的窗口；涉及用户角色设置的测试均备份并恢复；便笺测试使用隔离路径，没有创建用户灵感记录。

实际查看了新角色浅/深背景素材、修正后的四帧动作、青绿菜单、430×450便笺小窗、真实龙女与HUD合成。最终能力预览在 `artifacts/v28/abilities-171320639/`，角色预览在 `artifacts/v28/ai-characters/`。发布预览保存在 `release/V2.8/previews/`。新功能未改变既有角色美术。

## 构建、启动、发布

`build.cmd`构建通过，保持C#5/net48；最终EXE以Hidden方式存活3秒，按本次PID找到“原神启动 · 瓦雷莎”，程序集2.8.0.0、产品名正确，无新增崩溃日志；只关闭测试进程。证据：`artifacts/v28/startup-smoke.json`。

发布 `release/V2.8/原神启动-V2.8.zip`，34,411,798字节；EXE为34,796,032字节。根目录与发布EXE一致，ZIP严格包含EXE与使用说明两条目，预览在ZIP外。

- ZIP SHA-256：`2691930e5dfcb3149ad483323041f52d11910974a7b0934182435abbd9b3b054`
- EXE SHA-256：`a8eb2b09515dcd3ebff312cf39829fac5c7018908ecfc4c892ee81ad9993816b`
- 资源ZIP SHA-256：`2ec1ca9f987b8d29ffa68732d4b0290ea5b7aee3cfb3f8ca7674e01f13811045`

```powershell
.\build.cmd
.\test.cmd -SkipPreviews -IncludeVisualSmoke
.\package.cmd -Version 2.8 -InstructionsPath .\docs\usage-v2.8.txt
```

编码预览时去掉SkipPreviews并指定含Pillow的Python。重建素材通过 `build.cmd -RebuildAssets -Python ...`；仅新增AI资源可用 `build_assets.py --append-ai`，此路径保留已有其他角色条目。
