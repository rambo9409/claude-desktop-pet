# AI与开发者接手说明

更新日期：2026-10-29。项目是本地 Windows WPF 桌宠，生产名“原神启动”，目标版本 V2.7。当前任务是角色拥有表、按拥有角色匹配攻略，以及未来角色/资料更新方式；保留V2.6的OCR、工程整理及原桌宠行为。

V2.7已完成：正式构建、18组回归（拥有表207项、战斗助手119项）、界面检查、正式EXE启动和发布包校验均通过。发布为 `release/V2.7/原神启动-V2.7.zip`；实际时间、证据位置、哈希及限制见 [V2.7验证](validation-v2.7.md)。后续改动仍需重新运行相关验证。

V2.6已完成发布，历史17组全套通过和后续模型修正证据见 [V2.6验证](validation-v2.6.md)。以Git最新状态为起点，不要再次套用更早“构建坏了”的交接状态。

## 先确认实际状态

1. 读取 `git status --short` 与 `git log -5 --oneline`，保留未提交改动。`90b5a12` 是前任 V2.6 战斗助手原样保存的提交，`97baf61` 是V2.5基线；当前实现包含后续修复，不要以旧交接“构建损坏”为由覆盖它。
2. 读本文件、[工程说明](project-layout.md)、[战斗助手说明](combat-planner-v2.6.md)、[角色与资料更新](roster-updates-v2.7.md)和当前用户请求。
3. 运行 `.\build.cmd`。构建结果是根 `原神启动.exe`，展开的编译清单是 `artifacts/build/production.rsp`。
4. 根据变更运行相关测试，发布前跑 `.\test.cmd -SkipPreviews -IncludeVisualSmoke`。17组默认回归加VisualSmoke共18组，新增CombatRosterTests。
5. 判断测试结果以本次 `artifacts/tests/latest-run.json` 的时间、状态和套件为准。历史验证记录不能代表当前源码已通过；本版发布证据写入 `docs/validation-v2.7.md`。

## 模块地图

| 文件 | 职责与入口 |
| --- | --- |
| `src/ClaudePetMotion.cs` | 程序入口、PetWindow生命周期、主计时循环、基础设置与程序集版本 |
| `src/PetMenu.cs` | 四角色菜单、工具入口、活动模式、窗口魔法开关 |
| `src/PetMovement.cs` | 移动状态、平台枚举、拖拽、跌落、跨屏运行时 |
| `src/DesktopMovement.cs` | DesktopMovement纯几何、VaresaRunTrack路线类 |
| `src/PetGanyuTraversal.cs` | 甘雨完整多段路径规划、单墙/屏幕接缝借力、小跑靠近、运行时取消 |
| `src/PetPointerGestures.cs` | 单击/双击与拖拽区分 |
| `src/PetEffects.cs`、`PetWings.cs`、`PetNightsoul.cs` | 胡桃蝶化、三角色展翼缓降、瓦雷莎能量 |
| `src/PetWindowAbility.cs`、`PetAbilityEffects.cs` | 珂洛窗口目标选择、冷却/开关、实际Win32操作、施法显示 |
| `src/DailyHighlights.cs` | 看点WPF界面、折叠排序与主题 |
| `src/DailyHighlightsData.cs` | 看点模型、来源、解析、缓存和后台抓取 |
| `src/GptResetPrediction.cs` | 公开全站预测的抓取、缓存、时间语义和工具界面 |
| `src/GenshinCombatPlannerIntegration.cs` | 桌宠打开/复用战斗助手窗口，不设置桌宠Owner |
| `src/GenshinCombatPlanner.cs` | 战斗助手WPF表单、主题、字段错误、OCR预览、推荐资料与结果显示 |
| `src/CombatPlannerModel.cs` | 输入/结果模型、纯计算、全字段校验、本地存档 |
| `src/CombatPlannerPresets.cs` | 内嵌/本地覆盖配队目录验证、导入、展示与示例面板构造 |
| `src/CombatRoster.cs` | 拥有表存储、角色名/别名、目录候选并集及缺员匹配 |
| `src/CombatRosterUi.cs` | 拥有表搜索勾选、自定义名字、推荐筛选与资料导入界面 |
| `assets/combat-teams.json` | 14套配队，核对日期、适用版本、来源、手法与充能建议；格式version 2 |
| `assets/combat-roster.json` | 127个角色/元素形态条目与别名；维护者词典快照，非实时完整实装名册 |
| `src/CombatPanelOcr.cs` | 本机OCR、面板文字解析、文件/剪贴板图片加载、可替换识别接口 |
| `src/PetStorage.cs` | 原子文件替换、损坏存档唯一备份、可恢复错误诊断 |

## 构建约束

只用 C# 5 / net48 支持的语法。已有系统csc即可，不要在没有任务需要时迁移SDK风格项目或安装工具链。构建机需要 .NET Framework 4.8 Developer Pack 和 Windows 10/11 SDK；运行用户仅需要Windows和net48。

`build.rsp` 是生产源文件/资源的唯一模板，新增源文件必须加入这里。引用占位符由 `scripts/common.ps1` 动态展开，支持 `PET_NET48_REFERENCES` 和 `PET_WINMD_FILE`。WinRT OCR引用必须来自Windows SDK完整 `UnionMetadata/Windows.winmd`，不能改用System32分片元数据。已配置 `/codepage:65001` 与WinRT桥接所需的1701警告抑制。

CMD入口以进程级 ExecutionPolicy Bypass 启动PowerShell，不更改机器策略。无Python环境可使用 `-SkipPreviews`；仅GIF编码和重裁素材需要Python/Pillow/NumPy。`KOLO_PYTHON` 或 `-Python` 可显式指定，脚本不依赖某个AI私有运行时路径。

