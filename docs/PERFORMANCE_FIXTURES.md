# 搜索性能回归样例

仓库只提交可审计的最小化 gameplay fixture。原始问题包、完整日志、截图、Profiler 和存档均留在 Git 忽略目录，不属于公开测试数据。

七组样例都能脱离原始问题包运行，但它们属于合成战斗根或战前状态重建，不是原生战斗状态回放。headless 数字用于比较固定工作量、累计分配和 GC 行为，不能替代正常可见 Steam 会话的帧时间结论。

完整策略测试必须逐值遵守样例设置。固定节点或 5 秒短搜只作为显式诊断变体，不能冒充完整策略结果，也不能用于降低路线质量。

## Ironclad Aeonglass Clone/Havoc 极端压力

- fixture：`coverage/unattended/search-performance-ironclad-clone-havoc-huge-deck-cards.json`
- 输入：`26` 条聚合记录；手牌 `5`、抽牌堆 `2300`，其中 `2187` 张为 `HAVOC+1 + CLONE×4`；`2302` 张视为永久牌组牌，另有 `3` 张 `DAZED`。
- 白名单：`cardId / pile / count / upgradeLevels / enchantmentId / enchantmentAmount / treatAsDeckCard`。
- 排除：原始 seed、RNG、遗物、药水、Power、存档、日志、路径、平台、账户及 Mod 环境。
- 边界：只重建造成压力的牌堆与 listener 形状，不宣称原生战斗逐动作 replay。

根隔离测量：

```bash
./tools/run-unattended-test.sh \
  --scenario-id SEARCH-PERF-IRONCLAD-CLONE-HAVOC-ROOT \
  --character-id IRONCLAD \
  --seed SEARCH_PERF_IRONCLAD_CLONE_HAVOC \
  --encounter-id AEONGLASS_BOSS \
  --ascension 10 \
  --act-index-for-test 2 \
  --enemy-current-hp 526 \
  --initial-enemy-move-ids-json '["EBB_MOVE"]' \
  --initial-player-hp 80 \
  --initial-player-max-hp 80 \
  --initial-player-energy 5 \
  --clear-run-deck \
  --clear-player-piles \
  --cards-path coverage/unattended/search-performance-ironclad-clone-havoc-huge-deck-cards.json \
  --verify-combat-root-snapshot \
  --stop-after-combat-root-snapshot-assertion \
  --timeout-seconds 120 \
  --exit-on-complete
```

固定 1 秒、DOP1 搜索 A/B：

```bash
./tools/run-unattended-test.sh \
  --scenario-id SEARCH-PERF-IRONCLAD-CLONE-HAVOC-1S \
  --character-id IRONCLAD \
  --seed SEARCH_PERF_IRONCLAD_CLONE_HAVOC \
  --encounter-id AEONGLASS_BOSS \
  --ascension 10 \
  --act-index-for-test 2 \
  --enemy-current-hp 526 \
  --initial-enemy-move-ids-json '["EBB_MOVE"]' \
  --initial-player-hp 80 \
  --initial-player-max-hp 80 \
  --initial-player-energy 5 \
  --clear-run-deck \
  --clear-player-piles \
  --cards-path coverage/unattended/search-performance-ironclad-clone-havoc-huge-deck-cards.json \
  --performance-preset-for-test Low \
  --potion-policy-for-test Smart \
  --search-max-degree-of-parallelism-for-test 1 \
  --force-short-search-only \
  --short-search-budget-override-milliseconds 1000 \
  --measure-search-phases \
  --enable-detailed-diagnostic-logs-for-test 0 \
  --expected-initial-search-phase Short \
  --expected-initial-deep-search-triggered 0 \
  --expected-initial-executable-action-count-at-least 1 \
  --stop-after-initial-solver-result-assertion \
  --timeout-seconds 120 \
  --exit-on-complete
```

