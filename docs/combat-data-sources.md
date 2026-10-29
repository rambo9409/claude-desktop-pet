# 配队来源与案例核验

核验日期：2026-10-29（北京时间）。此文与 `assets/combat-teams.json` 的版本2目录对应。

## 当前游戏版本

官方的 [7.1下半祈愿公告](https://genshin.hoyoverse.com/en/news/detail/166634) 标注2026-10-13至11-03；[官方7.1更新公告](https://genshin.hoyoverse.com/m/es/news/detail/166383) 日期为9月23日。因此本次以**7.1**为已经上线的版本。公告部分页面为动态网页，本次检索可读到官方索引摘要；没有把空的页面正文视为更详细的证据。

HoYoverse通过 [Games Press发布的7.2公告](https://www.gamespress.com/ko-KR/Genshin-Impact-Version-72-Arrives-on-November-4-as-Teyvats-Seven-Natio) 计划11月4日上线。目录不纳入尚未上线角色的预测配队，也不会在那一天自动改变版本号。

## 值得借鉴的案例

| 案例 | 实际提供什么 | 本项目采用的方向 |
| --- | --- | --- |
| [gcsim说明](https://docs.gcsim.app/)、[入门](https://docs.gcsim.app/get-started/) | 基于输入队伍与动作进行战斗模拟，输出角色/技能伤害、能量与反应等统计；可编写或导入共享模拟 | 将动作与面板分开；精确计算应使用模拟引擎，当前通用乘区模型不能自称gcsim级别 |
| [Genshin Optimizer官方仓库](https://github.com/frzyc/genshin-optimizer) | 圣遗物配装优化工具，仓库说明有GOOD导入数据格式 | 保留结构化输入和可替换识别接口；本版尚未实现GOOD导入或优化引擎 |
| [KQM角色指南](https://keqingmains.com/q/skirk-quickguide/) | 按角色机制和命座给队友、循环、资源要求，标注更新版本 | 每队独立来源、适用范围和手法依据；把“来源循环”与本项目简化建议区别开 |

以上是设计参考和可打开的外部工具，不是已安装依赖或内置引擎。

## 十套预设的依据

| 目录ID | 证据与本次处理 |
| --- | --- |
| `vesna-comfort` | [Icy Veins构筑与轮转](https://www.icy-veins.com/genshin-impact/vesna-guide-best-builds) 与 [队伍页](https://www.icy-veins.com/genshin-impact/vesna-team-guide) 提供相同四人组。使用其舒适队动作顺序；数值为带条件的充能参考 |
| `vesna-faruzan` | 同一构筑页的法露珊队；保留六命与无专职生存位条件，不保留“最高上限”断言 |
| `vesna-f2p` | [Enjoygm队伍页](https://www.enjoygm.com/blog/genshin-impact/vesna-team-guide) 给四星队友变体与开场顺序，主C段明确为简化练习。页面标题检索有错配且正文抓取曾超时，采用能读取的正文段落并以Icy Veins机制交叉核验；不据此排名 |
| `flins-aino`、`flins-columbina` | [KQM菲林斯](https://keqingmains.com/q/flins-quickguide/) 有砂糖对应队伍和特殊短Q连段。替换旧交接缺证据的希诺宁连段；充能表自身标为Luna V，不称7.1新测数据 |
| `nefer-lunar-bloom` | [KQM奈芙尔](https://keqingmains.com/q/nefer-quickguide/) 为Luna II基础资料；改为有明确记录的爱诺队，纠正菈乌玛长E消耗资源的问题。队友开场标注为职能推导，非来源唯一最优循环 |
| `skirk-freeze` | [KQM丝柯克](https://keqingmains.com/q/skirk-quickguide/) 明确有7.1更新，提供Vodyanitsa队与基础N5连段。使用易练版本，并注明延迟/讨龙约束 |
| `neuvillette-hydro` | [KQM那维莱特](https://keqingmains.com/neuvillette/) 的零命芙宁娜/万叶/希诺宁轮转；补齐希诺宁两次普攻和中途刷新，避免只写E就切走 |
| `arlecchino-vaporize` | [KQM阿蕾奇诺](https://keqingmains.com/q/arlecchino-quickguide/) 的夜兰/班尼特/蓝砚例队；保留蓝砚挂火与零命动作条件 |
| `raiden-national` | [KQM雷电将军](https://keqingmains.com/raiden/) 的香菱/行秋/班尼特例队与易用连段；辅助顺序明示为练习建议，纠正“四个角色都不是五星”的旧描述 |

新角色英文名沿用原始来源，未核实官方中文名时不自创译名。来源中各类角色有配装假设，目录没有充分证据支持统一SS/S+等级，因此去掉了这些强度标签。

## 后续维护要求

1. 核验当前官方公告、攻略版本和URL内容；每条更新 `verifiedAt`、`versionScope`、`rotationBasis`。
2. 保持四人队真实可执行；特别检查特殊资源、点/长E、短/长Q、二段动作、切人和带讨龙的目标。
3. 不支持的机制设 `supportsEstimate:false`，不能用零能量或虚构倍率绕过模型限制。
4. 检查更改的角色是否也需要更新职责、缩放和数值提示。角色模板只是输入占位，不是装备数据库。
5. 日期较新只能说明本地覆盖文件优先级，不等于信息可靠；提交前运行CombatPlanner回归并检查手法卡片实际显示。
