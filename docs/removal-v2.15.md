# V2.15 移除GPT重置预测

2026-11-05，程序集2.15.0.0。按用户要求移除GPT重置预测功能。

## 变更范围

- 删除工具菜单入口、窗口持有和生命周期调用，以及`src/GptResetPrediction.cs`中的界面、抓取、刷新和缓存实现。
- 清理`build.rsp`编译项、`GptResetTests`与对应JSON响应快照；菜单和角色工具测试只检查保留的功能。
- 当前测试为21组默认回归，加VisualSmoke共22组。历史版本中的测试数量及结果保留原样。
- 每日看点、战斗循环助手、六个角色的形象与专属能力继续保留；本版不改动作素材。

旧版可能留下`%LocalAppData%\KoloPet\gpt-reset-cache.json`。升级时不主动删除用户目录，当前程序不再读取或刷新该缓存，也不再请求预测接口。历史发布包、使用说明和验证记录保留，数据源文档已标为历史资料。

## 接手检查

当前生产源码和编译清单不应残留预测窗口类型、入口或数据源URL。后续功能维护以README及当前程序集为准，不要根据历史文档恢复已移除模块。

## 本版验证

正式构建、22组完整回归、移除检查、正式EXE启动和发布校验全部PASS。本轮证据保存在`artifacts/v215/`，不沿用历史版本结果。

- 完整回归从2026-11-05 10:09:43.737至10:10:41.298（UTC+08:00），见`full-test-run.json`。AiCharacters 297项、PointerInteraction 268项、KoloEasterEgg 370项、Notebook 122项、KoloPortal 18409项。AiCharacters删除了被移除窗口的2项主题断言。
- `removal-verification.json`确认3个专属源码/测试/快照文件已删除；51个当前源码及编译项检查通过，正式程序集不含该功能或接口标记。
- 角色素材包未改，SHA256仍为`3d3eaea72831d16b31e89e186cd8a2a1b90ded0b8d5bd7f4d5ee4eb2effdd0d9`。
- `startup-smoke.json`确认2.15.0.0正式EXE启动观察3秒，无新增崩溃日志。本次不重复动作素材审看或物理双屏拖拽手测。

发布校验时间2026-11-05 10:12:23.375（UTC+08:00），见`release-hashes.json`与`package-verification.json`。ZIP只含EXE和使用说明，解包与最终构建及`docs/usage-v2.15.txt`逐字节一致；说明8836字节，SHA256 `e737edcec104b647cce739ececd7319cb926b5a7bc5f54fe4418afa684276081`。

| 发布文件 | 字节数 | SHA256 |
| --- | ---: | --- |
| `release/V2.15/原神启动.exe` | 43537920 | `b1eb0349a1737cb54d091f46606432e8ce690542817e458b2ec81e20b88b3abd` |
| `release/V2.15/原神启动-V2.15.zip` | 43131106 | `21008990fcdd7f333ac7cd8f5a184d807431064d11cd304e611fa36210150410` |

历史V2.14包SHA256保持`fba36238eacc80eb1460ec85fb29b7f6844e51d1a9686040326c3dc009794520`，未被覆盖。