基线和最终必须保持 `expanded / transitions / forks / replays`、路线、评分、终止边界与搜索策略一致；`--clear-run-deck` 必须保留，否则会把角色默认起始牌混入 `2302` 张目标牌组。当前严格 A/B 为 `5186.2 ms / 912,561,760 B` 对 `3471.0 ms / 267,554,848 B`，两侧均选择 `ENTROPY`，评分均为 `-3752001`，终止边界均为 `TimeLimit`，工作量均为 `1/3/3/2`。搜索时的生产根捕获为 `3971.045 → 2235.620 ms`；独立根测试阶段为 `4570.556 → 2215.689 ms`。

BaseLib 使用独立保守路径，不把外部 `CardModifier` 侧表当成不变数据。最终门禁 runId `7ef16ab8cf5b4760a8a27e04ac694e75` 从两张初始未修饰卡开始，验证 modifier 动态增删以及 `Amount`/`Priority`、fingerprint、choice-key、continuation 和 listener 精确跟随分支状态；Owner、父子分支和侧表容器不共享。首次 live stamp 经根 modifier 注册后仍保持稳定；空 modifier 生成牌使用稳定状态键并能被部署查回，首次遇到未登记的非空 modifier 时则保守发现并独立克隆。实现另将 `StoreSaveData` 的 `IntProperties`/`AdditionalProperties` 保序编码进三类状态键，该部分为静态审计证据。从带 `DeckVersion` 且已标记移除的源卡创建玩法生成 clone 时，modifier 独立克隆并重绑 Owner，而生成卡的 `DeckVersion=null` 且 `HasBeenRemovedFromState=false`。没有 BaseLib modifier 的根仍使用 listener/fingerprint 快速路径。

## 随机牌与复杂首回合矩阵

用户指定牌按本地化名称映射为 `ENTROPY`、`INFERNAL_BLADE`、`STOKE`、`SPECTRUM_SHIFT`、`BUNDLE_OF_JOY`、`TRANSFIGURE`、`CALL_OF_THE_VOID`、`CREATIVE_AI`、`SEEKER_STRIKE`、`CATASTROPHE`、`AUTOMATION`、`MAYHEM`、`JACKPOT` 和 `JACK_OF_ALL_TRADES`。每个场景再从对应角色牌池随机选择一次 `5` 种攻击和 `5` 种防御，并把选择冻结在 JSON 中；每种填充牌放入 `6` 张，以同时制造牌堆规模和随机/choice 压力。运行时不重新抽样，因此相同提交上的 A/B 可逐字段比较。

| 场景 | 手牌中的复杂牌 | 冻结随机填充 | 额外 Power | 合成边界 |
| --- | --- | --- | --- | --- |
| 三骑士 / Ironclad | `INFERNAL_BLADE+`、`STOKE+`、`CATASTROPHE+` | `ANGER`、`ASHEN_STRIKE`、`POMMEL_STRIKE`、`TWIN_STRIKE`、`HEADBUTT`；`DEFEND_IRONCLAD`、`SHRUG_IT_OFF`、`FLAME_BARRIER`、`COLOSSUS`、`IMPERVIOUS` | 无 | `KNIGHTS_ELITE`，HP `108/97/89`，首动作 `RAM_MOVE/HEX/POWER_SHIELD_MOVE` |
| 女王 / Regent | `BUNDLE_OF_JOY+`、`SPECTRUM_SHIFT+`、`ENTROPY+`、`JACK_OF_ALL_TRADES+` | `STRIKE_REGENT`、`SOLAR_STRIKE`、`SHINING_STRIKE`、`CRESCENT_SPEAR`、`COMET`；`DEFEND_REGENT`、`CLOAK_OF_STARS`、`BULWARK`、`PARTICLE_WALL`、`COSMIC_INDIFFERENCE` | `SPECTRUM_SHIFT_POWER×2`、`ENTROPY_POWER×2` | `QUEEN_BOSS`，星星 `3`，HP `211/419`，首动作 `STRONG_TACKLE_MOVE/PUPPET_STRINGS_MOVE` |
| 实验体 / Defect | `CREATIVE_AI+`、`AUTOMATION+`、`MAYHEM+`、`JACKPOT+` | `STRIKE_DEFECT`、`COLD_SNAP`、`COMPILE_DRIVER`、`SWEEPING_BEAM`、`GO_FOR_THE_EYES`；`DEFEND_DEFECT`、`BOOT_SEQUENCE`、`CHARGE_BATTERY`、`LEAP`、`GLACIER` | `CREATIVE_AI_POWER×2`、`AUTOMATION_POWER×1(cardsLeft=1)`、`MAYHEM_POWER×2` | `TEST_SUBJECT_BOSS`，第二 Boss，HP `111`，首动作 `BITE_MOVE` |
| 永世沙漏 / Necrobinder | `TRANSFIGURE+`、`SEEKER_STRIKE+`、`CALL_OF_THE_VOID+` | `STRIKE_NECROBINDER`、`BLIGHT_STRIKE`、`DEFILE`、`SCULPTING_STRIKE`、`REAVE`；`DEFEND_NECROBINDER`、`DEFY`、`DELAY`、`GRAVE_WARDEN`、`MELANCHOLY` | `CALL_OF_THE_VOID_POWER×2` | `AEONGLASS_BOSS`，HP `526`，首动作 `EBB_MOVE` |

