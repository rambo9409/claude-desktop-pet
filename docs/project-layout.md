# V2.6 工程与构建

## 目录

| 路径 | 用途 |
| --- | --- |
| `src/` | C# 5 / WPF / .NET Framework 4.8 生产代码 |
| `build.rsp` | 唯一生产源文件、资源与引用模板，生产和测试共用 |
| `build.cmd`、`test.cmd`、`package.cmd` | 无需更改系统执行策略的 Windows 入口 |
| `scripts/` | 编译、测试、打包与预览编码 |
| `tests/` | 测试源码与历史薄包装入口 |
| `assets/characters.zip`、`assets/combat-teams.json` | 内嵌角色资源与有来源的静态配队目录 |
| `assets/generated/`、`assets/source/` | 当前和历史源素材、提示词、裁切参数 |
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
# 16组默认回归；加看点交互检查后共17组，无需Python
.\test.cmd -SkipPreviews -IncludeVisualSmoke
# 单独战斗助手
.\test.cmd -Suite CombatPlanner -SkipPreviews
# 使用明确的Python编码预览
.\test.cmd -Python C:\Python312\python.exe -IncludeVisualSmoke
```

多组选择可在 PowerShell 内调用：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& './scripts/test.ps1' -Suite CombatPlanner,GanyuDualScreen -SkipPreviews"
```

默认组为 DesktopMovement、Effects、Gait、MenuTheme、Motion、SpeechLayout、Wings、Highlights、GlideFall、Nightsoul、WindowAbility、GptReset、CombatPlanner、PointerInteraction、GanyuTraversal、GanyuDualScreen。VisualSmoke 是额外一组，默认 `--verify` 后退出，不启动常驻交互窗口。

按顺序运行，避免共享配置互相干扰。测试中需要暂时写配置的路径必须备份并在 finally 还原；窗口能力原生测试仅操作自己创建的窗口。`latest-run.json` 从 RUNNING 开始逐组写入 PASS/FAIL，未执行的组保持 PENDING，不使用旧日志冒充本次通过。

## 打包

```powershell
.\package.cmd -Version 2.6 -InstructionsPath .\docs\usage-v2.6.txt
```

脚本检查程序集版本与目标版本一致。发布ZIP只含 `原神启动.exe` 与 `使用说明.txt`，并验证EXE哈希及ZIP条目。预览放在同版本 `previews/`，不混入运行包。

`.gitattributes` 规定 C#/PowerShell/CMD/RSP 为 CRLF、Markdown/JSON/Python为LF；二进制不作换行转换。没有对历史仓库做全量换行重写。旧根目录构建和 Python 缓存归档说明见 [V2.6 整理记录](cleanup-v2.6.md)。历史使用说明和验证文档保留原路径，通过[索引](INDEX.md)导航，避免旧发布链接失效。

含中文路径或界面文案的PowerShell源码使用UTF-8 BOM，以兼容Windows PowerShell 5.1；RSP模板由脚本显式按UTF-8读取，C#编译指定65001代码页。
