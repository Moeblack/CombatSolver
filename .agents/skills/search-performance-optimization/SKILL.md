---
name: search-performance-optimization
description: 在战斗语义已证明正确后，审计或修改 CombatSolver 的候选展开、Beam 保路、终局排序、评分、剪枝、Pareto、转置、预算、分配、No-GC 或 Steam 实机卡顿。
---

# CombatSolver 搜索质量与性能优化

## 适用边界

先证明同一起点、同一动作的 actual/simulated 状态一致。存在根快照、语义或续用偏差时转 `combat-semantic-change`，不要调搜索掩盖。

读取 `docs/ARCHITECTURE.md` 的 Search 章节。当前搜索职责已拆开：

- `Expansion` 产生候选；
- `StateEvaluation` 计算快照、威胁和评分特征；
- `BeamRetentionPolicy` 决定中间候选保留；
- `FinalPlanOrdering` 决定终局路线；
- `SearchRunContext` 拥有单次运行指标、转置和缓存；
- `CombatSearchCoordinator` 组织主搜索与药水反事实。

不要把所有问题重新塞回 `Solve` 或用一个总分同时承担保路与终局政策。

## 1. 建立可比较基线

固定同一源码版本、快照、预设和测试模式，记录：

- phase / boundary、searched turns / shuffles；
- expanded、transitions、choice branches；
- dominance / transposition / repeatable pruned；
- elapsed、allocated bytes、bytes/transition；
- GC 次数、总/最大暂停、最大帧和 `>50ms` 帧；
- 战损、药水、结束回合、动作序列和 unexpected replans。

至少有三类基线：目标质量、不可退化质量、固定长线性能。当前稳定场景从 `docs/TEST_MATRIX.md` 选择，不把历史数字硬编码成永久阈值。

`-VerifyIncrementalSearch` 会逐转移执行完整回放，只用于正确性，不能与性能门槛组合，也不能引用其时间或分配作为生产性能。

## 2. 判断瓶颈所在职责

- **预算不足**：更高预算找到更好合法路线，低预算有明确时间/节点边界。
- **展开截断**：目标动作或选牌未进入 frontier，检查 `Expansion` 和单节点分支预算。
- **中间候选丢失**：候选出现后被状态去重、Beam 通道、配额、Pareto 或转置淘汰，检查 `BeamRetentionPolicy` / `Retention`。
- **终局政策**：完整候选存活但未被选择，检查 `SearchFeatures` 与 `FinalPlanOrdering`。
- **模拟成本**：质量稳定，但 fork/action/round/snapshot/fingerprint 阶段过重。
- **GC / 主线程卡顿**：分别检查 No-GC 是否保持、堆分区、其他 Mod 分配、worker yield 和主线程回调。

先定位候选在哪一层消失，再修改该层。终局政策问题不能通过提高 Beam 分数偷偷解决。

## 3. 保路与终局规则

- 多样性用有上限的显式通道：防守、进攻、能力铺垫、资源、控制、药水状态、集火、复活窗口、破坏性选择等。
- 只有真正独立的目标进入全局 Pareto；增加维度前比较支配剪枝、节点与分配。
- 药水/无药配额替换候选时保护已标记代表；最终药水价值仍由反事实与 `FinalPlanOrdering` 决定。
- 持续能力、未来资源和延迟伤害可帮助中间保路；终局比较实际胜负、战损、药水、卖血和敌方状态。
- 通用上下文可按单敌/多敌、普通/Boss 调整；不要按具体 encounter 或单卡硬编码路线。
- 纯启发式特征不进入战斗状态键或 `ContinuationStamp`。

## 4. 性能所有权

- `SearchRunContext` 是单次运行可变指标、转置和缓存的所有者；不要把这些字段退回 solver 入口或静态全局。
- Runtime 拥有 `SearchGcPolicy`，Search 只通过 `SearchFramePressureSignal` / `SearchWorkPacer` 消费节流信号。
- 优先避免无价值候选、Fork 和快照产生；No-GC 区内释放引用不会返还预算。
- 区分 transitions 增长与 bytes/transition 增长，用阶段指标定位实际热点。
- No-GC 同时观察配置预算、SOH/LOH、是否保持到搜索退出和首次长帧时的 expanded。
- 收益小且扩大语义验证面的微优化保留简单实现。

## 5. 实验与验证

1. 每轮只改变一个可解释因素，失败实验立即撤回；
2. Release 构建并运行 `tools/verify-refactor-boundaries.ps1`；
3. 先跑目标首轮质量，再跑完整自动战斗，固定 `Instant / 0 秒`、零非预期重算；
4. 跑与改动相关的药水、卖血、延迟伤害、复活、选择等不可退化场景；
5. 最终候选单独执行一次增量等价，数字不用于性能比较；
6. 改动涉及 coverage/state 分类时运行对应 CoverageCatalog verify；
7. 性能最终结论来自关闭诊断的正常可见 Steam 会话。

职责迁移时同步 `docs/ARCHITECTURE.md` 和结构门禁。搜索行为或指标变化同步开发笔记与测试矩阵。普通优化直接提交；只有用户要求版本或 ZIP 时才转 `release-gate`。
