# V2.5 工程目录与构建

## 目录职责

| 目录或文件 | 内容 |
| --- | --- |
| `src/` | 当前生产 C# 源码：角色窗口、动作、甘雨跨屏蹬跳与靠近起跳、输入手势、蝶化、滑翔、夜魂、窗口能力与特效、菜单、看点、GPT 重置预测和素材读取 |
| `build.rsp` | 唯一生产编译清单、程序集引用和内嵌资源声明；测试在此基础上替换入口 |
| `build_assets.py` | 从本项目源素材构建 `assets/characters.zip` 和静态/动作预览 |
| `assets/source/kolo/` | 旧版默认角色的基础源帧，保留作历史参考 |
| `assets/generated/` | 当前四位角色、动作和风之翼源图，以及 imagegen 提示词；历史源图保留 |
| `assets/previews/` | 方便查看的角色、动作预览，不参与程序启动 |
| `assets/characters.zip`、`assets/ClaudePet.ico` | 编译时内嵌资源；发布后的 EXE 不需要旁置图片或 ZIP |
| `tests/` | 测试 C# 和兼容原命令的薄包装 PS1，只保留源文件 |
| `scripts/` | 统一构建、测试、GIF 编码和发布打包入口 |
| `artifacts/tests/` | 可重新生成的测试 EXE、编译响应文件、日志、帧图和 GIF |
| `release/V*/` | 按版本保留的发布文件；早期版本可用于回退 |
| `archive/` | 已退出当前构建的早期入口、散落旧帧、检查产物及历史测试输出 |
| `docs/` | 工程说明与整理记录 |

## 构建

需要 Windows、.NET Framework 4.8 引用程序集，以及系统自带 .NET Framework C# 编译器。
首次从源素材重建需要 Python、Pillow 和 NumPy；已有 `assets/characters.zip` 时编译程序不需要 Python。

```powershell
# 使用已打包素材编译
& ./scripts/build.ps1

# 从项目内源图重新生成素材后编译
& ./scripts/build.ps1 -RebuildAssets
```

Python 可通过 `-Python 'C:/path/to/python.exe'` 或 `KOLO_PYTHON` 环境变量指定。未指定时，优先使用当前用户的 Codex 依赖环境，其次查找 PATH 中的 Python。脚本不包含固定用户名。

## 验证

```powershell
# 默认十五组回归检查，顺序执行，避免共享配置相互干扰
& ./scripts/test.ps1

# 指定测试组
& ./scripts/test.ps1 -Suite Wings,Effects

# 只检查动作/渲染断言，跳过最后的 GIF 编码
& ./scripts/test.ps1 -Suite Wings -SkipPreviews

# 今日看点的 WPF 折叠、排序与重新打开验证
& ./scripts/test.ps1 -Suite VisualSmoke
```

默认组为 DesktopMovement、Effects、Gait、MenuTheme、Motion、SpeechLayout、Wings、Highlights、GlideFall、Nightsoul、WindowAbility、GptReset、PointerInteraction、GanyuTraversal、GanyuDualScreen。另行运行 VisualSmoke，完整验证共16组。
`tests/*Tests.ps1` 继续可用，但都转到统一入口；VisualSmoke 默认使用 `--verify`，避免无意启动常驻交互窗口。

所有编译均读取当前 `build.rsp`，不会使用历史 RSP 中的旧源码列表。日志和预览在 `artifacts/tests/`。一组失败立即退出；`latest-run.json` 在本次运行开始时写入 RUNNING 和所选测试组，随后逐组更新为 PASS 或 FAIL。编译失败也会写入本次日志，未执行的组保留 PENDING。

## 打包

```powershell
& ./scripts/package.ps1 -Version 2.5 -InstructionsPath './docs/usage-v2.5.txt'
```

使用实际存在的使用说明路径。脚本会检查 EXE 的程序集文件版本必须与目标版本匹配，再生成 `release/V2.5/原神启动-V2.5.zip`。用户运行包只包含 `原神启动.exe` 和 `使用说明.txt`，同时验证 EXE 校验和及 ZIP 条目数量。

预览文件和完整工程可单独提供，避免把数百张测试帧、旧编译器输出或历史版本混入运行包。源码工程应包含当前 `src/`、`tests/`、`scripts/`、`docs/`、`build.rsp`、`build_assets.py` 和当前 `assets/`；无需包含 `archive/`、`artifacts/`、`release/` 或根 EXE。

本次发布的验证结果与文件校验和见 [V2.5 发布验证](validation-v2.5.md)。双屏蹬跳预览保存在 `release/V2.5/previews/`。

## 历史资料

`archive/v2.0-cleanup-manifest.json` 记录每个归档文件的原路径、归档路径、大小及 SHA-256。整理过程保留原始内容；旧入口仅作历史参考，已不在生产构建中。
