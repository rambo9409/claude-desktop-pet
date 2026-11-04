# V2.12 工程与构建

## 目录

| 路径 | 用途 |
| --- | --- |
| `src/` | C# 5 / WPF / .NET Framework 4.8 生产代码 |
| `build.rsp` | 唯一生产源文件、资源与引用模板，生产和测试共用 |
| `build.cmd`、`test.cmd`、`package.cmd` | 无需更改系统执行策略的 Windows 入口 |
| `scripts/` | 编译、测试、打包与预览编码 |
| `tests/` | 测试源码与历史薄包装入口 |
| `tests/WpfTestRender.cs` | 离屏WPF渲染的共享入口，在位图渲染前后处理Dispatcher队列；仅测试编译包含 |
| `assets/characters.zip`、`assets/combat-teams.json`、`assets/combat-roster.json` | 内嵌动画、带来源的配队目录与角色名称/别名快照 |
| `assets/generated/`、`assets/source/` | 当前和历史源素材、提示词、裁切参数 |
| `build_gpt6a_v212.py`、`build_deepseek_v29.py`、`build_kolo_v29.py` | 当前参考形象的透明图集裁切与运行帧布局，由 `build_assets.py` 统一调用；V2.10龙女脚本保留历史 |
| `build_kolo_v211.py` | 珂洛新版慢走与换装帧，合并到既有珂洛素材和本子命中框 |
| `artifacts/build/` | 根据当前环境展开的生产编译响应文件 |
| `artifacts/tests/` | 测试 EXE、日志、渲染截图、`latest-run.json` |
| `release/V*/` | 精简用户发布包及独立预览 |
| `archive/` | 已停用构建、历史资源和缓存的保留副本 |
| `docs/` | 当前指南、AI交接、数据源和历史验证索引 |

## 构建依赖

Windows 10/11、.NET Framework 4.8 Developer Pack、Windows 10/11 SDK。编译器为系统 Framework64 或 Framework 的 `csc.exe`，语法上限保持 C# 5。SDK 的 `UnionMetadata/Windows.winmd` 用于 Windows OCR 的 WinRT 类型；不要混用 System32 分片 WinMD，它们与 Framework 的 `IBuffer` 类型投影不匹配。

`scripts/common.ps1` 动态寻找安装目录，支持覆盖：

| 变量 | 用途 |
| --- | --- |
| `PET_NET48_REFERENCES` | 含 PresentationFramework.dll 的 net48 引用程序集目录 |
| `PET_WINMD_FILE` | Windows SDK 的完整 UnionMetadata/Windows.winmd 路径 |
| `KOLO_PYTHON` | Python 可执行文件；也可向脚本传 `-Python` |

`build.rsp` 中 `{NET48}`、`{FRAMEWORK}`、`{WINRT_METADATA}` 由脚本展开到 `artifacts/build/production.rsp`。因此请使用统一入口，不直接把模板交给 csc。测试也从同一模板生成自己的 RSP。`/nowarn:1701` 仅抑制 Framework/WinRT 桥接常见的版本绑定警告，其他编译错误仍直接失败。

```powershell
.\build.cmd
# 重新裁切素材需要 Python + Pillow + NumPy
.\build.cmd -RebuildAssets -Python C:\Python312\python.exe
```

Python 优先显式参数，其次 `KOLO_PYTHON`、PATH 的 python/python3、`py -3`。不再依赖某个AI运行时的私有目录，不会自动安装或下载依赖。

## 测试

```powershell
# 22组默认回归；加看点交互检查后共23组，无需Python
.\test.cmd -SkipPreviews -IncludeVisualSmoke
# 单独战斗助手
.\test.cmd -Suite CombatPlanner -SkipPreviews
# 角色拥有表、资料导入、筛选与实际WPF窗口
.\test.cmd -Suite CombatRoster -SkipPreviews
# 珂洛空中稳住与传送
.\test.cmd -Suite KoloPortal -SkipPreviews
# 三分钟可见使用计时、到期保留、换装与正式切换
.\test.cmd -Suite KoloEasterEgg -SkipPreviews
# 使用明确的Python编码预览
.\test.cmd -Python C:\Python312\python.exe -IncludeVisualSmoke
```

