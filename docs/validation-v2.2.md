# V2.2 验证记录

验证日期：2026-10-26。生产版本：2.2.0.0。

## 实现与检查

| 范围 | 结果 |
| --- | --- |
| 珂洛新造型 | 按右侧橙发参考更新主图、慢走、跑步、攀爬和四帧举手；检查透明边距、脚底基线、攀爬接触点和动作接入 |
| 瓦雷莎夜魂 | 1542 项检查：真实左下起跑到顶部中间、距离扣除、有限窗口路径剩余时间、半墙/顶边耗尽、立刻展翼、普通跑步回退、60 秒恢复、切换角色、HUD 不裁切 |
| 滑翔速度 | 62745 项检查：不同大小与帧率、向上/向下甩出限速、缓慢持续下降、负坐标屏幕、窗口落地与珂洛普通重力 |
| 窗口能力 | 273 项检查：筛选、缩小/拉近、最大化恢复、跨屏边界、先举手再移动、5 分钟冷却与持久化、窗口失效、取消与真实 WPF 主循环接入 |
| 原有移动 | DesktopMovement 45455 项通过；原有跨屏、跳跃、攀爬、屏幕/窗口跑墙、转角和动作中断覆盖保留 |
| 动作与显示 | Effects 66、Gait 284、Wings 3316 项通过；MenuTheme、Motion、SpeechLayout 通过 |
| 今日看点 | Highlights 20 项与 VisualSmoke 通过，覆盖四主题、头像、折叠/排序/重开、滚动状态与窄窗口布局 |
| 启动 | 最终 EXE 启动存活 3 秒，通过后关闭该测试进程；版本信息为2.2.0.0 |

共12组自动检查通过。最终修正了首次窗口定位之前 HUD 使用未初始化坐标的问题；Motion、SpeechLayout、Wings、Highlights、GlideFall、Nightsoul、WindowAbility、VisualSmoke 随后全部复验通过。各组日志在 `artifacts/tests/`；最后一批8组的运行清单在 `artifacts/tests/latest-run.json`。

窗口能力用模拟窗口适配器验证，不修改用户真实应用窗口。WPF 主循环验证使用透明、非激活测试宿主。今日看点使用离线数据，不把网络可用性或物理鼠标拖放当作本次已验证项。

顶边耗尽转滑翔时根据实际透明素材边距限制位置，保留角色所在高度，避免透明窗口顶部钳制造成瞬移。

## 资源

`assets/characters.zip` 共123条目：120张独立角色帧和3张风之翼。珂洛25张，其他三位角色共98个资源条目与V2.1逐字节一致。资源验证详情在 `artifacts/v22/kolo-assets-validation.json`。

新素材使用内置 imagegen。提示词：`assets/generated/kolo-v22-prompts.md`；裁切参数：`assets/generated/kolo-v22-crops.json`。生产构建只做裁切、等比缩放、位置归一和打包，已删除旧珂洛调色代码。

资源 ZIP SHA-256：`932eae3140ea536e134b60d59f1420955d818ffb58fb52ef3f88dbe55aa6ccf4`

## 发布

- `release/V2.2/珂洛桌宠-V2.2.zip`：25,428,534字节，仅包含 EXE 与使用说明。
- EXE：25,605,120字节，根目录构建与发布目录副本校验一致。
- ZIP SHA-256：`ca06de5e3b4da8b5addb9d570a8aeb0e6dd0207cf91fde9de8a110900dfe8669`
- EXE SHA-256：`4759ada57740894144cbf48fae0036af0892d2e40c681f5a397a5c49e236ad54`
- V2.1 ZIP SHA-256 仍为 `7f7410a1ffb408626b23e19b722f7304aa3a3d033bae765c09dcc679828aa80e`，历史包未改动。

新预览位于 `release/V2.2/previews/`：四角色总览、珂洛逐帧动作图、珂洛动作GIF、夜魂奔跑与缓降GIF。夜魂演示由实际 WPF 主循环渲染240帧，每帧40毫秒。

```powershell
& ./scripts/build.ps1
& ./scripts/test.ps1 -Suite DesktopMovement,Effects,Gait,MenuTheme,Motion,SpeechLayout,Wings,Highlights,GlideFall,Nightsoul,WindowAbility,VisualSmoke
& ./artifacts/tests/NightsoulTests.exe --preview
& ./scripts/package.ps1 -Version 2.2 -InstructionsPath './docs/usage-v2.2.txt'
```
