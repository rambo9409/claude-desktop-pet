# AI与开发者接手说明

更新于2026-11-06。项目是 Windows WPF 桌宠“原神启动”，当前发布版本V2.17，程序集2.17.0.0。本版为六角色增加不同字幕样式，并使字幕随角色移动、缩放与跨屏；当前要求替代旧的固定屏幕坐标规则。操作说明见`usage-v2.17.txt`，当前设计与验证见[字幕样式与跟随](speech-v2.17.md)。本轮构建、22组回归、视觉审看、正式EXE启动与发布校验全部通过，证据保存到`artifacts/v217/`。

V2.17完整回归时间2026-11-06 17:23:41.583–17:24:46.681（UTC+08:00）；17:25:24.492发布校验通过，ZIP SHA256为`45e17dcb5cb4fb330469b6585b2151b5304c1fe43ff9e5bee56da0798df43b27`。浅/深背景实际渲染预览保留在`assets/previews/speech-themes-v217-*.png`。角色素材和历史V2.16发布不变。跨屏覆盖使用模拟工作区与WPF位置事件，未做物理鼠标跨不同DPI双屏手测。

字幕以角色稳定待机轮廓作相对锚点，真实窗口移动时跟随，动作帧、镜像与旋转不改变锚点。靠近顶部或屏幕边缘时让位并限制在工作区内。`PetSpeechWindow.cs`负责六角色矢量外观、文字约束及放置；`PetWindow`负责跟随更新，更新布局时须防重入。不要恢复`speechPositionLocked`的屏幕锁定行为，也不要每帧从当前动作alpha重新定位。字幕不抢焦点、不拦截鼠标；主题与布局检查合并在SpeechLayout，默认21组加VisualSmoke共22组。

布局必须取父容器槽与待机比例，不能使用随当前图片宽高变化的Image实际尺寸；小于1 DIP的取整差异保持原位置，真实位移累计到阈值继续跟随。新句/换主题立即刷新测量，避免第一帧气泡大小滞后。顶部左右均放不下则放到角色下方，窄工作区约束宽高。

V2.16将珂洛换装从4.8秒缩短至3.2秒，8阶段每个约0.4秒；自动180秒资格、四组双击与续点保护保持。该版构建、22组回归、时长、启动及发布验证通过，历史记录见[换装加速](costume-speed-v2.16.md)，证据保留在`artifacts/v216/`。

V2.15完整回归时间2026-11-05 10:09:43.737–10:10:41.298（UTC+08:00），21组默认加VisualSmoke共22组全部PASS，稳定记录为`artifacts/v215/full-test-run.json`。AiCharacters为297项（删除被移除工具的2项主题断言），PointerInteraction 268项、KoloEasterEgg 370项、Notebook 122项、KoloPortal 18409项。源码、编译清单及最终程序集没有预测模块或接口标记，见`removal-verification.json`；素材包保持，2.15.0.0正式EXE启动观察3秒通过。旧`gpt-reset-cache.json`不再读取或刷新，升级不主动删除用户目录。历史发布与数据源文档只作历史记录，不应恢复已删除功能。

V2.15于10:12:23.375（UTC+08:00）完成发布校验，`release/V2.15/原神启动-V2.15.zip`仅含EXE和使用说明，与最终构建/文档逐字节一致；ZIP SHA256 `21008990fcdd7f333ac7cd8f5a184d807431064d11cd304e611fa36210150410`。历史V2.14包未覆盖。完整字节数、哈希与验证边界见[本版记录](removal-v2.15.md)，稳定证据保存在`artifacts/v215/`。

V2.14新增回归对修复前代码按预期复现第9下取消，记录为`artifacts/v214/before-fix-test-run.json`。修复后完整回归时间2026-11-05 09:21:23.620–09:22:26.744（UTC+08:00），23组全部PASS，稳定manifest为`artifacts/v214/full-test-run.json`；PointerInteraction 268项、KoloEasterEgg 370项、Notebook 122项、KoloPortal 18409项、AiCharacters 299项。正式EXE观察3秒无新崩溃，09:23:11.023发布校验通过；EXE及使用说明与ZIP内容逐字节一致，素材和V2.13历史包哈希保持。发布包为`release/V2.14/原神启动-V2.14.zip`，SHA256 `fba36238eacc80eb1460ec85fb29b7f6844e51d1a9686040326c3dc009794520`。证据保存在`artifacts/v214/`。