牌和 Power fixture：

- `coverage/unattended/search-performance-complex-random-knights-cards.json`
- `coverage/unattended/search-performance-complex-random-queen-cards.json` 与 `search-performance-complex-random-queen-powers.json`
- `coverage/unattended/search-performance-complex-random-test-subject-cards.json` 与 `search-performance-complex-random-test-subject-powers.json`
- `coverage/unattended/search-performance-complex-random-aeonglass-cards.json` 与 `search-performance-complex-random-aeonglass-powers.json`

从仓库根目录可用同一公共后缀复现四组 canonical 请求：

```bash
run_complex_random_perf() {
  ./tools/run-unattended-test.sh "$@" \
    --ascension 10 --act-index-for-test 2 \
    --initial-player-hp 80 --initial-player-max-hp 80 --initial-player-energy 5 \
    --clear-run-deck --clear-player-piles \
    --performance-preset-for-test Low --potion-policy-for-test Smart \
    --search-max-degree-of-parallelism-for-test 1 \
    --force-short-search-only --short-search-budget-override-milliseconds 2000 \
    --measure-search-phases --enable-detailed-diagnostic-logs-for-test 0 \
    --expected-initial-search-phase Short --expected-initial-deep-search-triggered 0 \
    --expected-initial-executable-action-count-at-least 1 \
    --stop-after-initial-solver-result-assertion --timeout-seconds 120 --exit-on-complete
}

run_complex_random_perf --scenario-id SEARCH-PERF-COMPLEX-RANDOM-KNIGHTS-CANONICAL-DOP1-2S --character-id IRONCLAD --seed SEARCH_PERF_COMPLEX_RANDOM_KNIGHTS --encounter-id KNIGHTS_ELITE --initial-enemy-current-hps-json '[108,97,89]' --initial-enemy-move-ids-json '["RAM_MOVE","HEX","POWER_SHIELD_MOVE"]' --cards-path coverage/unattended/search-performance-complex-random-knights-cards.json

run_complex_random_perf --scenario-id SEARCH-PERF-COMPLEX-RANDOM-QUEEN-CANONICAL-DOP1-2S --character-id REGENT --seed SEARCH_PERF_COMPLEX_RANDOM_QUEEN --encounter-id QUEEN_BOSS --initial-enemy-current-hps-json '[211,419]' --initial-enemy-move-ids-json '["STRONG_TACKLE_MOVE","PUPPET_STRINGS_MOVE"]' --initial-player-stars 3 --cards-path coverage/unattended/search-performance-complex-random-queen-cards.json --powers-path coverage/unattended/search-performance-complex-random-queen-powers.json

run_complex_random_perf --scenario-id SEARCH-PERF-COMPLEX-RANDOM-TEST-SUBJECT-CANONICAL-DOP1-2S --character-id DEFECT --seed SEARCH_PERF_COMPLEX_RANDOM_TEST_SUBJECT --encounter-id TEST_SUBJECT_BOSS --mark-encounter-as-second-boss-for-test --initial-enemy-current-hps-json '[111]' --initial-enemy-move-ids-json '["BITE_MOVE"]' --cards-path coverage/unattended/search-performance-complex-random-test-subject-cards.json --powers-path coverage/unattended/search-performance-complex-random-test-subject-powers.json

run_complex_random_perf --scenario-id SEARCH-PERF-COMPLEX-RANDOM-AEONGLASS-DOP1-2S --character-id NECROBINDER --seed SEARCH_PERF_COMPLEX_RANDOM_AEONGLASS --encounter-id AEONGLASS_BOSS --enemy-current-hp 526 --initial-enemy-move-ids-json '["EBB_MOVE"]' --cards-path coverage/unattended/search-performance-complex-random-aeonglass-cards.json --powers-path coverage/unattended/search-performance-complex-random-aeonglass-powers.json
```