编辑文件优先用补丁或正常文件API。不要把巨量源代码逐键输入到交互式PowerShell；之前的中断来自这种输入方式。

含中文的 `.ps1` 必须保存为带BOM的UTF-8，确保Windows PowerShell 5.1正确读取中文EXE路径；C#由 `/codepage:65001` 明确编码。换行格式见 `.gitattributes`，只规范正在修改的文件，不全仓重写。

## 战斗助手边界与扩展

- 四人输入及敌人参数可以手填；截图只提取可见面板值，不会读取游戏进程、内存、文件，也不提供自动按键。
- `ICombatPanelImageExtractor` 是图像识别扩展接口，默认 `WindowsOcrPanelExtractor`；`CombatPanelTextParser` 支持识别文字到属性的转换。未来接其他识别引擎时保持预览核对、未识别字段不覆盖、可手改与错误反馈。
- 本机OCR需Windows语言组件。结果可能误读或缺失。若需新增网络视觉服务，应明示图片去向，不可悄悄上传用户截图。
- 配队JSON是有版本、日期和来源的静态资料，不是实时“最新排行榜”。更新时重新核对来源，区分来源给出的手法与自行编排，未知版本、新角色或机制不猜数据。具体编辑规范见资料字段与相关测试。
- 拥有表只保存名字；匹配是已收录四人攻略的缺员筛选，不是任意组合自动优化。尚未配置时可浏览全部；明确保存空表时无队伍可用。缺员队不得直接载入为可用队。
- 角色候选由基础词典、当前目录成员、用户保存名字合并。旅行者元素不可互设别名；允许同角色中英文和明确错别字兼容，禁止把替代角色当别名。Vesna与用户“薇斯娜”、词典“薇斯纳”均匹配；“法露珊”兼容标准“珐露珊”。
- 新角色通过手填或目录成员补入即可选中，不代表拥有经过核验的攻略或已实现伤害机制。新增特殊队伍默认仅手法；不得改一个supportsEstimate就宣称支持新公式。
- 目录导入是完整替换，不是追加。格式version 2、核对日期不能倒退，支持同日修订。来源HTTPS与字段校验只能验证格式，无法验证内容真实性。不能把重新加载按钮写成自动联网更新。
- 模型是通用期望伤害与循环规划，非逐帧战斗模拟。E补放受循环剩余时间限制；增益覆盖为近似；能量、特殊反应、命座与装备被动并不完整建模。通用参考属性线不能包装成所有角色的毕业线。
- `Calculate` 不得改变调用方输入。非法字段一次返回对应错误，空角色和损坏存档应给友好提示。

## 存储与兼容

数据目录 `%LocalAppData%\KoloPet`。战斗存档为 `combat-plan.json`；版本迁移与校验在 `CombatPlannerStorage`。重要设置使用 `PetStorage.WriteAllTextAtomic` 或 `TryWriteAllText`，写临时文件后替换，替换失败应保留旧文件，禁止退回 `File.Copy(..., overwrite: true)`。

V2.7拥有表为 `combat-roster-owned.json`，独立于战斗面板。配队覆盖为 `combat-teams.json`，导入时保存旧有效覆盖到 `combat-teams.json.previous.json`。坏覆盖回退内置并记录日志；导入前无法备份损坏旧文件时拒绝覆盖。导入/重新加载不得清空拥有表、当前面板或令已经应用的旧队悄悄套上新队的机制标记。

保存的预设id在新目录中失效时，重启仍保留面板，但禁止直接伤害粗算。恢复原资料或由用户明确“转为自定义（保留输入）”后才脱离旧引用；不要因查不到预设就默默把特殊机制队变成可估算的自定义队。

损坏文件使用带时间和GUID的 `.broken-…json` 备份。备份失败不得默默以示例覆盖原文件。`diagnostics.log` 在256KB附近轮换到 `diagnostics.old.log`；此记录与 `error.log` 的未处理异常职责不同。不能把日志写入错误再递归记录到同一个失败位置。

窗口魔法开关存于 `window-magic-enabled.txt`：新安装默认关闭；有旧冷却存档的升级用户保留启用状态。关闭要立即中断施法；双击减少冷却不会打开魔法。不要因为修改UI而改变这些行为。

## 验证与发布约定

测试顺序执行；涉及用户设置的测试必须备份并finally还原。原生能力测试只操作测试创建的专用窗口，不应用用户真实窗口作为目标。界面测试应覆盖四主题、最小尺寸、长文字、错误态和OCR预览。

正式打包前同步程序集版本、README、使用说明及验证记录。命令：

```powershell
.\build.cmd
.\test.cmd -SkipPreviews -IncludeVisualSmoke
.\package.cmd -Version 2.7 -InstructionsPath .\docs\usage-v2.7.txt
```

`release/V2.7/原神启动-V2.7.zip`只能含EXE和使用说明。预览独立放 `release/V2.7/previews/`。保留旧发布，记录ZIP与EXE SHA-256，不要把测试帧、历史构建或工程源混入用户包。

## 整理与后续方向

README用于使用，CHANGELOG用于版本演进，当前设计和交接在docs。旧usage/validation保留路径，经 [INDEX.md](INDEX.md) 导航。旧根EXE与Python缓存已移动至 `archive/v2.6-engineering-cleanup/`，哈希在 [cleanup-v2.6.md](cleanup-v2.6.md)。归档不参与构建，缺失不影响新机器编译。

后续可按实际功能继续拆分大型WPF窗口与异常处理边界，勿单纯为文件长度机械重写。SDK风格项目迁移需另设任务验证资源、net48兼容、WinRT引用和所有窗口行为。当前不能宣称已消除所有历史 `catch { }`；本次重点是存储与新功能的可诊断失败。