V2.14实际输入探针记录到4组双击成功触发并切换DeepSeek，但没有记录触发后的多余点击；超过阈值续点由自动回归覆盖。电脑操作截图超时后没有发送自动点击，真实12连点UI计划未执行，`live-probe-result.json`准确标记PARTIAL。探针已关闭，角色设置已按原字节恢复。完整发布哈希和验证边界见[V2.14说明](ai-v2.14.md)。

手动请求一旦发出（含等待落稳与已播放），普通续点不改变`pointerHeld/state`，也不取消请求、重启门户或重复扣冷却；真的拖动超过5px才转入常规拖拽并取消。预览本子命中必须转交同一保护逻辑，开始播放时立即出第一帧。前3组双击显示1/4至3/4进度，前4组各减1分钟冷却。回归必须涵盖8次后继续9–12次点按及实际拖拽转换；原测试恰好停在8次，漏掉了真实用户继续连点的场景。

V2.13正式构建、23组完整回归、最终素材验证与人工审看、EXE启动和发布检查全部PASS。完整回归时间为2026-11-05 08:54:22.707–08:55:24.706（UTC+08:00），稳定manifest为`artifacts/v213/full-test-run.json`；AiCharacters 299项、PointerInteraction 230项、KoloEasterEgg 328项、Notebook 122项。真实屏幕/光标与模拟夹具混用的问题已在测试中隔离，未删减断言。末轮只调整跑步整体比例：龙女源图scale 0.95545，小肥鱼0.9215；随后09:01:26.647–09:01:32.816重新通过AiCharacters/Motion/Gait三组，见`artifacts/v213/final-asset-test-run.json`。最终资源208项中8改、200保留，慢走原字节不变，完整重建208项一致；2.13.0.0正式EXE启动观察3秒无新增崩溃日志。09:02:01.702完成发布校验，`release/V2.13/原神启动-V2.13.zip`仅含EXE和使用说明，均与最终构建/文档一致，历史V2.12包哈希未变。最终素材SHA256为`3d3eaea72831d16b31e89e186cd8a2a1b90ded0b8d5bd7f4d5ee4eb2effdd0d9`；发布大小、哈希和覆盖界限见[V2.13验证](validation-v2.13.md)。稳定证据位于`artifacts/v213/`，不要用后续可能被覆盖的`artifacts/tests/latest-run.json`替代。尚未进行真实等待180秒及物理鼠标跨不同DPI双屏拖拽的手测。

V2.12正式构建、资源验证、23组完整回归、EXE启动及发布检查全部通过。资源208项中27张龙女帧更新、181项保持V2.11原字节，完整重建208项也一致；证据为 `artifacts/v212/asset-verification.json`。最终回归时间为2026-11-04 22:25:28–22:26:25（UTC+08:00），manifest为 `artifacts/v212/full-test-run.json`；2.12.0.0启动观察3秒无新增崩溃日志。发布包为 `release/V2.12/原神启动-V2.12.zip`，仅含EXE和使用说明且与构建/文档一致，历史V2.11 ZIP哈希不变；实际字节数、哈希及验证范围见 [V2.12验证](validation-v2.12.md)。稳定证据保存在 `artifacts/v212/`，避免后续测试覆盖 `artifacts/tests/latest-run.json`。三分钟阈值通过可控时钟测试验证，未另行做真实等待180秒的手测；不同DPI物理双屏拖拽也尚未手测。

珂洛计时包含普通移动及安静模式，隐藏/最小化暂停，切换形象或重新开启彩蛋重置三分钟，重启不继承进度。到期遇到拖拽、菜单、传送或施法会保留资格，等安全支撑后播放；普通移动会停止后换装，中断不再重新随机等待。不要恢复V2.11的随机抽签、12秒连续待机或5分钟触发冷却。

V2.14保留四组有效双击规则：相邻组完成时间间隔不超过1.5秒，支持WPF ClickCount连续1–8和四组独立1/2。手动请求独立于自动开关，空中/门户等待安全支撑。触发前的拖拽、超时、额外不成对单击、本子操作及跨角色会重置累计；触发后的普通续点受保护，真实拖拽、菜单、隐藏、最小化、切角色等操作才清理手动待触发。手动中断不重试，已到期的自动Ready按原规则保留。不要恢复“每个MouseDown都算新抓取并取消”的旧行为。