统一条件为 A10、第三幕（`actIndex=2`）、清空默认牌组与所有玩家牌堆、Low preset、Smart potion、DOP1、强制 `2 s` 短搜、首个 solver result 后停止。结果如下；`choice` 是 choice 分支数，`sold-hp pruned` 是卖血预算剪枝数（四组 `repeatable-no-progress` 均为 `0`）：

| 场景 | runId | 搜索耗时 / 分配 | choice / actions / sold-hp pruned / turns | 结果时内存 |
| --- | --- | --- | --- | --- |
| 三骑士 | `108c2fd35daf46938d66be241d51283a` | `1324.6 ms / 96,126,192 B` | `0 / 4 / 14 / 4` | heap `83,035,136 B`；WS `1,630,224,384 B` |
| 女王 | `f590d200f7e0467aaf090c140d2eced8` | `2143.5 ms / 250,810,968 B` | `1146 / 6 / 270 / 2` | heap `73,031,464 B`；WS `1,781,719,040 B` |
| 实验体 | `17875c14859c4aedb5f44e5f6539b788` | `2062.0 ms / 160,130,880 B` | `0 / 6 / 0 / 5` | heap `82,701,608 B`；WS `1,701,670,912 B` |
| 永世沙漏 | `11d097fe49764027a69c551616ab5416` | `1393.4 ms / 149,711,016 B` | `516 / 4 / 24 / 5` | heap `78,028,328 B`；WS `1,678,315,520 B` |

四组均为自然首回合的确定性合成根，不设置三骑士/女王/实验体/永世沙漏在战斗中后段才出现的私有字段。它们用于比较搜索成本，不宣称覆盖完整 Boss 状态机。女王还有一组同输入并行探索：DOP4 为 `1764.1 ms / 553,203,344 B`，DOP8 为 `1489.3 ms / 560,337,384 B`，两侧工作量同为 `choice=2237 / actions=6 / sold-hp-pruned=234 / turns=6`；DOP8 比 DOP4 快 `15.58%`，但分配增加 `1.29%`。DOP1 因时间预算内到达的工作量不同，不能与 DOP4/8 当作严格吞吐 A/B。

## 战斗结束与跨战斗 GC 压力

原问题日志表明“每场都强制全代回收”本身就是可见卡顿源：有效回收样本把 managed-live 从 `11,815,996,832 → 2,679,998,192 B` 和 `5,781,980,800 → 2,408,884,000 B`，但 GC 停顿分别为 `1455.6/525.4 ms`；另两次 managed-live 从 `2,579,302,440 → 2,606,643,024 B` 和 `2,995,424,344 → 3,052,853,864 B`，没有释放存活量却仍停顿 `350.2/150.2 ms`。因此不以“战斗结束时 working set 必须立即降低”作为正确性目标；CLR 可以保留已空堆段供后续战斗复用。

当前策略：

