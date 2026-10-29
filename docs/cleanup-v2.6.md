# V2.6工程整理记录

日期：2026-10-29。保持现有 net48 / C# 5，不安装新SDK，不重写整套项目格式。

- 新增 `build.cmd`、`test.cmd`、`package.cmd`，只在子进程中设置 PowerShell ExecutionPolicy Bypass，不更改用户或机器策略。
- `build.rsp` 改为引用模板，统一由 `scripts/common.ps1` 自动寻找 net48 Developer Pack、Framework编译器与Windows SDK WinRT元数据；生产与测试都使用展开后的RSP。
- 移除构建脚本对特定AI工具私有Python目录的依赖，支持参数、`KOLO_PYTHON`、PATH、`py -3`。
- `.gitattributes` 定义文本换行及二进制属性；没有重写历史提交或全仓库归一化换行。
- `PetStorage` 统一持久化与诊断：写临时文件并Flush后替换；替换失败保留旧文件，不退回非原子覆盖。损坏存档备份名包含GUID，避免同秒碰撞。
- README改为用户入口；CHANGELOG按版本收纳变化；HANDOFF记录模块、扩展点、测试、边界与后续工作。历史usage/validation保留路径，用索引区分当前与历史。
- 战斗助手按模型、资料、OCR、界面、桌宠入口拆分；`DesktopMovement.cs` 提取纯几何与瓦雷莎路线类，`DailyHighlightsData.cs` 提取资讯数据、缓存与服务层。桌宠运行时与资讯窗口继续保留原行为，再通过完整回归验证。

## 保留归档

移动前核对源和目标绝对路径均在项目内，只移动下列明确对象，未删除文件。归档位于 `archive/v2.6-engineering-cleanup/`，其中 `manifest.json` 保存路径、大小与SHA-256。

| 原路径 | 大小 | SHA-256 |
| --- | ---: | --- |
| `ClaudeDesktopPet.exe` | 25,605,120 | `4759ada57740894144cbf48fae0036af0892d2e40c681f5a397a5c49e236ad54` |
| `__pycache__/build_assets.cpython-312.pyc` | 46,977 | `c583f968a873f0753fc964912111d7389cc299595e9c4179df9c48dd29c70bc0` |

`archive/`、`artifacts/`、`release/`、根EXE和Python缓存继续由 `.gitignore` 忽略。其他AI克隆仓库时不依赖这些本地产物；归档目录缺失不影响构建。