多组选择可在 PowerShell 内调用：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& './scripts/test.ps1' -Suite CombatPlanner,GanyuDualScreen -SkipPreviews"
```

默认组为 DesktopMovement、Effects、Gait、MenuTheme、Motion、SpeechLayout、Wings、Highlights、GlideFall、Nightsoul、WindowAbility、GptReset、CombatPlanner、CombatRoster、PointerInteraction、GanyuTraversal、GanyuDualScreen、AiAbilities、AiCharacters、Notebook、KoloPortal、KoloEasterEgg。VisualSmoke 是额外一组，默认 `--verify` 后退出，不启动常驻交互窗口。

按顺序运行，避免共享配置互相干扰。测试中需要暂时写配置的路径必须备份并在 finally 还原；窗口能力原生测试仅操作自己创建的窗口。`latest-run.json` 从 RUNNING 开始逐组写入 PASS/FAIL，未执行的组保持 PENDING，不使用旧日志冒充本次通过。

18个含离屏绘图的测试入口统一使用 `WpfTestRender.Render(bitmap, visual)`，在RenderTargetBitmap渲染前后以Background优先级处理Dispatcher队列，避免没有Application.Run消息循环的同步测试积累WPF工作。`scripts/common.ps1`编译任何测试时会自动加入 `tests/WpfTestRender.cs`，无需在各测试或生产 `build.rsp` 重复列入。纯逻辑测试也可以共用该编译配置。调用必须位于Measure/Arrange/OnRender之外的安全边界；需确定性状态的测试先停止桌宠计时器，避免泵消息时意外推进角色。不得吞异常或跳过预览来绕过失败。此辅助类不进入正式EXE。

## 打包

```powershell
.\package.cmd -Version 2.12 -InstructionsPath .\docs\usage-v2.12.txt
```

脚本检查程序集版本与目标版本一致。发布ZIP只含 `原神启动.exe` 与 `使用说明.txt`，并验证EXE哈希及ZIP条目。预览放在同版本 `previews/`，不混入运行包。

仅更新V2.12龙女素材使用 `build_assets.py --refresh-v212`。它替换27个龙女资源，源图与裁切参数在 `assets/generated/gpt6a-v212-*`。正式资源仍为208项，V2.11的181个其他条目应逐字节保留。`build_gpt6a_v212.py`只裁切、等比缩放和定位，原生成alpha与颜色保持。完整重建入口仍是 `build.cmd -RebuildAssets -Python ...`。刷新与完整重建均应核对正式资源内容及裁切边界；历史定向刷新入口继续保留用于维护。

`.gitattributes` 规定 C#/PowerShell/CMD/RSP 为 CRLF、Markdown/JSON/Python为LF；二进制不作换行转换。没有对历史仓库做全量换行重写。旧根目录构建和 Python 缓存归档说明见 [V2.6 整理记录](cleanup-v2.6.md)。历史使用说明和验证文档保留原路径，通过[索引](INDEX.md)导航，避免旧发布链接失效。

含中文路径或界面文案的PowerShell源码使用UTF-8 BOM，以兼容Windows PowerShell 5.1；RSP模板由脚本显式按UTF-8读取，C#编译指定65001代码页。

## 战斗助手模块边界

`GenshinCombatPlanner.cs` 负责编辑面板和攻略展示；同一窗口的角色拥有表和资料更新入口放在 `CombatRosterUi.cs`。`CombatRoster.cs` 负责拥有表持久化、名称别名、缺员计算和稳定排序。`CombatPlannerPresets.cs` 读取/校验/导入攻略目录；`CombatPlannerModel.cs` 负责通用估算；`CombatPanelOcr.cs` 负责本机截图识别和字段解析。

角色名单是名称快照，配队是经过人工核验的四人攻略。添加新攻略及角色通常只需导入 JSON；新的伤害机制需要修改估算模型。外部资料不包含脚本，不能改变计算代码。详细格式、数据边界和更新步骤见 [V2.7 资料更新指南](roster-updates-v2.7.md)。