V2.11已完成正式构建、23组完整回归、EXE启动、208项素材及完整重建比较、发布包内容和哈希检查，全部PASS。最终全套时间为2026-11-04 18:14:17–18:15:17（UTC+08:00），稳定manifest为 `artifacts/v211/full-test-run.json`。18个含离屏绘图的测试入口已统一到共享 `WpfTestRender`，保留全部断言与预览，修复同步宿主的Dispatcher队列问题。发布包为 `release/V2.11/原神启动-V2.11.zip`，测试宿主修改不改变已核验的生产EXE，V2.10历史包保持不变。稳定证据保存在 `artifacts/v211/`，不要把会被后续测试覆盖的 `artifacts/tests/latest-run.json` 当作永久记录。尚未进行物理鼠标在不同DPI双屏之间拖拽的实测，模拟几何和路由事件通过不等于该项手测通过。

V2.10已完成正式构建、21组回归、27张龙女替换/173项其他资源保留检查、200项完整素材重建比较、启动和发布包内容校验。发布包为 `release/V2.10/原神启动-V2.10.zip`，预览在同目录 `previews/`，证据和限制见 [V2.10 验证](validation-v2.10.md)。后续修改仍需运行对应检查。

DeepSeek采用蓝发鲸尾女仆形象，珂洛有持本和空手两组动作。持本时窗口魔法暂停，拖走和归还本子时更新施法资格，详见[本子与参考形象](notebook-v2.9.md)。

V2.9已完成构建、21组回归、末轮素材修正后的Notebook/Motion复测、200项素材完整重建比较、正式EXE启动及发布包内容校验。发布包为 `release/V2.9/原神启动-V2.9.zip`，预览在同目录的 `previews/`。双屏覆盖为模拟坐标与路由事件测试，尚未进行物理鼠标在不同DPI显示器间拖拽的实测。

V2.8历史发布包为 `release/V2.8/原神启动-V2.8.zip`，当版验证见 [V2.8验证](validation-v2.8.md)。

V2.7已完成：正式构建、18组回归（拥有表207项、战斗助手119项）、界面检查、正式EXE启动和发布包校验均通过。发布为 `release/V2.7/原神启动-V2.7.zip`；实际时间、证据位置、哈希及限制见 [V2.7验证](validation-v2.7.md)。后续改动仍需重新运行相关验证。

V2.6已完成发布，历史17组全套通过和后续模型修正证据见 [V2.6验证](validation-v2.6.md)。以Git最新状态为起点，不要再次套用更早“构建坏了”的交接状态。

## 先确认实际状态

1. 读取 `git status --short` 与 `git log -5 --oneline`，保留未提交改动。`90b5a12` 是前任 V2.6 战斗助手原样保存的提交，`97baf61` 是V2.5基线；当前实现包含后续修复，不要以旧交接“构建损坏”为由覆盖它。
2. 读本文件、[工程说明](project-layout.md)、[战斗助手说明](combat-planner-v2.6.md)、[角色与资料更新](roster-updates-v2.7.md)和当前用户请求。
3. 运行 `.\build.cmd`。构建结果是根 `原神启动.exe`，展开的编译清单是 `artifacts/build/production.rsp`。
4. 根据变更运行相关测试，发布前跑 `.\test.cmd -SkipPreviews -IncludeVisualSmoke`。V2.15删除GptReset套件后，21组默认回归加VisualSmoke共22组。
5. 判断测试结果以本次`artifacts/tests/latest-run.json`的时间、状态和套件为准。历史验证记录不能代表当前源码已通过；本版证据汇总在`docs/speech-v2.17.md`，并保留到`artifacts/v217/`。

## 模块地图

