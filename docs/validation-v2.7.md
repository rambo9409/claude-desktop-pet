# 原神启动 V2.7 验证记录

日期：2026-10-29（北京时间），程序集版本2.7.0.0。

## 完成范围

战斗助手新增独立保存的角色拥有表、搜索与别名匹配、自定义角色、批量勾选、取消编辑、完整四人筛选及缺员提示。筛选和资料更新不改写当前四人面板，缺员队只能浏览。基础名单127项是有来源的名称快照，不宣称官方实时完整名册。

配队目录扩为14套，新增4套经典队，均有KQM来源并仅提供手法。支持导入完整JSON目录和重新加载本地资料；校验版本、日期、成员唯一性、来源、完整性与大小，原子替换并保存前一份有效覆盖。新名字能补入拥有表，新机制不自动获得伤害估算。详细边界见[资料更新说明](roster-updates-v2.7.md)。

刷新目录时保留已应用队伍的机制限制；重启后原预设已不存在时保留输入、停止估算，需恢复资料或明确转为自定义。没有增加自动联网更新或任意角色组合优化。

## 回归

最终回归为北京时间11:45:37–11:46:32，全套18组通过，含原17组及新增CombatRoster。证据清单：`artifacts/v27/full-test-run.json`；逐组日志：`artifacts/tests/`。本次同时完成蝶化与展翼GIF编码。

| 套件 | 结果 |
| --- | --- |
| CombatRoster | 207项：未设置/空表区分、四人匹配、缺员排序、别名、旅行者元素、未来名字、安全存储、损坏备份、锁定写入、过大拥有表保存保护、无效/过旧/过大目录拒绝、成功更新备份、真实WPF筛选、缺员不能载入、四主题、最小尺寸、搜索/勾选/取消/自定义保存、更新及重启后失效预设保护 |
| CombatPlanner | 119项：通用计算、时间边界、错误提示、存档、本机OCR、四主题和攻略/估算能力边界 |
| DesktopMovement | 45454项 |
| Effects / Gait / Wings | 66 / 284 / 3316项 |
| GlideFall / Nightsoul | 62745 / 1542项 |
| WindowAbility / PointerInteraction | 337 / 120项 |
| GanyuTraversal / GanyuDualScreen | 4158 / 61849项，含42组双屏场景 |
| Highlights / GptReset | 47 / 94项 |
| MenuTheme / Motion / SpeechLayout / VisualSmoke | 四主题、动作、固定文字位置、看点10版块折叠排序与窗口布局通过 |

Windows本机OCR本次用 `zh-Hans-CN` 从本地测试图片识别7个面板字段。测试不上传图片，不代表任意游戏截图都能无误识别；导入仍需预览核对。原生窗口测试仅操作自建窗口。使用用户配置的原有测试按各自finally恢复；拥有表和目录导入测试使用隔离路径，WPF窗口以不持久化模式运行，未替用户勾选角色。

最新拥有表预览目录为 `artifacts/v27/checks-114623723/`。主任务实际查看了相同UI的前一轮拥有表、缺员提示、甘雨主题与小窗口；其他主题由渲染和断言覆盖。最终轮仅新增存储大小保护，UI未改变。预览复制到 `release/V2.7/previews/`。窗口能力与看点沿用原回归，不声称本次通过物理鼠标拖动或在线数据源实测。

## 构建与启动

最终 `build.cmd` 通过；保持C#5、net48与动态Windows SDK引用。只规范本次修改文本的换行与BOM，没有全仓格式化或重绘素材。

正式EXE以Hidden方式启动存活3秒，按本次PID识别到“原神启动 · 瓦雷莎”，产品名“原神启动”、版本2.7.0.0，无新增崩溃日志，随后只关闭测试进程。证据：`artifacts/v27/startup-smoke.json`。

## 发布

`release/V2.7/原神启动-V2.7.zip`，25,502,527字节；EXE为25,813,504字节。根目录与发布EXE哈希一致，ZIP严格两条目：EXE与使用说明。目录示例和预览在ZIP外。

- ZIP SHA-256：`d92580b5e25165e5be847cf5aca2eccf2eb4fa4b732547571862b425d399fae4`
- EXE SHA-256：`1b05800f0b8e04a762f1915ed85bcc34ab2293a4120f071ef5f7280debf6e69d`
- 角色资源ZIP保持：`932eae3140ea536e134b60d59f1420955d818ffb58fb52ef3f88dbe55aa6ccf4`
- V2.6发布ZIP保持：`5210f631ed87c6f7333474bb0da37273afa63297423b7b8501bf80af165bd047`

```powershell
.\build.cmd
.\test.cmd -SkipPreviews -IncludeVisualSmoke
.\package.cmd -Version 2.7 -InstructionsPath .\docs\usage-v2.7.txt
```

需要生成GIF时去掉SkipPreviews并通过 `-Python` 指定含Pillow的Python。本次完整回归用本机已安装的解释器，路径未写进工程默认配置。资料导入测试只证明格式与保存行为，不证明未来提供的任意攻略内容真实可靠。
