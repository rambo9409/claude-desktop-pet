# 原神启动 V2.3 验证记录

验证日期：2026-10-27。程序集与文件版本：2.3.0.0。

## 本次变化

- 软件标题、右键菜单主标题、窗口标题、EXE 和 ZIP 发布名称更新为“原神启动”；继续使用原有 KoloPet 设置目录。
- 珂洛施法新增 WPF 金色法阵、星光牵引线、目标窗口描边、初始与目标位置虚线。优先当前屏幕露出的窗口；跨屏目标使用牵引。实际读取到位置或尺寸改变且稳定后才判定成功。
- 每日看点扩展到10栏，增加 GitHub、AI、股票、汽车、游戏、美女·时尚；固定分类导航和每来源缓存状态，保留折叠、排序、重开布局及角色主题。
- 工具新增 GPT 重置预测，接入堂云 AI 公开 JSON。显示模型概率、北京时间、历史常见时段、间隔图和历史记录，每10分钟异步刷新，失败时保留带标记的快照。

## 自动检查

生产编译无警告。13组回归全部通过，完整清单见 `artifacts/tests/latest-run.json`；本次运行时间为北京时间09:16:20–09:17:06。

| 检查 | 结果 |
| --- | --- |
| DesktopMovement | 45455项：跨屏、DPI、拖拽、跳跃、攀爬、跑墙、顶部转角和取消 |
| Effects | 66项：胡桃跑动起步、蝶化、反向恢复及中断；96帧预览 |
| Gait | 284项：四角色走跑节奏、速度、移动模式与持久化 |
| MenuTheme | 四角色菜单与子菜单通过，新增两个工具入口、对比度及紧凑布局通过 |
| Motion / SpeechLayout | 动作播放通过；互动文字固定位置、字体及屏幕边缘布局通过 |
| Wings | 3316项：展翼、拖拽转下落、收翼、动作中断；120帧预览 |
| Highlights | 47项离线检查，包含10来源、GitHub排序、RSS时效、缓存迁移与失败缓存年龄 |
| GlideFall | 62745项：缓降、甩出、尺寸、帧率、负坐标显示器与窗口落地 |
| Nightsoul | 1542项：夜魂路线校准、真实距离消耗、耗尽展翼、60秒恢复、HUD与切换 |
| WindowAbility | 301项：模拟窗口、实际Win32缩小/牵引、边界确认、冷却、取消、特效窗口与焦点保持 |
| GptReset | 94项离线检查：解析、时区、来源、缺失值、过期、倒计时、链接、缓存、四主题、560×480、滚动保持 |
| VisualSmoke | 10栏折叠、分类跳转、WPF拖放事件、排序持久化、刷新、四主题、620×480及错误/空状态通过 |

窗口能力的原生测试仅操作测试自己启动的辅助进程：核对 HWND 与 PID，调用生产 `NativeWindowAbilityDesktop.Refresh/SetBounds`，读取实际缩小与牵引后的边界，并验证前台窗口未改变。没有调用候选窗口枚举或修改用户应用；测试结束销毁辅助窗口和特效层。

VisualSmoke 检查 WPF 事件与布局，没有模拟物理鼠标拖放。以上测试顺序执行，涉及角色与布局的原有配置在结束时恢复。

## 真实数据与视觉检查

- 十个看点来源实际连接均成功。首次日志 `artifacts/v23/highlights-live.txt`；随后真实预览抓取57条，快照位于 `artifacts/v23/highlights-live-data.json`。两次探测没有改写用户资讯缓存或布局。
- GitHub 使用公开搜索 API，取近90天创建的原创、非归档项目，按累计 Fork 排序；不把累计数字标为日增长。
- 新闻采用 Bing RSS，数量不足时补充 Google RSS，只保留最近7天发布的内容。虎扑继续提供 NBA。
- GPT 预测通过生产 .NET HttpClient 实际访问 `https://tangyun.ai/api/site/reset-radar` 成功。数据来源、字段及时间规则记录在 `docs/gpt-reset-data-source.md`。
- 预测截图使用2026-10-27公开响应快照，数据未硬编码进程序。没有明确的截止时间时不显示倒计时；界面说明这是第三方全站观察，与个人账号额度不同。
- 已目视检查四主题、预测窄窗口、历史列表、每日看点真实内容与 GitHub 页、施法缩小/牵引图。发布预览在 `release/V2.3/previews/`。

## 启动与资源

最终 EXE 隐藏启动存活3秒，以测试进程 PID 查到标题为“原神启动 · 瓦雷莎”的实际窗口，版本及产品名正确，没有新增崩溃日志，然后关闭该测试进程。详见 `artifacts/v23/startup-smoke.json`。启动检查在20秒首次施法等待之前结束。

角色资源未改变：`assets/characters.zip` 共123条目，SHA-256 仍为 `932eae3140ea536e134b60d59f1420955d818ffb58fb52ef3f88dbe55aa6ccf4`。施法新增图形由 WPF 实时绘制，无需新增位图素材。

## 发布文件

- `release/V2.3/原神启动-V2.3.zip`：25,448,100字节，仅包含 `原神启动.exe` 与 `使用说明.txt`。
- EXE：25,648,640字节，根目录与发布副本校验一致。
- ZIP SHA-256：`2bbd7cc03174cecd0724909095770288879471870cb7028b76da781eff01aef3`
- EXE SHA-256：`a2281dbf339e5810e2f1704edb3298d4e6204ea3dc7ea867ad1a967d0421f2e1`
- 历史 V2.2 ZIP 校验仍为 `ca06de5e3b4da8b5addb9d570a8aeb0e6dd0207cf91fde9de8a110900dfe8669`，历史发布未改动。

```powershell
& ./scripts/build.ps1
& ./scripts/test.ps1 -Suite DesktopMovement,Effects,Gait,MenuTheme,Motion,SpeechLayout,Wings,Highlights,GlideFall,Nightsoul,WindowAbility,GptReset,VisualSmoke
& ./artifacts/tests/VisualSmoke.exe --live-preview
& ./artifacts/tests/GptResetTests.exe --live
& ./artifacts/tests/WindowAbilityTests.exe --preview
& ./scripts/package.ps1 -Version 2.3 -InstructionsPath './docs/usage-v2.3.txt'
```
