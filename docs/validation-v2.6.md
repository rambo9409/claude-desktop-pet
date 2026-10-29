# 原神启动 V2.6 验证记录

日期：2026-10-29（北京时间），程序集版本2.6.0.0。

## 本次完成

恢复前任中断的编译清单，移除重复战斗模型，完成截图文件/剪贴板导入、本机OCR、可修改和勾选的导入预览、手动面板、字段红框与全部错误、当前及达标后每秒、四主题下拉框与标签页字体修复。工具窗口不再归桌宠所有。

内嵌10套有来源的配队，按官方7.1公告核验，截至2026-10-29；来源和条件见 [配队核验记录](combat-data-sources.md)。其中7套特殊机制仅展示攻略手法，隐藏本模型不能支持的伤害数值。其余3套仍使用通用示例，需用户填入面板和技能倍率，不是实战模拟或排名。

模型不会修改输入名称；短冷却E按可用时间排入后续动作；全队增益包含自己，按时长估算覆盖；通用参考线及公式限制均明示。损坏存档先备份，备份失败阻止覆盖；原子替换失败保留原文件。

新增窗口魔法独立开关，首次安装默认关闭，已有能力冷却存档的升级用户保留原行为。设置原子持久化，关闭取消施法，双击不会隐式打开开关。

拆分看点数据/界面、移动几何/运行时和战斗模型/界面，提供CMD入口、动态构建引用、统一换行规则、精简README、CHANGELOG、文档索引与 [HANDOFF](HANDOFF.md)。旧根EXE与Python缓存归档清单见 [整理记录](cleanup-v2.6.md)。

## 回归结果

全套运行：北京时间10:13:01–10:13:55，17组全部通过。清单：`artifacts/v26/full-test-run.json`；逐组日志：`artifacts/tests/`。随后修正不释放Q时不应计入Q伤害的模型边界，专项重新运行111项全部通过，证据在 `artifacts/v26/final-combat-run.json`；其余模块未改变。最终EXE在这项修复后重新构建、启动验证与打包。

| 范围 | 结果 |
| --- | --- |
| CombatPlanner | 最终111项：公式、补E时间、未释放Q不计伤害、覆盖、无输入突变、所有字段错误、损坏备份、锁定文件写入保护、空槽、OCR解析与本机OCR、四主题、错误态、小窗口、手法与不支持机制隐藏估算 |
| WindowAbility | 337项：施法与冷却回归、原生缩小/牵引、独立开关持久化及取消 |
| DesktopMovement | 45454项 |
| GanyuTraversal / GanyuDualScreen | 4158 / 61849项；42组双屏场景保留 |
| Effects / Gait / Wings | 66 / 284 / 3316项 |
| GlideFall / Nightsoul | 62745 / 1542项 |
| PointerInteraction | 120项 |
| Highlights / GptReset | 47 / 94项 |
| MenuTheme / Motion / SpeechLayout / VisualSmoke | 四主题、动作、固定文字位置、10栏看点布局与折叠拖放全部通过 |

实际Windows本机OCR使用 `zh-Hans-CN`，读取测试绘制的属性截图7项，并通过文件加载、行重建、数值解析完整链路。截图位于 `artifacts/tests/combat-ocr-fixture.png`，识别文本在 `combat-ocr-lines.txt`。这证明本机OCR链路可用，不代表任意游戏截图识别率；应用仍要求核对后再应用。测试未上传图片。

WPF预览覆盖四主题、860×620小窗、多个字段错误与推荐队伍。主代理查看小窗、错误态和推荐手法截图；文件位于 `artifacts/tests/combat-planner-*.png`，发布预览另存 `release/V2.6/previews/`。

涉及设置的测试备份并还原原值；原生窗口能力只操作自建测试窗口。战斗工具没有读取或控制游戏进程。

## 构建、启动与兼容

`build.cmd`经Windows PowerShell5.1实际构建通过，无警告。引用由本机安装的net48与Windows SDK动态发现，运行版仍为单EXE。源码保持C#5。原生命令失败日志完整捕获，不再在stderr第一行提前丢失详细错误。

最终EXE以Hidden启动存活3秒，按测试PID识别到“原神启动 · 胡桃”，版本和产品名正确，无新增崩溃日志，然后只关闭测试进程。证据：`artifacts/v26/startup-smoke.json`。

旧V2.5 ZIP SHA-256保持 `283191edca1b0dc8b89dc9ab9c88cd947317b61f8457de9220fde1820de700bd`。资源ZIP保持 `932eae3140ea536e134b60d59f1420955d818ffb58fb52ef3f88dbe55aa6ccf4`，没有重绘既有角色。

## 复现

发布文件：`release/V2.6/原神启动-V2.6.zip`（25,488,892字节），EXE为25,772,032字节。根目录与发布EXE哈希一致，ZIP两条目检查通过。打包校验使用.NET文件流SHA256，可在本机Windows PowerShell5.1缺少Get-FileHash时运行。

- ZIP SHA-256：`5210f631ed87c6f7333474bb0da37273afa63297423b7b8501bf80af165bd047`
- EXE SHA-256：`5c58c59e7e548a6752f41f9c9e4125783e721c1a0abea43b376590a4f8e5b8c2`

```powershell
.\build.cmd
.\test.cmd -SkipPreviews -IncludeVisualSmoke
.\package.cmd -Version 2.6 -InstructionsPath .\docs\usage-v2.6.txt
```

去掉SkipPreviews并指定Python可生成GIF；本次完整测试还执行了蝶化与展翼GIF编码。发布ZIP只能包含EXE与使用说明，预览不混入运行包。