- 战斗期搜索、根快照和取证捕获分开统计分配压力。低于 `256 MiB` 的战斗结束只退出 No-GC 区域并恢复 latency mode，不强制 Gen2。
- 超过阈值、No-GC 耗尽或区域滚动才请求一次后台、非压缩 Gen2。一个新 LOH 弱引用 sentinel 与 `GCKind.Background/FullBlocking` 完成索引共同证明回收确实发生在引用释放之后，而不是误把已在进行的旧 background GC 当作完成。
- Reset 在建立引用释放/回收门前先登记 early No-GC exit：它等待当前搜索退出，只结束区域并恢复 latency mode，不强制 Gen2。Reset 取消搜索和部署后，后台等待当前与先前已换下的 worker、主线程 callback、部署 operation、已延迟根请求、回合开始 visual-setup/回合准备后部署任务、回合准备/自动预出牌原版任务以及战斗取证 FIFO 释放引用。同时到来的回收请求只 join 同一任务；新战斗的普通搜索和回合准备在门完成后才经主线程 dispatcher 捕获根，worker 在此后才建立新 No-GC 区域，避免新的大根先消耗旧区域。每次 Reset 推进 lifecycle epoch，旧回合准备完成不能把结果、错误或状态偏差写进新会话。游戏主线程不同步等待回收；前置释放任务取消或失败也不会污染后续门。
- 取证 FIFO 完成后清空临时 combat capture/state text/profile 图；内存中检查点上限与导出上限统一为 `6`，开始新战斗时不再同时保留上一场检查点。

早期组件细节门禁 runId `6fdc219535da49c5b6db7179528bf775` 记录低分配 `forced_gen2=false / gen2_delta=0 / 0.0 ms`。最终 policy gate `GC-LIFECYCLE-POLICY-MECHA-010` 以 runId `8441e817afdf402d95567eb4d7d11607` 通过：低分配 active scope 的 early exit 等待旧搜索、阻止新 entrant 和 root-capture barrier 且不强制 Gen2，fault/cancel 的释放任务不会破坏 FIFO。No-GC 区域外的根压力与区域内重分配都构造 `295,200,624 B` 连通对象图；两条路径均只完成一次非压缩 Gen2 且弱引用图死亡，区域内并发请求为 `reclaim=1 / join=1 / Gen2=1`。控制器门禁 `GC-CONTROLLER-RELEASE-MECHA-011` 以 runId `57ec7ee8c2634d95ae0c358a336ae7cf` 验证 A→B→Reset 会等待两个搜索 worker、实际主线程 callback、visual-setup 延迟任务与两个已换下的合成部署 operation；搜索和部署各自均为 `scheduled=2 / completed=2 / CTS disposed=2`，旧任务/旧回合准备 epoch 不写新会话，最终断言后的 `SolverResult` 测试引用会在复用 GC 前清空。正常 `Setup → AutoPrePlay → 熵选牌 → 第 2 回合复用` 由 runId `6f09011103084daaaca36084264eacdb` 通过。root-capture 门的直接 policy 断言已覆盖，但新增的主线程 dispatcher 与原版回合准备取消竞态尚无独立生产竞态夹具。这些是合成 headless 政策门，完整 Mod 组合下的战斗结束帧间隔仍需可见 Steam 会话验收。

## 算法与实现调研结论

当前引擎已经有双 `ulong` 状态键、duplicate pruning、transposition table、Pareto 支配、copy-on-write 和确定性并行 lane。上述极端样本在首个 Beam 节点前或一次卡牌执行中就产生主要成本，因此本轮不直接替换成 A*、IDA*、MCTS 或更宽 Beam；先减少状态表示、Hook dispatch 和根捕获的常数成本，能在不改变路线排序的前提下扩大有效搜索预算。