| 文件 | 职责与入口 |
| --- | --- |
| `src/ClaudePetMotion.cs` | 程序入口、PetWindow生命周期、主计时循环、基础设置与程序集版本 |
| `src/PetSpeechWindow.cs` | 六角色字幕气泡、文本布局、透明窗口和边缘放置，跟随锚点由PetWindow更新 |
| `src/PetMenu.cs` | 六角色菜单与配色、工具入口、活动模式、能力入口 |
| `src/PetAiAbilities.cs` | 龙息专注、星环和泡泡显示、统一DeepSeek主题的灵感本UI与安全保存 |
| `src/PetNotebook.cs` | 持本/离手状态、每帧命中、跨屏独立本子窗口、归还及生命周期 |
| `src/PetKoloPortal.cs` | 拖拽松手后的稳住/开门/进入/底部出现/收门，当前屏落点与WPF门户绘制 |
| `src/PetKoloEasterEgg.cs` | 自动180秒资格与手动换装请求、安全时机、8帧换装、开关与正式DeepSeek切换 |
| `build_deepseek_v29.py`、`build_kolo_v29.py` | 两角色参考重绘的裁切与归一化，主入口由build_assets.py统一调用 |
| `build_gpt6a_v212.py` | GPT6A统一画风后的27张运行素材，由build_assets.py统一调用；V2.10脚本保留历史 |
| `build_ai_runs_v213.py` | 两位AI各4张跑步覆盖层，保留基础脚本的慢走和其他动作 |
| `build_kolo_v211.py` | 新慢走8帧、本子慢走命中框与换装8帧，与既有珂洛素材合并 |
| `src/PetMovement.cs` | 移动状态、平台枚举、拖拽、跌落、跨屏运行时 |
| `src/DesktopMovement.cs` | DesktopMovement纯几何、VaresaRunTrack路线类 |
| `src/PetGanyuTraversal.cs` | 甘雨完整多段路径规划、单墙/屏幕接缝借力、小跑靠近、运行时取消 |
| `src/PetPointerGestures.cs` | 单击/双击与拖拽区分、四组双击累计与超时取消 |
| `src/PetEffects.cs`、`PetWings.cs`、`PetNightsoul.cs` | 胡桃蝶化、三角色展翼缓降、瓦雷莎能量 |
| `src/PetWindowAbility.cs`、`PetAbilityEffects.cs` | 珂洛窗口目标选择、冷却/开关、实际Win32操作、施法显示 |
| `src/DailyHighlights.cs` | 看点WPF界面、折叠排序与主题 |
| `src/DailyHighlightsData.cs` | 看点模型、来源、解析、缓存和后台抓取 |
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
| `tests/WpfTestRender.cs` | 测试专用离屏渲染与Dispatcher安全边界，由测试编译器自动包含 |

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

V2.9中珂洛默认持本：`NotebookBlocksMagic`必须在目标枚举和Advance之前阻断施法，持本、取本拖拽和独立本子拖拽均不能施法。拖离不自动开魔法或清冷却。归还、隐藏、最小化、切换和关闭必须回收本子并取消施法；不持久化离手状态。`kolo-book-*`和空手帧分离，命中框由ZIP中的 `kolo-notebook-hitboxes.json` 指向实际BitmapSource；必须随WPF镜像、倾斜和缩放正确反变换。

V2.11门户仅由珂洛拖拽松手触发。高于当前屏底足够距离才启动，近底部使用正常落地；自然走出窗口的掉落沿用旧路径。演出共约2.95秒，传送时重新读取工作区，处理负坐标、缩放与屏幕消失。`notebook.Pressed`期间不推进移动或传送时间；本子仍在手中或已拖离但未松手，都应保持暂停，松手再继续。取消时必须恢复Opacity、Clip和Transform，避免再次抓取或换角色后不可见。窗口魔法在门户和换装期间都必须先阻断，再枚举目标窗口。

彩蛋开关仍存于 `kolo-easter-egg-enabled.txt`，默认开，原子保存。V2.12的 `KoloEasterEggSession`累计180秒可见使用，移动与quiet均计时；隐藏和最小化暂停，超过5秒的计时中断/休眠及时间回拨不计入，切换角色或改变开关重置。到期Ready保持到安全机会，不再随机，也不再要求连续待机。持本不阻止彩蛋，按住本子、拖独立本子、无支撑、菜单、窗口魔法和门户阻止当前播放；普通走路会StopWalking后播放，开始先收回本子。V2.16起8帧持续3.2秒，在PrepareNotebookFrame后覆盖；末帧等于正式DeepSeek idle，再调用SwitchCharacter完成统一刷新与保存。抓取、缩放、隐藏等中断保留Ready，安全后重播；手动切换或禁用重置并阻止补触发。缩放取消入口保留在ResizePet开头。不要把“中断当前演出”误写成“清除三分钟资格”。