| 候选方向 | 与本项目的关系 | 结论 |
| --- | --- | --- |
| 根级 signature/choice 元数据驻留、combat preview 复用 | 当前 `action/card_exec` 与 listener 构造占最大分配；同一根的卡牌定义和 choice 元数据高度重复 | 下一批低语义风险优化，先做 profile-guided interning 与根生命周期缓存 |
| typed、保序 Hook plan | 只按 Hook 类型执行稳定有序 listener，减少每次效果扫描；必须保留附魔、苦难、BaseLib 动态 modifier 的顺序和失效语义 | 先加差分门禁，再按 Hook 类型逐个启用 |
| `CardHandle + CardState` 与分页 COW/持久化牌堆 | [RRB tree](https://infoscience.epfl.ch/record/169879/files/RMTrees.pdf) 和 [Clojure PersistentVector](https://github.com/clojure/clojure/blob/master/src/jvm/clojure/lang/PersistentVector.java) 展示了结构共享的成熟实现；适合高 fork、低修改率 | 潜在收益最大，但需先移除可变 preview、OwnerPile、observer 和外部引用对对象身份的依赖，单独实施 |
| 增量 Zobrist/Merkle 指纹 | [Zobrist hashing 原始报告](https://research.cs.wisc.edu/techreports/1970/TR88.pdf) 与 [Stockfish Position](https://github.com/official-stockfish/Stockfish/blob/master/src/position.cpp) 展示按状态增量更新键；当前牌堆 fingerprint miss 仍会扫描数千张牌 | 作为分页状态之后的第二阶段；保留双键和必要的结构校验，避免随机/choice 状态漏键 |
| 扁平 TT、slab/arena、数组池 | [Stockfish TT](https://github.com/official-stockfish/Stockfish/blob/master/src/tt.cpp) 是紧凑、代际替换的参考；.NET 可用 [`ArrayPool<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1) 降低短命数组分配，并需遵守 [LOH](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/large-object-heap) 行为 | 适合 fingerprint/choice 数组与节点元数据；不得池化仍被 continuation 或 UI 持有的对象 |
| 按 bytes/transition 自适应 DOP | 女王 DOP4→8 同工作量提速 `15.58%`，但分配增加；固定最大并发会在超大牌堆上放大 working set | 根据近期每转移分配、GC 余量和剩余预算限制 lane，而不是只按 CPU 核数 |
| Gumbel/正则化/occupancy MCTS | [Gumbel AlphaZero](https://openreview.net/forum?id=bERaNdoegnO)、[regularized MCTS](https://proceedings.mlr.press/v119/grill20a.html) 和 [Volume-MCTS](https://proceedings.mlr.press/v235/schramm24a.html) 分别改善少模拟政策更新、探索正则化和长时域覆盖 | 都依赖可校准的 policy/value 或可采样目标，会改变当前确定性 Beam 的质量语义；先作离线对照，不作为本轮内存/常数优化 |
| Beam-stack、SMA*、IDA*、hash-distributed search | [Beam-stack search](https://cdn.aaai.org/ICAPS/2005/ICAPS05-010.pdf)、[SMA*](https://cse.sc.edu/~mgv/csce580f11/gradPres/Russell_ecai92-sma.pdf)、[IDA*](https://www.sciencedirect.com/science/article/abs/pii/0004370285900840) 和 [HDA*](https://metahack.org/Kishimoto-Fukunaga-Botea-icaps2009.pdf) 可分别约束内存或扩展并行 | 这些算法要求可比较的启发式/目标和稳定状态划分；本项目还有随机分支、部分未镜像效果与多目标评分，只有在状态成本先下降、质量基准建立后再做独立实验 |

每项候选都必须在相同 work counters、路线、评分和终止边界下做 A/B；时间预算内工作量不同的结果只报告吞吐探索，不宣称严格加速。

## Silent 大牌组 Fork/选牌压力

- fixture：`coverage/unattended/search-performance-silent-large-deck-cards.json`
- 输入：`44` 条聚合记录，共 `396` 张牌（手牌 `7`、抽牌堆 `389`）。
- 白名单：仅 `cardId / pile / count / upgradeLevels / treatAsDeckCard`。
- 排除：存档、RNG、遗物、药水、Power、日志、路径、平台和环境信息。

```bash
./tools/run-unattended-test.sh \
  --scenario-id SEARCH-PERF-SILENT-LARGE-DECK-5S \
  --character-id SILENT \
  --seed SEARCH_PERF_SILENT_LARGE_DECK \
  --encounter-id AEONGLASS_BOSS \
  --ascension 5 \
  --act-index-for-test 2 \
  --enemy-current-hp 512 \
  --initial-enemy-move-ids-json '["EBB_MOVE"]' \
  --initial-player-hp 65 \
  --initial-player-max-hp 65 \
  --initial-player-energy 3 \
  --clear-player-piles \
  --cards-path coverage/unattended/search-performance-silent-large-deck-cards.json \
  --performance-preset-for-test VeryHigh \
  --potion-policy-for-test Smart \
  --search-max-degree-of-parallelism-for-test 8 \
  --force-short-search-only \
  --short-search-budget-override-milliseconds 5000 \
  --measure-search-phases \
  --expected-initial-search-phase Short \
  --expected-initial-deep-search-triggered 0 \
  --expected-initial-executable-action-count-at-least 1 \
  --stop-after-initial-solver-result-assertion \
  --timeout-seconds 120 \
  --exit-on-complete
```

Runner 同时断言 VeryHigh 的 Beam `36/90`、节点 `10000/50000`、出牌分支 `30/48` 和 No-GC `16,000,000,000 B` 均保持原设置。

## Necrobinder 药水/高分支压力

- fixture：`coverage/unattended/search-performance-necrobinder-potion-heavy-run-snapshot.json`
- 输入：`38` 张战前牌、`20` 件遗物、`2` 瓶药和生命/药水槽；战斗初始化后形成 `41` 张搜索根。
- 白名单：顶层仅 `players`；玩家只保留 runner 支持的 `deck / relics / potions / max_potion_slot_count / max_hp / current_hp` 及合法子字段。RNG 由下方公开合成 seed 生成。
- 排除：地图、房间历史、时间、平台、账户、日志、路径和环境信息。
- 边界：这是战前白名单投影，不能作为原生战斗逐动作 replay。

```bash
./tools/run-unattended-test.sh \
  --scenario-id SEARCH-PERF-NECROBINDER-POTION-QUICK \
  --character-id NECROBINDER \
  --seed SEARCH_PERF_NECROBINDER_POTION \
  --run-snapshot-path coverage/unattended/search-performance-necrobinder-potion-heavy-run-snapshot.json \
  --encounter-id AEONGLASS_BOSS \
  --ascension 10 \
  --act-index-for-test 2 \
  --enemy-current-hp 526 \
  --initial-player-hp 41 \
  --cards-json '[]' \
  --search-max-degree-of-parallelism-for-test 8 \
  --performance-preset-for-test VeryHigh \
  --potion-policy-for-test RequireAtLeastOne \
  --force-short-search-only \
  --short-search-budget-override-milliseconds 5000 \
  --measure-search-phases \
  --expected-initial-search-phase Short \
  --expected-initial-deep-search-triggered 0 \
  --expected-initial-executable-action-count-at-least 1 \
  --stop-after-initial-solver-result-assertion \
  --timeout-seconds 120 \
  --exit-on-complete
```

Runner 同时断言 VeryHigh 原 Beam、节点、分支、药水策略与精确 `16 GB` No-GC 契约，并要求返回可执行且使用药水的候选。

Windows 与 Linux 的单行等价命令登记在 `docs/TEST_MATRIX.md`。

## 性能解释边界

- v0.21.3 固定 `576` 展开的历史 A/B 中，两侧均为 `3463` 次转移、`1124` 个选牌分支，并返回相同 3 回合、预计掉血 `2` 的路线。上游为 `5116.3 ms / 1,039,502,640 B`，优化版本为 `3985.0 ms / 528,876,328 B`：累计分配降低 `49.12%`，单次 headless 墙钟缩短 `22.11%`。
- 精确 `16 GB` No-GC 的完整长搜保持 `7018/52644/23196` 工作量、评分、10 回合胜利、玩家/敌人 HP `9/0`、预计战损 `56`、卖血 `11` 与两瓶药使用结果不变；连续三个优化版本约为 `52.6–53.6 s / 11.646–11.655 GB`，差异属于单次 headless 波动。
- 以上结果没有改变 Beam、节点、时间、分支、评分、RNG、候选顺序或药水政策，也不构成可见 Steam 帧率结论。