V2.8灵感本为 `deepseek-bubbles.json`，最多100条、每条2000字，沿用原子写入和损坏备份。隐藏/切换仅收起窗口并保留草稿，退出主窗通过 `CanCloseAiAbilities` 检查未保存内容，确认后 `CloseAiAbilities` 清理。龙息专注是内存内UTC截止时间，切换/隐藏/退出即取消；不恢复成系统定时任务，不更改quiet设置。

V2.12记事窗直接复用DeepSeek的共享浅蓝调色板。圆角窗的自定义关闭按钮必须调用原有Close/ConfirmDiscard路径，不能跳过未保存提示。历史列表的Preview、UpdatedLabel是带ScriptIgnore的显示属性，不能写入存储模型；窗口头像复用当前角色idle位图，不需要重新加载整套素材。

## 验证与发布约定

测试顺序执行；涉及用户设置的测试必须备份并finally还原。原生能力测试只操作测试创建的专用窗口，不应用用户真实窗口作为目标。界面测试应覆盖六主题、最小尺寸、长文字、错误态和OCR预览。MenuTheme会逐个渲染六角色的菜单和子菜单，并检查悬停、选中与禁用文字的对比度。

含离屏绘图的测试入口使用共享 `tests/WpfTestRender.cs`。`scripts/common.ps1`测试编译会自动加入它，生产 `build.rsp`不应包含此类。`WpfTestRender.Render`在同步RenderTargetBitmap前后以Background优先级处理Dispatcher队列，让已排队的WPF工作完成；不要在Measure/Arrange/OnRender中调用。需要确定性模拟的测试先停止桌宠计时器。不要改为Send（同线程可能直接执行而不清队列），也不要吞掉Dispatcher异常、删断言或跳过预览来伪造通过；这项修复属于测试宿主，不应改变正式程序渲染行为。AiCharacters在正常结束和异常退出时都会恢复测试前的角色设置；原生回调异常可能绕过Main的finally，因此异常路径恢复也应保留。

正式打包前同步程序集版本、README、使用说明及验证记录。命令：

```powershell
.\build.cmd
.\test.cmd -SkipPreviews -IncludeVisualSmoke
.\package.cmd -Version 2.17 -InstructionsPath .\docs\usage-v2.17.txt
```

`release/V2.17/原神启动-V2.17.zip`只能含EXE和使用说明。需要新增预览时独立放`release/V2.17/previews/`。保留旧发布，记录ZIP与EXE SHA-256，不要把测试帧、历史构建或工程源混入用户包。本版字幕由WPF绘制，角色资源不变，沿用V2.13的208项（207PNG和1JSON）。维护V2.13跑步素材时可用`build_assets.py --refresh-v213`，只替换两位AI各4张`walk-*`，其余200项须与V2.12逐字节一致，尤其保留`stroll-*`。完整重建仍用`build.cmd -RebuildAssets -Python ...`，旧版定向刷新入口保留用于维护。

素材重建须保留 `transform_frame` 在横向放大、纵向缩小时的显式先横后纵采样。Pillow 13会自动换轴顺序，直接恢复单次 `resize` 会改变三位原神角色的9张历史表情图。珂洛两帧的 `excludeAtlasRects` 仅排除已核对的相邻格鞋底和头发，不能扩大到人物本身。原图、裁切配方和提示词共同构成可复现来源。

## 整理与后续方向

README用于使用，CHANGELOG用于版本演进，当前设计和交接在docs。旧usage/validation保留路径，经 [INDEX.md](INDEX.md) 导航。旧根EXE与Python缓存已移动至 `archive/v2.6-engineering-cleanup/`，哈希在 [cleanup-v2.6.md](cleanup-v2.6.md)。归档不参与构建，缺失不影响新机器编译。

后续可按实际功能继续拆分大型WPF窗口与异常处理边界，勿单纯为文件长度机械重写。SDK风格项目迁移需另设任务验证资源、net48兼容、WinRT引用和所有窗口行为。当前不能宣称已消除所有历史 `catch { }`；本次重点是存储与新功能的可诊断失败。
