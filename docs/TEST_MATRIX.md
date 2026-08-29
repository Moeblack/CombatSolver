# CombatSolver 测试清单

> 基线：CombatSolver `0.17.0`（2026-08-29）、塔 2 `0.111.0`、RitsuLib 实测 `0.5.17`（清单最低 `0.5.13`）、CombatSolver 内置战斗模拟引擎。无人测试运行隔离的原版 `--headless` 游戏进程，不使用自建 STS CLI；性能最终门槛另由 Steam 可见会话验证。完整战斗基准使用 `Instant` 出牌与 `0` 额外停顿。

同一版 DLL 的场景默认复用 marker 记录的 headless 游戏进程：首条命令直接启动游戏，完成后返回主菜单等待下一条请求；后续命令只向该测试 PID 投递。测试使用独立 `APPDATA/LOCALAPPDATA`、关闭 Steam，并只在隔离设置中确认允许加载 Mod；RitsuLib 在 headless 生命周期内从创意工坊版本目录临时投影到带所有权标记的本地目录，退出时删除。发现未由 marker 管理的塔 2 进程时拒绝运行。只有重新编译需要加载新 DLL，或最后一批显式传入 `-ExitOnComplete` 时才退出游戏。同一战斗能容纳的行动继续合并到一个批次夹具中连续执行。

## 0.17.0

本节只登记本批次实际运行的验证。需求原文和计划不作为测试通过证据。

| 场景 | 结果 | 验证内容 | 日期 |
| --- | --- | --- | --- |
| `OPENING-STRENGTH/DEXTERITY-POTION-0170` | 通过 | 力量药与敏捷药均为最终路线首个动作、位于首张牌之前；runId `a65e0ce7c1e1478c949052d86ed799a7`、`c6217b9d975e4ecbaf0e26a4a3dd7a5d` | 2026-08-29 |
| `LIZARD-TAIL-LIVE-REUSE-0170` | 通过 | 1 HP 触发蜥蜴尾巴后，首轮与第 2 回合复用均保留整场战损 1；路线有“蜥蜴尾巴：复活”，计划外重算 0。runId `735a14adb16241719f220badb89f00a9` | 2026-08-29 |
| `BRIGHTEST-FLAME-TERMINAL/NECESSARY-0170` | 通过 | 同样无伤可胜时不打至亮之焰；必须用它完成当回合击杀时，路线保留 78 最大生命与 2 点当前损失。runId `c6b2b17afea34e9d8befaf3f32401f36`、`207af957f0664c478201bcd4c49bffd2` | 2026-08-29 |
| `BATTLEWORN-DUMMY-V1/V2/V3-KILL-0170` | 通过 | 三档训练假人均以自伤攻击完成击杀，不用安全停滞替代目标；runId `319a22b784414d4d8f75559ecaa21779`、`9f7666436f4c4451907971ae732211b1`、`b61f285e96ce40e2bef847e1c708250c` | 2026-08-29 |
| `BATTLEWORN-DUMMY-EVENT-DEFEAT-0170` | 通过 | 倒计时耗尽返回 `EventDefeat`，不授予胜利。runId `963ddd67b7db402da7a46f17a73cd7a3` | 2026-08-29 |
| `TWO-CARD-INFINITE-DEPLOY-0170` | 通过 | 亮剑/亮技双卡无限执行 19 个动作、18 次洗牌，当回合零战损击杀；完整自动执行计划外重算 0。runId `54b78ec8e2ef4baf80a452ff0744a81f` | 2026-08-29 |
| `ANGER-COMPACT-ALTERNATIVE/REQUIRED-0170` | 通过 | 等价击杀选择切割且不打愤怒；只有愤怒可击杀时仍使用。runId `03bfc9e0b0d44389aaba29c74f6a99fa`、`64a3b6d6d4304bdd9c6db48386983122` | 2026-08-29 |
| `AEONGLASS-ANGER-MIDCOMBAT-0170` | 通过（近似重建） | 按问题包第 9 回合手牌、生命、格挡、Power 和行动历史近似重建，路线不再加入愤怒。该夹具仍只找到死亡路线，省略完整消耗堆与部分历史，不作为原包战损复放。runId `58125330c52a4552b196021df614298e` | 2026-08-29 |
| `BECKON-CROSS-TURN-DEPLOY-0170` | 通过 | 首动打出呼唤，预计整场战损 4，第 2 回合自动击杀，计划外重算 0。runId `41198704657b42d284ff24113dbc429b` | 2026-08-29 |
| `GENETIC-ALGORITHM-REPLAY / GOOPY / SCYTHE-0170` | 通过 | 遗传算法华彩重放累计成长 6，并在第 2 回合继续执行、计划外重算 0；黏糊防御成长 1；巨镰成长 5，三者均在同战损胜利路线中主动培养。runId `6097ecc0ab3142a0a6c0ee187c1eda54`、`4b36668f89c449ca8eeb5ea6e6e1d2e4`、`4420c21826404907b33a1e9543949cdc` | 2026-08-29 |
| `NIGHTMARE-CLONE-GROWTH-BOUNDARY / SOULS-POWER-GROWTH-0170` | 通过 | 梦魇 `Clone` 不带 `DeckVersion`，因此不虚构跑局成长；灵魂之力跨回合培养至少 6。边界验证 runId `6e616ddf5c9b45b9a7c20434a8f912c1`，灵魂之力 runId `4e265950217046f886890458d2728220`；错误保留跑局版本会在第 2 回合产生状态差异，失败证据 runId `6112275ee763406abe03f23dfdc5238c` | 2026-08-29 |
| `FEED / THE-HUNT / HAND-OF-GREED-FATAL-0170` | 通过 | 三类斩杀分别获取最大生命、卡牌奖励和金币，且优先于普通等价击杀。runId `1e25b558793e445cbfa2394b23e2ef7a`、`41205c552552424197c3dde4827fa0f7`、`716c7e94d0c44b659672a8954b47de20` | 2026-08-29 |
| `NOT-YET / ROYALTIES / FORBIDDEN-GRIMOIRE / ALCHEMIZE-0170` | 通过 | 同战损胜利中依次保留治疗、金币奖励、移除奖励和生成药水；runId `72e453a92041468e8b041df280512ecb`、`3d1538b146e249b6b9debbb1a84ee54c`、`62943077e9a241ff90097518571d6dfc`、`532ddda82beb48fe815a0f66d8528d06` | 2026-08-29 |

## 0.16.0

| 场景 | 结果 | 验证内容 | 日期 |
| --- | --- | --- | --- |
| `NIBBITS-DUPLICATE-TORIC-0160` | 通过 | 从啃咬兽问题包战前状态直接注入两份坚韧之环。正式搜索跨 8 回合返回，runId `254746adc3a54c52ad894279e310d1e6`；严格差分验证两份实例以 `Block=5/8` 分别触发，合计获得 13 格挡并剩余总层数 1，runId `1eedfd2234a847378a0e79c600fb1012`；问题包世界线完整全自动在第 8 回合结束、计划外重算 0，runId `bcd844191a81453d8d21702024aa0fb9` | 2026-08-29 |
| `KNIGHTS-RELIC-ANNOTATION-REGRESSION-0160` | 通过 | 从三骑士问题包战前存档恢复种子、A10、`108/97/89` HP 与首行动；最终遗物标注的完整路线回放正常完成，返回 3 回合候选，不再出现两张 `BOOST_AWAY` 升级/保留状态交换。当前路线与旧包不同，不记作旧路线逐动作回放。runId `dfdb18f2036e42ecb6beda299d808028` | 2026-08-29 |
| `CHOICES-PARADOX-SCROLLS-0160` | 通过 | 使用咬人卷轴问题包的战前存档、种子、A10、四敌生命和行动重建首回合；验证选择悖论原生页面先显示、搜索后启动、Mod 自动选牌，路线第一组胶囊以“选择悖论：选择 ”开头。短搜 `5891.5 ms`，比较 `5883` 个选牌分支，runId `b9fa371a5b29479bb97c19da7980526f`；最小五候选夹具 runId `220482eb7fbc4f459c6f970748b0e033` 同样通过 | 2026-08-28 |
| `RINGING-HAVOC-AUTOPLAY-0160-FINAL` | 通过 | 仪式兽施加昏眩后，破灭作为本回合第一张牌正常结算；其翻出的重振被原版 `CardPlaysStarted` 规则阻止，不获得格挡、不消耗手牌防御，重振按破灭规则进入消耗堆。原生与预测完整状态一致。runId `bbc71ec201d34e16a114e2a1769ceb52`；修复前基线 runId `c24277b02b414dc08fdcb59fc7cec21e` 为模拟 5 格挡、实机 0 格挡 | 2026-08-28 |
| `MONSTER-MOVES-BATCH-029-RINGING-0160-FINAL` | 通过 | 既有昏眩相邻回归升级为当前逐实例状态键后，两次 `BEAST_CRY_MOVE` 严格差分通过：第一张牌可打、后续带昏眩的牌不可打，玩家回合末 Power 与全部昏眩状态清除。runId `0b73e8ca09374b0dbb27e41f6f021ec9` | 2026-08-28 |
| `HEADBUTT-EMPTY-DISCARD-0160` | 通过 | 清空全部牌堆后实际打出头槌；弃牌堆为空产生的 `0` 选项原生牌堆请求按空选择完成，第 2 回合精确复用，计划外重算 0。runId `53d9a793c14040d790183727ab0a88cd` | 2026-08-28 |
| `COSMIC-INDIFFERENCE-EMPTY-DISCARD-0160` | 通过 | 清空全部牌堆后实际打出宇宙冷漠；空弃牌堆选择不再中止部署，第 2 回合精确复用，计划外重算 0。runId `b09e6ef038604469b73680803c6916e7` | 2026-08-28 |
| `TORIC-TOUGHNESS-FRAIL-BLOCK-0160` | 通过 | 虚弱 1 层下打出坚韧之环，角色实际获得 3 格挡，但 Power 内部精确保存 `Block=3.75`；原生与预测完整状态一致。runId `e0e74f8d044e4026b9484ae78d03a622` | 2026-08-28 |
| `JAXFRUIT-TORIC-TOUGHNESS-REUSE-0160` | 通过 | 从啪嗒果问题包战前存档恢复种子、A10、双敌生命、首行动与 RNG；第 4 回合精确复用，计划外重算 0。runId `ed4f9715bde9405bab9655fd83701aba` | 2026-08-28 |
| `PAINFUL-STABS-MONSTER-ATTACK-0160` | 通过 | 给酸液攻击怪物注入荆棘，单次穿透格挡的命中后弃牌堆精确加入 1 张伤口；原生与预测完整状态一致。runId `b89e025cf429450595e4d38f2e603c90` | 2026-08-28 |
| `POWER-DAMAGE-HOOKS-REGRESSION-0160` | 通过 | 14 组伤害与攻击钩子严格差分全部通过，覆盖荆棘、吸取、活力、缓冲等，确认怪物攻击接入共享 `AfterAttack` 后没有重复结算。runId `50849d4ecff04661a7254b529611c74e` | 2026-08-28 |
| `TEST-SUBJECT-PAINFUL-STABS-REUSE-0160` | 通过 | 从试验体问题包战前存档恢复种子、A10、牌组、遗物、首行动与 RNG；越过第二形态多爪与荆棘，至第 6 回合持续精确复用，计划外重算 0。runId `a267600d977549f1a492d36479394f60` | 2026-08-28 |
| `VANTOM-UPGRADED-CARD-SHUFFLE-0160` | 通过 | 从 Vantom 问题包战前存档恢复种子、A2、牌组、首行动与 RNG；普通/升级打击跨洗牌顺序一致，第 5 回合精确复用，计划外重算 0。runId `e877c8239def4647a36c7d5102c940f3` | 2026-08-28 |
| `INSATIABLE-INVOKE-CROSS-CHARACTER-0160` | 通过 | 静默猎手打出召唤后推进到第 2 回合；原生与预测均创建 `2/2` 奥斯提并施加 1 层“为你而死”，两项下回合 Power 被消费，额外能量与 5 张手牌严格一致。runId `ec2f0a77e09a424fad6b8f78f2460c7e`；既有亡灵契约师奥斯提卡牌与伤害转移回归 runId `037a7a3ec6bc48f797913d398f0dfde1`、`c09e18e9f28b47e1a5c528495c62c124` 同时通过 | 2026-08-28 |
| `INSATIABLE-INVOKE-SEARCH-0160` | 通过 | 无厌沙虫固定为液化地面，静默猎手只有召唤与 5 张防御；正式 Short 搜索越过原 `EndTurn → SUMMON_NEXT_TURN_POWER` 初始化错误，正常返回 5 回合候选、1 个可执行动作、未镜像项 0。runId `0765ed5133604dcb9fab017fa8e30f42` | 2026-08-28 |
| `PALE-BLUE-DOT-FIFTH-CARD-DRAW-0160` | 通过 | 注入 2 层暗淡蓝点后恰好打出 5 张牌并进入下一回合；原生与预测都在第五张触发，下回合均抽基础 5 张加额外 2 张，瞬时抽牌 Power 均已消费。runId `cc6470a2161c417bbf64e5a672a67367` | 2026-08-28 |
| `TRIGGERED-SHUFFLE-CHOICE-ORDER-0160` | 通过 | 两个严格差分场景分别用早有准备和升级杂技触发空抽牌堆洗牌；战略选择先从洗牌后的抽牌堆拿走打击，随后卡牌自身选择把同一张打击置顶或弃掉，原生/模拟完整状态一致。runId `8da0adbda1484b8f8131cadd30e60d2c` | 2026-08-28 |
| `DECIMILLIPEDE-TRIGGERED-CHOICE-REPLAY-0160` | 通过 | 从千足虫问题包战前存档、种子、三段生命和三个首行动重建初始搜索，正常返回候选且没有再次出现第 17 回合早有准备双 pending 异常。当前路线与包内旧失败分支不同，不记逐动作回放。runId `967571405a5c4a78851c5310fcdd303a` | 2026-08-28 |
| `AEONGLASS-TRIGGERED-CHOICE-REPLAY-0160` | 通过 | 从永世沙漏问题包战前存档、种子、A10 和 `EBB_MOVE` 重建强制短搜，越过原第 6 回合杂技双 pending 边界并返回候选。当前路线与包内旧失败分支不同，不记逐动作回放。runId `d85101a57dcc4d4d910a2e159e02e6c0` | 2026-08-28 |
| `KNOWLEDGE-DEMON-GLAM-POCKETWATCH-0160` | 通过 | 注入怀表和带华彩的升级后空翻，后空翻以一个路线动作完成两次 CardPlay；推进到第 2 回合后原生/模拟牌堆、抽牌及怀表私有计数严格一致，均为 `POCKETWATCH/0/2`。runId `c82c4fcbadda41dc96df6d65cf0e0d63`；问题包 Custom/Low 战前跑局均未在夹具上限内完成首搜，不记通过 | 2026-08-28 |
| `CARD-UPGRADE-STABLE-SHUFFLE-0160` | 通过 | 武装只升级两张同名防御中的一张，两张牌以升级/普通顺序进入弃牌堆后触发洗牌；修复前第 2 回合严格差分稳定得到普通/升级防御错位，runId `1718b01532d94f34b65acc79a246482a`；改为按分支当前预览排序后原生/模拟完整状态一致，runId `854065893bc742c5ac04e3d6f59e8cdf` | 2026-08-28 |
| `CHOMPERS-UPGRADED-CARD-SHUFFLE-0160` | 通过 | 从啃咬者问题包战前存档重建，完整自动战斗在第 5 回合结束；武装升级后的同名牌跨洗牌顺序与实机一致，计划外重算 0。runId `ddebe062128845f9a3f73fbb6992e3ff` | 2026-08-28 |
| `CHOMPERS-UPGRADED-CARD-SHUFFLE-INCREMENTAL-0160` | 通过 | 同一问题包状态强制短搜并启用增量/完整前缀核对，覆盖 12 回合、3 次洗牌，未镜像项 0，前缀回放一致。runId `0c32515cb2944570a6a748febf928737` | 2026-08-28 |
| `STRATAGEM-PREPARED-CHOICE-ORDER-FINAL-0160` | 通过 | 升级准备充足在空抽牌堆时触发洗牌，战略选择先从三张抽牌堆选一张，随后准备充足抽两张、弃两张并留下打击完成 1 HP 斩杀；增量/完整回放一致，真实原生页面按两次选择顺序完成，计划外重算 0。runId `d305379b208841b68e25f8987e2e1967` | 2026-08-28 |
| `TEST-SUBJECT-PREPARED-CHOICE-SHORT-0160` | 通过 | 从问题包搜索请求检查点固化 5 张手牌、27 张有序抽牌、玩家状态及 `BITE_MOVE` 状态日志；强制短搜越过原准备充足双 pending 失败点，返回 7 回合候选，未镜像项 0。runId `d4903310604044ae8fa0c689a82f8b8d`；整包增量与普通深搜均在 180 秒达到夹具上限，不记通过 | 2026-08-28 |
| `CROSS-TURN-NO-PROGRESS-0150` | 通过 | 仅有一张防御、100 敏捷且完全没有伤害手段；修复前耗满短搜约 22 秒并搜索 54 回合，修复后搜索本体 175.3 ms 结束、剪掉 18 条跨回合无进展分支。runId `5e3fa09b18094a77a07492098e204785`，修复前 runId `ddcb886becc84a28aa8b56dbb067bea9` | 2026-08-28 |
| `BOWLBUGS-CROSS-TURN-NO-PROGRESS-0150` | 通过 | 从问题包战前存档、种子、敌人生命与首轮意图近似重建，仍找到第 6 回合胜利、预计战损 3、零药水；当前没有原生战斗状态导入器，不记作问题包逐动作回放。runId `33c814b90b6b4bdda47fe5b9c98961f9` | 2026-08-28 |
| `SURVIVOR-REPLAY-EMPTY-CHOICE-0150` | 通过 | 爆发与复制使升级生存者执行三次；前两次实际弃完两张牌，第三次原版 `options=0 / select=0..0` 请求按无操作完成，不消费虚构计划。首回合结束、计划外重算 0，runId `207cdd4927f74188948ec903574a3c7c`；修复前 runId `026a459931b24b58be85d644d3778d25` 在同一请求报错 | 2026-08-28 |
| `NATIVE-EMPTY-PLAN-ADJACENT-0150` | 通过 | 复制拾荒在空手时发出两次原生空请求；搜索生成的两条显式空计划逐条核销，首回合结束、计划外重算 0。runId `c73b5302e55e4b06bf56dc35169f3e20` | 2026-08-28 |
| `POCKETWATCH-REPLAY-REUSE-0150` | 通过 | 手牌中的螺旋打击实际结算两次 `CardPlay`，路线仍保持一个出牌动作；怀表逐次计数后第 2 回合命中精确复用，增量分叉与完整前缀回放一致，计划外重算为 0。runId `93d934679e8746de95bebd9dd5ce58e2`；修复前基线 runId `89792141b109409aa2aa5adcc7d2a846` 稳定得到 `expected=1 / actual=2` | 2026-08-28 |
| `POCKETWATCH-REPLAY-FULL-COMBAT-0150` | 通过 | 手牌为螺旋打击、抽牌堆为普通打击，敌人 13 HP；完整自动部署在第 2 回合结束，增量分叉与完整前缀回放一致，计划外重算为 0。runId `106f0b2966dd4225ac9ce1213e123712` | 2026-08-28 |
| `INCOMPATIBLE-GAMEPLAY-MOD-MESSAGE-0150` | 通过 | 预测失败边界断言验证未知第三方玩法 Mod 的玩家提示包含 Mod 名称、标识和卸载建议，不暴露内部订阅器类型；详细异常仍保留 Mod 与订阅器上下文。runId `3b920fd04bb64fdeba536ee825219ea4` | 2026-08-28 |
| `BATTLEWORN-DUMMY-TIMEOUT-BOUNDARY-0150` | 通过 | 第二档假人 150 HP、时间限制 1 层；正式后台搜索在原生逃跑前返回 `EventDefeat`，不移除假人、不授予胜利。runId `1b1d321d7ac941adb5d515efa861d6ee` | 2026-08-28 |
| `BATTLEWORN-DUMMY-V2-EXACT-FINAL-0150` | 通过 | 从第二档训练假人问题包的战前存档、固定牌序和 150 HP 重建，开启增量分叉/完整前缀回放核对并完整自动执行。未击杀分支正确为 `won=False / EventDefeat`，击杀路线为 `won=True / None`；第 2、3 回合精确复用，计划外重算 0。runId `7fbd338febef40668a4980555cc51971` | 2026-08-28 |
| `FAIRY-AUTOMATIC-RESCUE-FINAL2-0150` | 通过 | 1 HP 铁甲战士持瓶中仙女，手牌/抽牌堆各一张重锤；求解器不再判定仅有死亡路线，第 1 回合精灵药自动复活，第 2 回合击杀。首轮路线记录 1 瓶药，实机消耗 `FAIRY_IN_A_BOTTLE`，增量/完整回放一致，计划外重算 0。runId `bbddfcc1e1e54be2a4405e58cd7f557e` | 2026-08-28 |
| `FAIRY-DEATH-LIFECYCLE-FINAL2-0150` | 通过 | 瓶中仙女的自动防死、消耗槽位和 30% 回复与原版完整状态严格一致，已消耗实例不会再次进入死亡监听。runId `e601bec430ea49318ef57a550d8284f8` | 2026-08-28 |
| `ONLY-DEATH-NO-FAIRY-REGRESSION-0150` | 通过 | 相同 1 HP 与酸液攻击下不注入精灵药，首轮仍正确报告仅死亡路线并在第 1 回合死亡，用药数 0。runId `53ad9b78496646b196aa4844794766ef` | 2026-08-28 |

## 0.15.0

| 场景 | 结果 | 验证内容 | 日期 |
| --- | --- | --- | --- |
| `VAMBRACE-STABLE-FORK-FINAL-0150` | 通过 | 原版臂铠已经获得本场首次格挡后仍保留触发卡引用；修复后状态可 Fork，触发卡身份和 `BlockGainedThisCombat=true` 均保持。runId `31793a0e83df4656aa0ea3b9182c4c29`；修复前基线 runId `0e1c56fe922c4b4a87109dbd1c06acd0` 稳定抛出问题包同款异常 | 2026-08-28 |
| `TUNNELER-IMBUED-GLACIER-VAMBRACE-0150` | 通过 | 缺陷机器人持臂铠，注能冰川开局自动打出并进入弃牌堆，原版得到 12 格挡；首轮搜索正常返回，未镜像项为 0。runId `a9ebb7b36f554948a8de0f658385aa34` | 2026-08-28 |
| `TUNNELER-IMBUED-GLACIER-VAMBRACE-INCREMENTAL-0150` | 通过 | 同一组合启用增量搜索核对，增量分叉与完整前缀回放一致；开局 12 格挡、未镜像项 0，搜索正常返回。runId `689793cd6343465393fdd567a4a7c41e` | 2026-08-28 |
| `RELIC-CARD-HOOKS-AUDIT-PART-2-VAMBRACE-FINAL-0150` | 通过 | 臂铠连续打出两张防御，第一张 5 格挡翻倍为 10，第二张按普通值获得 5，最终严格为 15；同批遗物 Hook 11/11 通过。runId `eddeca24b4544e918299e4b4bb2a401b` | 2026-08-28 |
| `AXEBOT-THORNS-MULTIHIT-FINAL-0150` | 通过 | 巨斧机器人以 `2 HP`、`2` 层库存执行两连击；玩家持有 `3` 点荆棘和 `12` 格挡。修复前模拟继续执行第二段并产生 `8` 点虚构战损；修复后第一段反伤致死即中止剩余攻击，玩家保持 `75 HP / 2` 格挡，库存重生后的完整实机/模拟状态一致。相邻上勾锤击同时核对攻击者死亡后仍结算虚弱/脆弱。runId `fac22ea0270a4996afa276df384ba370`，基线 runId `fc33aad095054c37a3de730d89472d2a` | 2026-08-28 |
| `AXEBOTS-BUNDLE-FULL-AUTO-FINAL2-0150` | 通过 | 从问题包战前存档重建，以 Low、Instant/0 秒完整自动结束于第 11 回合，`UnexpectedReplans:0`。当前路线与原包不同，不记作逐动作回放。runId `fca4e9e3a09d4ab490271ec6d38ad10a` | 2026-08-28 |
| `AXEBOTS-BUNDLE-INCREMENTAL-FINAL2-0150` | 通过 | 同一问题包状态以 Low/Short 完成增量分叉与完整前缀回放一致性，覆盖 11 回合、3 次洗牌，未镜像效果为 0。runId `c8c8f3675a934f028704ab82e2f7dd4d` | 2026-08-28 |
| `FTL-CROSS-TURN-STATE-0150` | 通过 | 修复前跨回合严格差分稳定复现第 3 张 FTL 少抽一张；最终分支状态实现下，第 3 张抽牌、第 4 张不抽均与实机一致。runId `add8e54810d54f41b5cb6b55dc410892`，基线 runId `6c069ce0891a484091481bd8b3387e35` | 2026-08-28 |
| `CURRENT-TURN-CARD-HISTORY-ADJACENT-FINAL-0150` | 通过 | Fetch 在下一回合经全息影像取回同一实例后重新允许抽牌；Make It So 在本回合第 3 张技能后返回手牌，实机/模拟严格一致。runId `f478c96ec3144c81847c3b225f95866e` | 2026-08-28 |
| `BYRDONIS-BUNDLE-REUSE-FINAL-0150` | 通过 | 从多尼斯异鸟问题包的战前跑局状态重建；第 3 回合精确复用，计划外重算 0，增量分叉与完整前缀回放一致。首抽路线与原包不同，因此不记作逐动作回放。runId `a0c1808cfae745188ae5a3f8d1f28270` | 2026-08-28 |
| `SLITHERING-STRANGLER-BUNDLE-REUSE-FINAL-0150` | 通过 | 从蛇行扼杀者问题包的战前跑局状态重建；越过原第 4 回合重算点并精确复用，计划外重算 0。首抽路线与原包不同，因此不记作逐动作回放。runId `a6b72e6f81f24aa096833902d6860046` | 2026-08-28 |
| `CUBEX-ROOT-CAPTURE-150` | 通过 | 修复前同场景稳定复现不存在的 `CubexConstruct.ChargeUpStrengthGain`；移除多余捕获后根快照成功物化。runId `0c39d6aa84904c5b994bf8f985bfd316`，基线 runId `f96241297c7a443a8a1fe50d0a7b5414` | 2026-08-28 |
| `CUBEX-SEARCH-INITIALIZATION-150` | 通过 | 方柱构装体正常首轮搜索覆盖 4 回合，返回 3 个可执行动作，未镜像效果为 0。runId `2f37d97d045743aea8d68ebf99db0e57` | 2026-08-28 |
| `MONSTER-MOVES-BATCH-020-CUBEX-150` | 通过 | 既有 13 项实机/模拟差分全部通过；方柱构装体排出、蓄能和两次重复轰击分别验证多段伤害及力量 `2 → 4 → 6` 累计。runId `1d382781dfc2402581af0383e093b5ea` | 2026-08-28 |
| `TOASTY-MITTENS-BUNDLE-FINAL-0150` | 通过 | 从异螨问题包的战前跑局状态重建首回合烘焙手套；原生手牌页按 `Visible → SearchStarted → Selected` 由 Mod 自动接管，搜索返回 1 个 `TOASTY_MITTENS` 选择并严格进入 Play 状态。runId `097e957b46b941e1b4eb0165862d5493` | 2026-08-28 |
| `KNOWLEDGE-DEMON-NATIVE-CHOICE-0150` | 通过 | 知识恶魔首轮路线计划 `MIND_ROT`；提交结束回合后原生 `ChooseCard` 页面 `visible=1 / selected=1 / search=0`，玩家获得 `MIND_ROT_POWER`，计划外重算 0，增量/完整回放一致。runId `5b5d61d595c249c0a4861151460cc490` | 2026-08-28 |
| `KNOWLEDGE-DEMON-NATIVE-CHOICE-REUSE-0150` | 通过 | 同一路线完成敌方回合二选一后，第 2 回合直接复用；知识恶魔选择没有被错误留给下一回合准备器，计划外重算 0。runId `27a42f3669fb479fafde8e10e3d499f3` | 2026-08-28 |
| `TOASTY-KNOWLEDGE-CROSS-PHASE-0150` | 通过 | 知识恶魔战同时持有烘焙手套；首回合手套保持 `Visible → SearchStarted → Selected`，结束回合后自动完成 `MIND_ROT` 二选一，第 2 回合只重放准备选择并精确复用，计划外重算 0。runId `39de2fb177ce43db95c1c2209c390330` | 2026-08-28 |
| `BURNING-PACT-AUTO-COMPLETE-0150` | 通过 | 固定手牌为燃烧契约+、升格者之灾、防御，抽牌堆为打击；Normal 部署先显示原生手牌页并选择升格者之灾，再打出抽到的打击结束战斗。请求记录 `manual_confirmation=False`，页面 `visible=1 / selected=1 / search=0`，增量/完整回放一致。runId `660b6ba4b2a044938d3960208639b5ef` | 2026-08-28 |
| `ARMAMENTS-AUTO-COMPLETE-ADJACENT-0150` | 通过 | 未升级武装从打击、防御中选择升级目标，原生手牌升级页完成后继续打出升级打击；页面 `visible=1 / selected=1 / search=0`。runId `793fd329c05340d98a775b173dd3b8c9` | 2026-08-28 |
| `CHOMPERS-BURNING-PACT-BUNDLE-FIXED-0150` | 本问题路径通过，整战断言失败 | 从问题包战前跑局状态重建同族小队，燃烧契约原生手牌页完成且未出现确认按钮异常，战斗第 5 回合结束；第 4 回合另有防御升级状态不一致并触发 1 次计划外重算，故不记为整场通过。runId `99127886a8c54dfe8941239186b5ddea` | 2026-08-28 |
| `TOADPOLES-WEAK-20260828-BUNDLE` | 根因确认，待 macOS 实机复测 | `0.14.11`、macOS ARM64 的两次搜索均在 `GC.TryStartNoGCRegion(6 GB, 1 GB)` 抛出 `ArgumentOutOfRangeException(totalSize)`；根快照已成功，尚未进入 Beam。当前代码只把该精确异常分类为 CLR 区域上限，其余异常保持失败 | 2026-08-28 |
| `GC-NOGC-REGION-LIMIT-0150` | 通过（正常 No-GC 路径） | Windows headless 设置 `16 GB` No-GC 预算；本机 CLR 成功进入 No-GC，首轮 Short 搜索在 `168.7 ms / 2.20 MB` 内产出 1 个可执行动作，GC 暂停 `0 ms`，场景 Passed。runId `04450f09159d48d9bfaca0ba9ba049e0`；该结果不覆盖 macOS 的区域拒绝分支 | 2026-08-28 |
| `KAISER-CRAB-SEARCH-REPLAY-FINAL-0150` | 通过 | 从帝王蟹问题包战前存档、原 seed 与两只怪物的 `209/199` HP 重建；修复前在第 3 回合 EndTurn 回放稳定复现缺失 `Rocket.ChargeUpStrengthGain`，修复后搜索覆盖 7 回合、未镜像效果为 0。runId `7236617d45b54d17b98bb2a8a68fcf21`，基线 runId `42db83c00fbb43548135efadaed5604d` | 2026-08-28 |
| `KAISER-CRAB-INCREMENTAL-SHORT-FINAL-0150` | 通过 | 同一问题包状态以 Low/Short 完成增量分叉与完整前缀回放对照，搜索覆盖 9 回合、未镜像效果为 0。runId `2067fe850f4d4cd0b011c7e1ce05e40a`；High 完整验证因仪器开销在 120 秒超时，runId `343c4c6b3ba54ef58050f6d1a898ac05` | 2026-08-28 |
| `MONSTER-MOVES-BATCH-021-KAISER-0150` | 通过 | 帝王蟹 10 个行动的实机/模拟严格差分全部通过；火箭蓄能获得 `2` 点力量，激光与重新充能保留累计状态。runId `7bcbece1c9e04a36a72b8ddddb2db361` | 2026-08-28 |
| `CALCULATED-VAR-ROOT-CAPTURE-FINAL-0150` | 通过 | 耗尽堆 `EXPECT_A_FIGHT` 固定 `CalculatedBlock=16 / CalculationBase=15`；修复前根投影稳定复现 `16 → 15` 失败，计算缓存改为派生字段后根快照通过。runId `e243b913a1a44aa8ba67e692da88d1b0`，基线 runId `0ea043478335482c84408617ab91e38a` | 2026-08-28 |
| `EXPECT-A-FIGHT-CALCULATED-BLOCK-FINAL-0150` | 通过 | 玩家持有 5 点力量时打出 `EXPECT_A_FIGHT`，实机与模拟完整状态严格一致，证明移除派生缓存没有丢失公式输入或实际格挡。runId `bccf3de8a91f4aad94af02b589a77d1a` | 2026-08-28 |
| `CARD-DOWNGRADE-STATE-AUDIT-382-0150` | 通过 | 魔法骑士抑制对手牌、抽牌堆和弃牌堆的 8 类升级牌执行降级，并在施法者死亡后恢复；实机与模拟逐实例状态一致。runId `95795debe881414e8d8179921061e20e` | 2026-08-28 |
| `KNIGHTS-ELITE-SEARCH-FINAL-0150` | 通过 | 从三骑士问题包战前存档、原 seed、进阶与 `108/97/89` HP 重建，首轮搜索正常返回可部署路线；复杂嵌套随机选牌的失效候选没有再中止搜索。runId `411ab4cdfe514a7cab2bac384354beb5` | 2026-08-28 |
| `KNIGHTS-ELITE-BUNDLE-FULL-AUTO-FINAL-0150` | 通过 | 同一战前存档以 Instant/0 秒完整自动部署，第 1 回合结束战斗，计划外重算 0。当前源码首抽路线与 `0.14.11` 原包不同，不记作原包逐动作回放。runId `bc508c1d2a75438599fc4cb26656acf4` | 2026-08-28 |
| `KNIGHTS-ELITE-INCREMENTAL-FINAL-0150` | 通过 | 同一问题包重建状态以 Low/Short 完成增量分叉与完整前缀回放一致性，首轮返回 11 个动作并结束战斗。runId `b0740c38be024c61a73a7c7aa281164a` | 2026-08-28 |
| `STATE-FIELDS-DERIVED-CALCULATED-0150` | 通过 | CoverageCatalog 将 43 个原版 `CalculatedVar` 字段登记为 `Derived`，未分类状态字段为 0；真实基础变量、私有状态和字符串显示字段分类保持不变 | 2026-08-28 |

## 0.14.13 Loadout 战斗费用兼容

| 场景 | 结果 | 验证内容 | 日期 |
| --- | --- | --- | --- |
| `LOADOUT-EVERY-CARD-FREE-ROOT-1413` | 目标路径通过，完整断言受限 | 投影实际 Loadout `0.4.10` 与 BaseLib `3.4.5` 后进入小啃兽战斗。第一轮成功创建并 Fork 根快照，未再出现 `LoadoutEveryCardFreeCombatHook` 的 `SEARCH_SETUP_FAILURE`；随后旧测试把 ModHelper 运行级 subscriber 误算进原版前缀，runId `7c5c868146194a05a7d038d93c31feb3` 在外围计数断言失败。修正断言后的第二轮在 Loadout 的 headless 战斗房间资源预载处超时，未进入战斗断言，不记为完整通过 | 2026-08-28 |

## 0.14.12 同族小队压缩连锁

| 场景 | 结果 | 验证内容 | 日期 |
| --- | --- | --- | --- |
| `kin-boss-route-clean` | 通过 | 固定 `oldE0VXH9PVN8`、进阶 10、第一幕同族小队、三敌 `63/62/199` HP、铁甲战士 `56/85` HP 与存档牌序；Smart、Instant 完整自动执行。第 4 回合燃烧契约、愤怒、余烬后使用灰水耗尽六张牌，第 5 回合以愤怒、放血、燃烧契约及连续攻击击杀三敌；最终 3 HP，战损 53，第 5 回合获胜，计划外重算 0。runId `41108853af1640fa8ee3379793469fc9` | 2026-08-28 |

## 0.14.10 敌方攻击压制保路

| 场景 | 结果 | 验证内容 | 日期 |
| --- | --- | --- | --- |
| `INSATIABLE-MALAISE-CONTROL-150` | 通过 | 固定无厌沙虫“液化地面”、日志首手与有序牌堆，High 搜索的首个可执行动作必须为萎靡+；X 牌先吃满 4 能量，再考虑 0 费牌。runId `d46f2d8c7adb4bbc89b1b5c8a6b8cdeb` | 2026-08-27 |
| `MALAISE-CONTROL-NIBBITS-REGRESSION-150` | 战损通过，回合断言失败 | 双小啃兽仍为 `0` 战损、`0` 计划外重算、两次洗牌；实际第 6 回合结束，请求沿用了第 5 回合精确断言，因此结果状态为 Failed，不计作整场通过。runId `a6ef1d75a48d46f2be7b98ce7ef4def5` | 2026-08-27 |

## 0.14.9 Tender 出牌完成结算

| 场景 | 结果 | 验证内容 | 日期 |
| --- | --- | --- | --- |
| `HUNTER-KILLER-TENDER-CARD-SEQUENCE-149` | 通过 | 猎人杀手场景注入 Tender，依次打出后空翻、中和+、打击；敌方实际只损失 `7` HP，力量/敏捷各降 `3`，逐字段 actual/simulated 一致。runId `79929fef88b3495cbe60e4d529594a31` | 2026-08-27 |
| `TENDER-INCREMENTAL-CARD-COMPLETION-149` | 通过 | 两张打击覆盖 Tender 的逐次出牌完成结算，增量分叉与完整前缀回放一致，首回合结束且计划外重算 `0`。runId `bb70d9239f78495e988682b40bda9bec` | 2026-08-27 |
| `TENDER-FULL-AUTO-REUSE-149` | 通过 | 猎人杀手完整自动部署后进入第 2 回合，continuation 精确复用，计划外重算 `0`。runId `c242e1f6287c484cbae5925b36a995f5` | 2026-08-27 |
| `MONSTER-MOVES-BATCH-033-TENDER-149` | 通过 | 旧 Tender 双打击与玩家回合末力量/敏捷恢复严格差分继续通过。runId `5da676be79aa45e7b4f6cff40b353fa4` | 2026-08-27 |
| 问题包战前存档重建 | 未进入战斗 | 现有无人入口在原版 `NOverlayStack` 初始化阶段空引用；不计作问题包回放通过 | 2026-08-27 |

## 0.14.8 回合首张牌出牌间隔

| 场景 | 结果 | 验证内容 | 日期 |
| --- | --- | --- | --- |
| 回合抽牌完成到首张牌 | 未执行 | 按用户要求不运行测试；实现让全自动在原版抽牌及回合准备动作完成后，等待“牌间额外停顿”再恢复路线或部署首张牌 | 2026-08-26 |

`0.10.0` headless 接通阶段保留三条未计为通过的开发证据：首次隔离启动因未确认 Mod 警告而跳过全部 Mod；允许 Mod 后因关闭 Steam 而找不到创意工坊 RitsuLib；首次长线因无窗口“战斗基础”教学节点空引用而停住。启动器现分别通过隔离设置、临时 RitsuLib 投影和仅无人请求活动时跳过纯 UI 教学解决。熵的两个前置夹具也未冒充通过：低血双敌在第 `2` 回合先发生减员，导致第 `3` 回合按死亡敌人状态差异保守重搜；单敌夹具则被怪物自身 `2` 项未镜像效果的严格断言提前拒绝。最终通过项使用满血小啃兽，隔离了熵与 RNG 本身。

## 0.14.7 内存检查点续搜

| 场景 | 结果 | 验证内容 | 日期 |
| --- | --- | --- | --- |
| `GC-CHECKPOINT-RESUME-0147` | 通过 | 1 GB No-GC 压力下触发 5 次 Beam 检查点；每次从原回合层/出牌深度续搜，不从根重算。后台全代非压缩回收暂停 `3.1-4.0 ms`，托管存活量降至 `100-205 MB`；完整 6 回合获胜、零非预期重算、无 `>50 ms` 帧和 No-GC 耗尽 | 2026-08-26 |

## 0.14.6 动作选牌与部署高亮时序

| ID | 状态 | 场景与断言 | 最近验证 |
|---|---|---|---|
| `AEONGLASS-WITHER-CHOICE-TIMING` | 通过 | 凋零气场 `CardsLeft=1` 时打出杂技+；模拟与原生选牌候选都断言不含尚未由 `AfterCardPlayed` 生成的凋零，选择真实防御后完整状态一致。runId `4e3576a6ffc546c6979c768ea6f46f60` | 2026-08-26 |
| `MANUAL-CHOICE-TRANSACTION-ADJACENT` | 通过 | 生存者、杂技+、早有准备+、燃烧契约依次覆盖弃牌、抽后弃、抽二弃二和耗尽后抽牌；四项 actual/simulated 有序牌堆、Power、逐牌状态与 RNG 严格一致。runId `bcdaab927b3d405c8b8d20e3d4de4c93` | 2026-08-26 |
| `AEONGLASS-WITHER-CHOICE-FULL-AUTO-FINAL` | 通过 | 搜索在凋零气场 `CardsLeft=1` 下规划杂技+弃防御，再打出抽到的打击击杀永世沙漏；原生页面只请求并完成一次弃牌，增量/完整回放一致，计划外重算 0。runId `3ad4ff73aefd4136b51d7596741a7795` | 2026-08-26 |
| `TOOLS-UI-ACTION-ALIGNMENT` | 通过 | 第 2 回合必备工具页面完成后精确复用；回合准备胶囊不占部署索引，真实第一张牌为 `active_action_index=0`，牌完成后 500 ms 间隔内活动索引为空，原生页面 `search=0`，计划外重算 0。runId `268f83840ffc40fdb182edf1c03ff2f3` | 2026-08-26 |
| `PAELS-EYE-TOOLS-UI-ALIGNMENT-FINAL2` | 通过 | 首回合 0 张出牌直接结束并触发佩尔之眼，直接结束胶囊经历 active/complete；额外回合出现必备工具页面后复用第 2 回合，第一张牌仍从动作索引 0 开始，原生页面不搜索且计划外重算 0。runId `5e0966d1ce114496b2c4292a60d3871b` | 2026-08-26 |

## 0.14.5 佩尔之眼与路线重放胶囊

| ID | 状态 | 场景与断言 | 最近验证 |
|---|---|---|---|
| `PAELS-EYE-LIVE-END-TURN` | 通过 | 静默猎手只持有佩尔之眼，首回合 0 张出牌并直接结束；开启全自动“重算后战损增加暂停”以强制经过实机结束回合风险复核。路线与 Overlay 均标注 `PAELS_EYE:额外回合`，实际未触发 `live_end_turn_risk` 暂停，直接进入额外玩家回合并 `Reuse:Turn=2`，`UnexpectedReplans=0`。runId `ee7607c122bf4623a785daa13a3dc993` | 2026-08-26 |
| `OVERLAY-REPLAY-BADGE` | 通过 | 手牌只有螺旋附魔打击；搜索计划记录该实例附魔后重放次数为 1，Overlay 动作快照在牌名后显示 `重放×1`，随后实际打出该牌。runId `77cc4df7265640d9b78f93768a333f15` | 2026-08-26 |

## 0.14.4 单步选牌页接管与间隔

| ID | 状态 | 场景与断言 | 最近验证 |
|---|---|---|---|
| `SINGLE-STEP-TOOLS-TAKEOVER-EXECUTE` | 通过 | 单步先停在第 2 回合必备工具原生手牌页，求解器尚未选择；随后请求“执行本回合”，按既有计划完成选择，直接复用第 2 回合且计划外重算 0。设置 500 ms 牌间停顿，选择完成到下一张牌实测 610 ms。runId `48ec35fe5d4e45e38ed6fbed3fc012e4` | 2026-08-26 |
| `SINGLE-STEP-TOOLS-TAKEOVER-FULL-AUTO` | 通过 | 同一停住边界在原生页面开启全自动；既有计划完成选择后复用第 2 回合，计划外重算 0，500 ms 设置下实测间隔 609 ms。runId `f82172f5aa264f5d9d73b6c59e79a2a9` | 2026-08-26 |
| `FULL-AUTO-TOOLS-CROSS-TURN-0144-FINAL` | 通过 | 开启增量/完整回放核对并以 `Instant / 0 秒` 完整自动部署；第 2、3 回合必备工具页面均为 `visible=2 / selected=2 / search=0`，两回合都复用首轮路线，计划外重算 0，第 3 回合结束。runId `cfb96d6f67b74b7797dec580983b9bbb` | 2026-08-26 |

## 0.14.3 部署动作完成边界

| ID | 状态 | 场景与断言 | 最近验证 |
|---|---|---|---|
| `MONSTER-WATERFALL-SLY-AFTER-DEATH-ORDER` | 通过 | 双怪局中回响斩击先把 1 HP、带蒸汽爆发的瀑布巨兽转入蓄爆，再同回合由原生杂技页面弃升级战术大师。动作完成态为能量 2，战术大师与杂技均在弃牌堆；第 2 回合直接 `Reuse:Turn=2`，`UnexpectedReplans=0`。runId `cabb121a5a1544a196dd1bda013884b2` | 2026-08-26 |
| `MONSTER-WATERFALL-DEPLOYMENT-SETTLEMENT` | 通过 | 按玩家日志重建静默猎手 22 张有序牌堆与瀑布巨兽长线，`Instant / 0 秒` 全自动于第 10 回合结束，完整经过 `ABOUT_TO_BLOW_MOVE` 与 `EXPLODE_MOVE`；1 次搜索、9 次续用、计划外重算 0。runId `24ecb35d75b4417aa6cd7a44652dcc65` | 2026-08-26 |
| `DEPLOY-EXACT-POTION-ACTION` | 通过 | 强制至少使用一瓶药水，实际入队并使用弱化药后打出攻击，于首回合结束；验证药水部署也能捕获并等待本次 `UsePotionAction`。runId `dfed9e7bbf884d7f8fdf07960831bef4` | 2026-08-26 |

## 0.14.2 单步边界与同名重放卡牌

| ID | 状态 | 场景与断言 | 最近验证 |
|---|---|---|---|
| `SINGLE-STEP-TOOLS-SPIRAL` | 通过 | PUNCH Construct，抽牌堆仅有普通防御和螺旋附魔防御，玩家已有必备工具。初始路线的 `EndTurn.TurnStartChoices` 精确指向普通防御；执行本回合后停在第 2 回合原生手牌页，全自动关闭且 `turn_setup:2` 没有 Selected 记录。runId `07d96613e313442a99275ee969e1c02b` | 2026-08-26 |
| `FULL-AUTO-TOOLS-SPIRAL` | 通过 | 同一逐实例牌组开启全自动。第 2 回合原生手牌页 `visible=1 / selected=1 / search=0`，选择普通防御后直接 `Reuse:Turn=2`，`UnexpectedReplans=0`；本次日志没有“原生选牌会话没有位于活动栈顶”。runId `80552c268daf450cbce05aeb9314b844` | 2026-08-26 |
| `PUNCH-CONSTRUCT-20260826-BUNDLE` | 受限 | 问题包确认旧版在后续回合重复报告 `turn_setup:N` 会话栈异常，并记录第 3 回合普通防御只提供 3 点格挡。包内检查点位于必备工具选择之后，无法精确恢复选择前手牌；不记为整战复现通过，逐实例选择由上述两个定向夹具覆盖 | 2026-08-26 |

## 0.14.1 原生选牌定版

| ID | 状态 | 场景与断言 | 最近验证 |
|---|---|---|---|
| `NATIVE-CHOICE-REPLAY-NO-SEARCH-556/557` | 通过 | 首回合工具盒先显示原生页面，再搜索三个候选并按 `Visible → SearchStarted → Selected` 完成；第 2 回合必备工具读取上一轮 `EndTurn.TurnStartChoices`，原生手牌页 `visible=1 / selected=1 / search=0`，随后直接 `SEARCH_REUSED turn=2`。Steam 可见机甲整战共显示并完成 6 次手牌选择，页面期间 `search=0`，第 3 回合复用恢复通过，第 7 回合结束且计划外重算 0 | 2026-08-26 |
| `NATIVE-CHOICE-SURFACES-553/560` | 通过 | 当前工作树把求解器接管的选牌改为原版可见页面：工具盒使用 ChooseCard；选择悖论使用简易网格；烘焙手套、赌博筹码、助能生存者、出牌弃牌使用手牌页面；全息影像使用战斗牌堆页面；武装使用手牌升级页面。首回合页面后搜索，后续回合只重放既有路线；动作内选择在对应事务中播放，各场景保持精确 Play 状态或零计划外重算 | 2026-08-26 |
| `NATIVE-CHOICE-STRICT-DIFF-554` | 通过 | 无 UI 严格差分仍使用测试专用选择器，生存者、杂技、早有准备等推断选牌 12/12 完整状态一致；生产 `Runtime/` 除原生观察驱动外禁止调用 `CardSelectCmd.PushSelector`，覆盖扫描 85 个调用点、0 未解析 | 2026-08-26 |

## 0.14.0 重构验收

| ID | 状态 | 场景与断言 | 最近验证 |
|---|---|---|---|
| `HIDDEN-GEM-REPLAY-552` | 通过 | 从玩家 `0.13.35` 问题包恢复猫头鹰法官首轮的 7 张手牌、30 张有序抽牌、跑局快照与 RNG。High 固定根主动打出未掘宝石，使灵体获得 2 次额外重放，并从原“仅死亡路线”改为第 8 回合胜利；第 2-8 回合精确复用、0 药、零计划外重算。独立一步差分通过；Low 增量/完整前缀核对同样获胜（第 10 回合）；双小啃兽增量长线保持第 5 回合、两次洗牌、0 药、0 战损 | 2026-08-26 |
| `REFACTOR-FINAL-NIBBITS-551` | 通过 | 从最终提交构建的 Release DLL 开启根快照与增量/完整回放核对；双小啃兽第 5 回合结束、两次洗牌、0 药、0 战损，第 2-5 回合精确复用且零非预期重算。首轮 `6.21 s / 2.47 GB / 0 ms GC / 17.2 ms 最大帧` | 2026-08-26 |
| `REFACTOR-FINAL-MECHA-HIGH-550` | 通过 | 从最终提交以原固定快照和 High 预设复跑：第 5 回合结束，第 2-5 回合精确复用；`expanded=4624`、`transitions=33432`、`choice_branches=17735`、`dominance/transposition/repeatable=214/700/0`，`11.45 s / 3.55 GB / 0 ms GC / 17.6 ms 最大帧`。此前把增量全回放诊断与性能门槛组合的请求因 `100.4 s / 34.4 GB` 正确失败；中档请求因第 7 回合结束正确失败，二者均未计为通过证据 | 2026-08-26 |
| `REFACTOR-FINAL-NIBBITS-549` | 通过 | 根怪物从活动 roster 移除后仍保留本分支 AI/静态参数，允许正在执行的怪物行动完成尾部结算；原第 4 回合稳定崩溃夹具现于第 5 回合结束、两次洗牌、0 药、0 战损、逐回合复用且零非预期重算 | 2026-08-26 |
| `MIRROR-REGISTRY-DESCRIPTOR-548` | 通过 | action/result registry 统一提供支持 descriptor，CoverageCatalog 删除对三个私有字段及 MethodSpec 布局的反射；切换前后 3035 项及全部门禁/生成文件一致，钢笔尖 Hook 增量路线与真实部署通过 | 2026-08-26 |
| `SOLVER-OVERLAY-SNAPSHOT-547` | 通过 | 控制器一次性捕获 Overlay/Turn/Action 只读快照，三个 Renderer 不再读取搜索/预测可变类型；钢笔尖两动作路线真实渲染并部署，遗物后缀、击杀路线、ready/deploying/complete 状态和速度恢复均通过。人工布局与字体仍按 UI 人工项执行 | 2026-08-26 |
| `UNATTENDED-EXECUTOR-546` | 通过 | 差分分派、设置覆盖、搜索/部署等待、提前停止与完整自动战斗进入 `Executor`；双球两项严格差分、强制一瓶药首回合击杀、速度恢复和 Held 结果均通过，普通请求复用同一进程 | 2026-08-26 |
| `UNATTENDED-ASSERTIONS-545` | 通过 | 执行前预测/Fork/根快照/会话/CardModifier 检查及执行后回合、生命、出牌、用药、Power 断言进入 `Assertions`；根快照检查、实际打出指定卡和首回合结束在同一场景通过 | 2026-08-26 |
| `UNATTENDED-SCENARIO-BUILDER-544` | 通过 | 建局、进入遭遇、怪物/生命/牌堆/球/药水/遗物/Power/RNG 注入进入 `ScenarioBuilder`；Defect 双球两项严格差分通过。故意注入错误敌人数时仍记录 `inject_state` 与真实第 1 回合，随后同进程恢复成功 | 2026-08-26 |
| `UNATTENDED-WRITER-543` | 通过 | Passed/Held/Failed 的公共协议字段、内存采集和临时文件原子替换进入 `Writer`；同一进程依次写出成功、故意断言失败和失败后恢复成功三份结果，状态、阶段、错误与进程复用均正确 | 2026-08-26 |
| `UNATTENDED-PROTOCOL-HOST-542` | 通过 | 请求文件接收、协议版本、每请求测试开关、状态漂移和清理进入 `ProtocolHost`；同一 headless PID 连续完成两场首回合击杀，第二场明确 `UNATTENDED_REUSED`，最后按请求退出 | 2026-08-26 |
| `FINAL-ORDERING-POLICIES-541` | 通过 | 同一击杀夹具依次验证 Disabled/Smart 均保留 0 药路线，RequireAtLeastOne 选择并实机使用 1 瓶弱化药；固定防御牌组保持主动卖血 `5/5` 上限并剪除超预算路线 | 2026-08-26 |
| `FINAL-PLAN-ORDERING-540` | 通过 | `Solve` 的终局胜负、药水、卖血和边界排序迁入 `FinalPlanOrdering`，候选通过 `SearchFeatures` 读取固定特征。机甲保持第 5 回合、同动作序列、`4624/33432/17735` 与全部剪枝计数，`11.51 s / 3.55 GB / 0 ms GC / 18.8 ms`，零重算 | 2026-08-26 |
| `FINAL-ORDERING-DUAL-539` | 通过 | 切换前由旧排序和 `FinalPlanOrdering` 对同一候选集合逐字段比较选中节点、得分、药水与卖血统计；钢笔尖增量路线一致并首回合无损击杀 | 2026-08-26 |
| `BEAM-RETENTION-POLICY-538` | 通过 | 状态去重、Beam 排名、多样性通道、药水配额和 Pareto 保留进入具体策略；只通过 stand-pat 委托访问模拟。机甲保持第 5 回合、同动作序列、`4624/33432/17735` 与全部剪枝计数，`11.64 s / 3.55 GB / 0 ms GC / 17.1 ms`，零重算 | 2026-08-26 |
| `SEARCH-RUN-CONTEXT-537` | 通过 | 15 个搜索计数器、性能/节流、转置及四类缓存收口到单次 `SearchRunContext`，不池化或改算法。固定机甲保持第 5 回合、同动作序列、`4624/33432/17735` 与全部剪枝计数，`11.52 s / 3.55 GB / 0 ms GC / 17.4 ms`，零重算 | 2026-08-26 |
| `BEAM-PARTIAL-SPLIT-536` | 通过 | `CombatBeamSolver` 纯移动为七个阶段 partial；结构门禁固定文件和代表方法归属。机甲完整 headless 保持第 5 回合、同动作序列、4624 展开、33432 转移、17735 选牌分支与全部剪枝计数，`11.35 s / 3.55 GB / 0 ms GC / 17.2 ms`，零重算；Defect 球/Synchronize 严格差分通过 | 2026-08-26 |
| `MOD-SUBSCRIBER-BOUNDARY-534` | 通过 | BaseLib/Loadout subscriber 分段捕获；实际 CardModifier 夹具验证克隆、Owner 重绑和写时复制，Ritsu capability 反向夹具验证非空集合仍走原属性贡献。空 capability 快通道及 Fork listener 缓存继承把机甲分配从约 `4.98 GB` 降至 `3.57 GB`；连续两次完整 Mod 可见整战均为第 5 回合胜利、`0 ms GC`，最大帧 `8.6/16.5 ms`。带实际 Modifier 的最终轮仍为 `11.87 s / 3.57 GB / 0 ms / 13.5 ms` | 2026-08-26 |

## 已通过的无人场景

| ID | 状态 | 场景与断言 | 最近验证 |
|---|---|---|---|
| `CARD-POWER-NESTED-534` | 通过 | 卡牌 Power 结算改为可嵌套源栈；Unsettling Lamp 按触发卡关联。Knife Trap/Eidolon 自动出牌和升级 Knife Trap 双 Shiv 的模拟/原生差分通过 | 2026-08-26 |
| `BUILTIN-LISTENER-IDENTITY-533` | 通过 | Badge listener 以根克隆进入预测；多人缩放 listener 清除 live RunState/CombatState，并由单人 mirror 返回精确倍率。根隔离和双小啃兽增量整战通过；Loadout/BaseLib 第三方订阅另列适配项 | 2026-08-26 |
| `MONSTER-MODIFIER-IDENTITY-532` | 通过 | Modifier 以根克隆进入 Hook；永世沙漏生成凋零读取分支升级计数，Murderous 对预测召唤敌人施加 3 力量，召唤遗物读取根清单。首次差分暴露直接构造模拟器未物化根，统一构造边界后定向差分、根隔离和双小啃兽增量整战通过 | 2026-08-26 |
| `RUN-SNAPSHOT-HOOK-PREFIX-531` | 通过 | Run 标量、RNG、起始回合和 Hook 前缀进入主线程根；牌组 Card/Enchantment listener 使用克隆，卡池筛选显式消费捕获约束。根隔离、攻击药生成差分和双小啃兽增量整战通过 | 2026-08-26 |
| `ROOT-MODEL-INVENTORY-530` | 通过 | 玩家回合/金币、Relic/Potion、卡牌注册、Osty、初始 Power、Run RNG 和怪物私有字段进入主线程根；listener 使用克隆。首次严格复跑抓到并根修复 Relic `AfterCloned` 重置私有计数；最终遗物 Hook 11/11、钢笔尖、Knowledge Demon、Smart 救命药和双小啃兽增量整战通过 | 2026-08-26 |
| `COMBAT-ROOT-SNAPSHOT-529` | 通过 | 搜索根只能在主线程捕获；live 与根投影 continuation 逐项一致。捕获后修改实机能量，后台 Fork 仍保持捕获值。Beam 根、当前历史和 Hook listeners 不再从 worker 惰性构造；钢笔尖增量与双小啃兽普通/增量整战保持通过 | 2026-08-26 |
| `FORK-BOUNDARIES-528` | 通过 | Fork 在克隆前统一拒绝未完成 trace、选择、出牌、Hook 私有事务、延迟历史和遗物记录；钢笔尖与蜷身的瞬时引用不进入稳定节点。臂铠触发卡由 `0.15.0` 依据原版生命周期纠正为可 Fork 的持续状态。配对中途死亡、钢笔尖增量、两组 Hook 差分及双小啃兽增量整战全部通过并零重算 | 2026-08-28 |
| `CONTROLLER-SESSIONS-527` | 通过 | 战斗、搜索和部署状态进入独立会话；取消搜索后旧 callback 不得写回。战斗结束异步 GC 回收与新搜索按完成信号串行。策略快照/取消/重搜/完整部署通过；双小啃兽普通与增量均第 5 回合、两次洗牌、0 药、0 战损并零重算 | 2026-08-26 |
| `REFACTOR-BOUNDARIES-526` | 通过 | 不支持的动态数值、推断 OnPlay 异常与搜索转移异常均 fail-fast 且保留搜索上下文；搜索只消费主线程捕获的策略快照。双小啃兽普通/增量均第 5 回合、两次洗牌、0 药、0 战损并成功复用；药水 17/17、推断选牌 12/12、推断卡 43/43、CalculatedVar 25/25 通过；Smart 与至少一瓶策略均零重算 | 2026-08-26 |
| `DECIMILLIPEDE-LATE-DEATH-REATTACH-524` | 通过 | 肢节先执行正常行动、再于同一敌方回合死亡时，`DEAD_MOVE` 按原状态机直接过渡到 `REATTACH_MOVE`，死亡保留 Power、行动历史、私有死亡阶段和九条 RNG 严格一致；结束回合产生的复活窗口进入通用 Beam 保留。亡灵契约师问题包第 6 回合两药胜利，第 2-6 回合精确复用、零计划外重算；上一份千足虫和双小啃兽普通/增量均保持通过 | 2026-08-25 |
| `DECIMILLIPEDE-DEAD-TO-REATTACH-521` | 通过 | 反馈包中复活肢节实际执行 `DEAD_MOVE` 后，模拟与原版一同推进重接后继，不再把 0 HP 的 `Reviving` 肢节永久冻结在死亡动作。修复前重建整战出现 1 次计划外重算；最终仓库夹具 Smart、Instant/0 秒第 4 回合结束，第 2-4 回合精确复用、零计划外重算，首轮三药计划完整执行且不再反复变化 | 2026-08-25 |
| `MYTES-SMART-INDEPENDENT-POTION-AUDIT-519` | 通过 | 同一异螨开局的统一 Smart Beam 错把无药战损估为 31，选择三瓶药掉 1；独立 Disabled 搜索实际为 0 药掉 11。Smart 选中药水后固定运行独立禁药反事实，纠正为三药只省 10、低于 27 门槛；最终 0 药、第 8 回合、预计/实测均掉 11，第 2-8 回合精确复用、零计划外重算。无药必死救药与既有低损无药回归保持通过 | 2026-08-25 |
| `TWO-TAILED-RAT-RAND-WEIGHT-507` | 通过 | 原版尖啸参数 `3` 按三回合冷却而非三倍权重处理；固定问题包种子的一步差分中，疾病啃咬后的原版与模拟均选择抓挠。用户存档 Medium、Smart、Instant/0 完整自动战斗第 6 回合结束，预计/实测均掉 9，第 2-6 回合精确复用、零计划外重算；500 ms 短搜增量等价与第 2 回合续用通过 | 2026-08-25 |
| `WATERFALL-HORIZON-LIFECYCLE-506` | 通过 | 两个 0.13.27 用户问题包复现节点上限未完成路线在第 12 回合空过，以及蒸汽喷发致死后阵容、无限生命、AI 与 Power 生命周期偏差；0.13.29 以原 seed/250 HP、中档、Smart、Instant/0 秒完整自动执行，分别第 13/16 回合结束且零计划外重算；定向两回合与增量等价回归通过 | 2026-08-25 |
| `RAVENOUS-IMMEDIATE-STUN-505` | 通过 | 玩家回合击杀尸蛞蝓同伴后，幸存者立即以带原行动后继的 `STUNNED` 替换当前意图；敌方 Doom 触发与盛碗虫眩晕循环保持一致。尸蛞蝓完整战斗第 7 回合结束、零计划外重算；增量分叉与完整前缀回放一致 | 2026-08-25 |
| `CHOMPERS-PAIR-TRANSACTION-504` | 通过 | 0.13.25 啃咬机问题包首回合搜索因 pending CardPlay 配对状态在 Fork 处失败；0.13.27 恢复原开战前存档、种子、A10、第二幕、64/67 HP 和首轮行动，以 Medium、Smart、Instant/0 完成普通与增量整战，均第 11 回合结束、零非预期重算、无 cannot fork | 2026-08-25 |
| `SLUMBERING-PAIR-OBLIVION-498-503` | 通过 | 0.13.25 睡眠甲虫问题包的 CardPlay 配对状态在监听 Power 被移除后仍于动作完成边界核销；连续两次湮灭严格使用出牌前 3 层快照而非叠加后的 6 层。0.13.27 最小差分预测/实机灾厄均为 6；原存档 Medium、Smart、Instant/0 完整战斗与增量等价均第 7 回合结束、零非预期重算、无 cannot fork | 2026-08-25 |
| `USER-BUNDLES-PAIRED-THEFT-DEBILITATE-492-497` | 通过 | 外骨骼结算中移除监听器后卡牌配对事务在动作完成边界清空，开启增量等价的原存档整战第 5 回合结束；偷窃草蜢保留已修改牌的 DeckVersion，三候选偷牌 RNG、振翅归零眩晕和后继行动与原版一致，原存档第 5 回合结束；仪式兽虚弱/易伤读取分支 Debilitate，原存档第 9 回合结束；三场完整自动战斗均为 Medium、Smart、Instant/0 且零非预期重算 | 2026-08-25 |
| `QUEEN-ROUTING-OPT-491` | 通过 | 幕末 Boss 深搜为选牌历史保留 50% 策略位，普通深搜 40%，并联合保留威胁集火、潜在能力、下回合资源和关键攻击；女王中档 1 瓶敏捷药、第 11 回合、预计/实测 0 战损、零重算；双小啃兽维持 0/0；发布 ZIP 干净安装后 Steam 可见机甲 12.23s/3.82GB、战损 30、GC 0、最大帧 9.4ms | 2026-08-25 |
| `RAVENOUS-QUEEN-LONGLINE-488-490` | 通过 | 蛞蝓玩家侧击杀与敌方回合末 Doom 都应立即建立 `STUNNED`；`0.13.28` 已纠正旧证据中的玩家侧延迟时序。Buffer/生成牌/球/虚无/Echo Form 状态修复；女王日志重建夹具由 0.13.24 的预计掉 25 作为本轮优化基线 | 2026-08-25 |
| `USER-REPORT-PAELS-ROUTES-484 / CONTROL-MODE-485-486` | 通过 | 熟睡甲虫、虫术师、炮台操作员精确还原佩尔之眼额外回合并全程零重算；凯撒蟹与永世沙漏找到生还路线；计划外重算告警固定在标题右侧，手操后再由求解器接管仍会告警；问题包区分 `solver_only` 与 `manual_plus_solver` 并记录最近完整自动执行回合 | 2026-08-25 |
| `THEFT-ILLUSION-CHOMPERS-480-482` | 通过 | 偷钱地精/偷窃草蜢仅在对应遭遇显示“保牌/保钱、放走”，分支内追踪被盗资源并按模式决定卖血/用药；幻象被灾厄回合末击杀后保留复活意图，佩尔之眼额外回合有遗物标注；啃咬机精确初手与 17 张抽牌堆第 6 回合获胜、预计/实际掉 21、零重算；甲虫汁先消耗人工制品且不施加缩小 | 2026-08-24 |
| `BUG-REPORT-FORENSICS-478` | 通过 | 活动战斗和战后错误时机导出均逐检查点包含 metadata、结构化中途状态、原生战斗包和即时跑局存档，并解析完整 RNG、五牌堆、阵容、历史和当时设置；真实喝药后第 2 回合结果分别记录已喝 1、未来 0 | 2026-08-24 |
| `RADIATE-AND-REQUIRE-ONE-472-477` | 通过 | 崇拜/胜券在王/辉光正确累计本回合星能，辐射连击完整；女王真实第 2 回合检查点当前回合 0 战损斩杀；“至少一瓶”对多瓶路线追加无药反事实并只保留一瓶，喝过药后的重算不重复强制；速度药不残留负敏捷，Smart 致死救药不退化 | 2026-08-24 |
| `INITIAL-OSTY-AND-PAIRED-FORK-465/466` | 通过 | 亡灵契约师持绑定护命匣和赌博筹码时，首回合不重复召唤奥斯蒂，选择后完整状态一致；独白配对状态下攻击 99 荆棘导致中途死亡时清理本次瞬时配对，搜索可继续分叉并按死亡回合暂停 | 2026-08-24 |
| `BUG-REPORT-FORENSICS-469/471` | 通过 | 同一战斗先活动导出、击杀并回主菜单后再次导出；current/recent 均含内存跑局快照、完整 Run RNG、玩家 RNG/odds、检查点、路线和重算审计；战后无当前战斗仍可还原最近一场，同秒连续导出不撞名；5 回合两次洗牌保持零重算 | 2026-08-24 |
| `SMART-POTION-COUNTERFACTUAL-461/463` | 通过 | 淤泥旋螺 Smart 首搜三药但无可信无药终局时追加纯无药审计，找到无药掉 2 后拒绝三药并第 5 回合结束；1 HP 致死反向场景审计确认无药不胜，保留格挡药并第 2 回合获胜 | 2026-08-24 |
| `NECROBINDER-OSTY-RAVENOUS-453/454` | 通过 | 奥斯蒂被连击击杀后由护卫复活，“为你而死”保持 1；蛞蝓同伴死亡后幸存者获得 5 力量并立即进入带原行动后继的 STUNNED；用户完整战斗第 5 回合结束，第 2-5 回合精确复用、零重算 | 2026-08-24 |
| `SECONDARY-END-AND-GENERATION-447-450` | 通过 | 用户储君 Fogmog 存档从错误的 486 回合/470 次洗牌改为第 3 回合结束，完整自动战斗零重算、无生成牌越界；定向主怪击杀+幻象次要敌人首回合正确结束；生成选择 4/4 与实验体三形态保持通过 | 2026-08-24 |
| `REGENT-PRINT-BRANCH-441/446` | 通过 | 固化实机缩小甲虫的储君牌序和生成牌链；两回合具体候选保护窗将节点/选牌/转移/分配约减半，并从基线第 5 回合改善为第 4 回合；完整自动战斗预计/实际掉 3，第 2-4 回合精确复用，零重算；Fisticuffs 日志洪泛为 0 | 2026-08-24 |
| `PRINT-PRUNE-REGRESSIONS-442-445` | 通过 | 生成三选一 4/4、推断卡 15/15、双小啃兽 0 药 0 战损及机甲第 7 回合全部通过；机甲和双小啃兽均零非预期重算 | 2026-08-24 |
| `COMPLETION-AUDIT-428-432` | 通过 | 当前最终 DLL：机甲第 7 回合、预计掉血 36，第 2-7 回合全部精确复用；双小啃兽第 5 回合、两次洗牌、0 药、0 战损；工具盒+烘焙手套+助能三段首回合选择、横祸嵌套选择和千足虫复活均零非预期重算；完整战斗统一使用 Instant/0 秒 | 2026-08-24 |
| `POWER-SHADOW-LIFECYCLE-425-427` | 通过 | Power 数量影子在每次 Hook 批次同步后删除；Burst 回合末不再复活旧层数。重复出牌 11/11 Hook、伤害 Power 十四场及强制 Burst 跨回合续用全部通过 | 2026-08-24 |
| `ROSTER-SOURCE-GATE-408` | 通过 | 原程序集阵容变化共 51 个调用点：47 个单人召唤/逃跑/Osty/宠物来源受支持、3 个 Mock、1 个多人来源、0 未解析；新入口会使普通覆盖门禁失败 | 2026-08-24 |
| `AUTOPLAY-SOURCE-GATE-407` | 通过 | 原程序集 `AutoPlay/AutoPlayFromDrawPile` 共 19 个调用点：18 个单人来源受支持、1 个多人来源、0 未解析；新入口会使普通覆盖门禁失败 | 2026-08-24 |
| `AUTOPLAY-NESTED-CHOICES-403-406` | 通过 | 横祸、破灭、骚动和蒸馏混沌自动打出带选择的牌；搜索规划并实机提交嵌套选择，三场整战零重算并精确复用，药水场完整状态/RNG 差分一致 | 2026-08-24 |
| `COMBAT-CHOICE-SOURCE-GATE-402` | 通过 | 扫描原程序集全部正式模型的 `CardSelectCmd` 调用：85 个调用点中 60 个单人战斗来源受支持、24 个获得遗物流程、1 个多人来源、0 未解析；新来源会使普通覆盖门禁失败 | 2026-08-24 |
| `INITIAL-NATIVE-START-EFFECTS-400/401` | 通过 | 工具盒与七件首回合遗物同场；精确覆盖宝石面具 RNG 移牌、礼炮伤害、谜盒生成、力量电池、扭曲漏斗、石化蟾蜍及低语耳环最多 13 张付费自动出牌；高密度生存者场景另强制覆盖 Vakuu 连续嵌套选牌 | 2026-08-24 |
| `INITIAL-PRE-PLAY-CHOICES-394-398` | 通过 | 从原版首回合 `Start` 阶段搜索并实际提交工具盒、选择悖论、烘焙手套、赌博筹码及助能生存者选择；五场均无玩家界面且进入 `Play` 后完整状态戳一致 | 2026-08-24 |
| `IMBUED-NESTED-CHOICE-393` | 通过 | 助能生存者首回合自动打出，计划并实际弃掉打击；模拟与原版完整牌堆、逐牌状态、资源及 RNG 一致，无玩家默认选择 | 2026-08-24 |
| `SLUMBERING-BEETLE-SILENT-392` | 通过 | 固化用户 6 HP 静默猎手、进阶 10、46/42/89 HP 三敌和原 RNG；盛碗虫完全格挡后进入可见 `STUNNED`，毒伤正确递减熟睡甲虫并切换 `ROLL_OUT_MOVE`；第 2-6 回合全部精确复用，零重算、零战损 | 2026-08-24 |
| `BOWLBUGS-AUTO-CHOICE-391` | 通过 | 直接恢复两个问题包的 BOWLBUGS 开战存档；Mayhem 先固定整批自动牌并给嵌套选择绑定牌身份，牌堆选择使用稳定快照；Custom 5/60s、Instant/0 第 2 回合结束，无选择异常、集合修改和非预期重算 | 2026-08-24 |
| `DECIMILLIPEDE-CONTINUATION-390` | 通过 | 千足虫复活段在下回合可被命中并获得毒雾，第 2 回合精确续用；另使两个 `CONSTRICT_MOVE` 连续两回合叠加 Weak，第 3 回合精确续用；两项均零非预期重算 | 2026-08-24 |
| `KIN-FULL-AUDIT-386D` | 通过 | 用户同族存档按原种子、进阶 10 和满血敌人恢复；中档预算、Instant/0 从首轮执行到第 16 回合结束，追踪之环全程读取分支虚弱，非预期重算为 0 | 2026-08-24 |
| `STATE-CATALOG-GATES-389` | 通过 | `3035` 个 Hook 门禁、分支实机读取、语义动态字段、搜索期状态写入、运行证据和原生重扫边界均为 0 缺口；首回合根状态补齐后剩余 22 个求解接管前快照写入，另有 115 个静态行动图构造器 | 2026-08-24 |
| `CARD-STATE-MUTATION-381-384` | 通过 | 98 张升级卡、8 张降级/恢复卡、5 种附魔及女妖哀嚎/精准打击/践踏/Flatten 入场状态逐字段一致；生成卡保留后续 Hook 监听 | 2026-08-24 |
| `LIFECYCLE-ORDER-378-388` | 通过 | 能量/星费、空手、药水前后、死亡阻止递归均按原版顺序；资源 4 项、药水 17 项、死亡/伤害遗物 11 项通过 | 2026-08-24 |
| `SOLVER-MONSTER-MOVE-AUDIT-387` | 通过 | 57 个补偿怪物行动按永世沙漏、旧日雕像、外骨骼及普通合法宿主分片复跑，完整状态与 RNG 全部一致 | 2026-08-24 |
| `SHRINKER-APPLIER-REUSE-373` | 通过 | 41 HP 缩小甲虫执行无限缩小后，Power 施加者名称、层数和减伤动态值与实机一致；第 2-5 回合全部精确复用，零重算、零未补偿且无错误终局边界 | 2026-08-24 |
| `OBSCURA-VIGOR-EXACT-372` | 通过 | 用户 Obscura 开战前存档恢复进阶 10、第二幕、牌组/遗物/RNG；万向斩组合攻击只消费一次 Vigor，幻象反复复活不产生非法后继；第 10 回合结束且全程零非预期重算。龙涎香 40% 门槛另完成 9 组边界计算 | 2026-08-24 |
| `VANTOM-STRATAGEM-SEARCH-364` | 通过 | 问题包的 Vantom 开战前存档恢复牌组、遗物和 RNG；战略跨洗牌不再抛错，首搜完成 7 回合、2 次洗牌、2623 个选择分支。当前结果仍为死亡线，不冒充生还 | 2026-08-24 |
| `STRATAGEM-SHUFFLE-CHOICE-365` | 通过 | 强制战略 Power 在下回合抽牌时跨洗牌；搜索计划选择打击，实机自动提交，第 2 回合状态精确复用，零重算且无玩家界面 | 2026-08-24 |
| `TEST-SUBJECT-LIVE-END-RISK-362` | 通过 | 开启“战损变差时暂停”并使用用户实验体存档；完整回合末复核计入山铜等遗物格挡，不再把预计 11 误算成 20；第 6 回合剩 66 HP，零误停、零重算 | 2026-08-23 |
| `LIVE-END-TURN-RISK-MINIMAL-363` | 通过 | 原路线以 5 格挡承受 6 点攻击、预计掉 1；提交结束回合前清零格挡后仍正确识别致死，关闭全自动并保留玩家回合 | 2026-08-23 |
| `TEST-SUBJECT-USER-RUN-360` | 通过 | 用户猎手开战前存档精确恢复牌组、遗物、四药水槽与 RNG；实验体三形态第 6 回合结束、实际剩 66 HP，第 2-6 回合全部精确复用，未镜像与非预期重算均为 0 | 2026-08-23 |
| `TEST-SUBJECT-REPTILE-TURN-END-361` | 通过 | 复制药触发爬虫饰品后推进玩家回合末；原版与模拟完整状态一致，复制、临时力量来源和附加力量均按原版移除 | 2026-08-23 |
| `TEMP-PLAN-IMPLEMENTATION-357` | 通过 | 腐臭药水、零权重 RAND、变牌生成 Hook、狠揍嵌套选牌和 Begone 部署事务定向通过；同族智能两药第 13 回合生还，双小啃兽 0 药 0 战损；4 GB No-GC 在第二次搜索前轮换 | 2026-08-23 |
| `DECIMILLIPEDE-REVIVE-350` | 通过 | 从一节 0 HP、下一行动 `REATTACH_MOVE` 开始，实机恢复 25 HP；第 2 回合全体真正死亡，首轮缓存精确复用且零重算 | 2026-08-23 |
| `TEMP-SCULPTOR-MID-358` | 通过 | 精确恢复虔诚雕刻师第 4 回合牌堆、Power、行动历史和 RNG；同回合 0 战损 0 药击杀，未镜像与重算均为 0 | 2026-08-23 |
| `TEMP-KNOWLEDGE-MID-359` | 通过 | 精确恢复知识恶魔第 11 回合状态；同回合 0 战损击杀、零重算，另由 `KNOWLEDGE-DEMON-SEARCH-CHOICE-162` 验证诅咒选择 | 2026-08-23 |
| `LIVE-END-TURN-RISK-PAUSE-270` | 通过 | 同构墨宝安全路线第 2 回合原计划产生 5 格挡；测试在执行后清零格挡，结束回合实机复核得到路线预计 0、当前预计 4 且致死，关闭全自动并保持第 2 回合，不提交结束回合 | 2026-08-23 |
| `INKLETS-RIPPLE-BASIN-269` | 通过 | 复原用户 4 HP、三只墨宝、完整手牌/抽牌、两瓶药和涟漪盆；修复前第 2 回合漏防御并死亡，修复后补打防御，第 2/3 回合精确复用、零战损且第 3 回合结束 | 2026-08-23 |
| `WORSE-RECALCULATION-PAUSE-267` | 通过 | 对一条已算到第 9 回合击杀的旧日雕像完整路线在第 2 回合注入 `4 HP` 状态漂移；记录首个差异、重算预计战损 `32→38`、界面劣化标记，并在执行该回合前关闭全自动 | 2026-08-23 |
| `BUG-REPORT-EXPORT-266` | 通过 | 设置页问题包按游戏口径在后台收集日志、档案、版本和截图，并追加当前战斗精确状态、当前路线、求解器设置和说明；Headless 回归实际创建 ZIP 并逐项验证四个附加条目 | 2026-08-23 |
| `DETAILED-DIAGNOSTIC-LOGS-268` | 通过 | 无设置文件时详细诊断默认关闭且普通日志不含 `[CombatSolver/Debug]`；测试覆盖开启后写出药水槽、分支、层与最终候选诊断 | 2026-08-23 |
| `BYGONE-EFFIGY-CONTINUATION-264` | 通过 | 复原用户旧日雕像的 16 张牌、初始手牌/抽牌顺序、进阶 10、38 HP 与初始弱化；当前版第 2-9 回合全部精确复用并按首轮预测结束。单步 `25` 攻击、`13` 格挡、1 层弱化差分同样通过 | 2026-08-23 |
| `NO-NATIVE-RESCAN-244` | 通过 | `3035` 个钩子中未分析、待实现、缺证据、非通过证据和 `NativeAutoRescan` 均为 `0`；随机生成/选牌、召唤/替换/逃跑、死亡/复活、自动出牌、额外回合、药水槽与私有 AI 均有原生差分或跨回合复用证据 | 2026-08-23 |
| `NIBBITS-NO-RESCAN-246` | 通过 | 固定双小啃兽普通搜索 `1.957s / 360.7MB`，第 `6` 回合、两次洗牌、`0` 药、`0` 战损，第 `2-6` 回合精确复用；增量分叉对完整前缀回放验证同样通过 | 2026-08-23 |
| `MECHA-NO-RESCAN-247` | 通过 | 固定机甲 `5s/60s`：headless `8.207s / 2.212GB`，Steam 正常可见完整 Mod 栈 `9.208s / 2.291GB`，均第 `8` 回合、预计战损 `31`；可见会话 GC `0ms`、最大帧间隔 `11.0ms` | 2026-08-23 |
| `PARTICLE-WALL-TOUCH-176` | 通过 | 同构日志牌堆中，粒子墙被癫狂之触设为本场 `0` 费后不再耗尽单回合节点：从修复前 `1200` 节点、`2` 回合、`NodeLimit` 改为 `619` 节点、`17` 次无进展循环剪枝、`567.2 ms / 90.95 MB`，第 `3` 回合无药无战损击杀；反向场景保留粒子墙×7后全身撞击的 `9` 动作首回合击杀 | 2026-08-22 |
| `LAGAVULIN-DEPLOY-REPLAN-175` | 通过 | 乐加维林族母睡眠阶段第 `2` 回合精确复用；`BEAT_INTO_SHAPE` 正常路线首回合真实打出；部署中实机拒绝动作会从当前状态重搜而非中止 | 2026-08-22 |
| `PERFORMANCE-PRESETS-170` | 通过 | 无设置文件时默认中档 `5/60s + 6GB`、死亡暂停开、战斗结束暂停关；低档和高档分别完整断言 `2/20s + 4GB` 与 `8/120s + 8GB` 及对应 Beam、节点、出牌分支；自定义保持独立预设身份；双小啃兽维持第 `6` 回合 `0/0` | 2026-08-22 |
| `KNOWLEDGE-BOSS-POLICY-162` | 通过 | 知识恶魔评估 `396` 个选牌分支并计划/执行 `MIND_ROT`，选择结算后第 `2` 回合精确复用；二幕 Boss 与三幕第二 Boss 标记战后回血，三幕首 Boss 与普通战斗不标记；死亡回合暂停保持战斗进行并交还操作权 | 2026-08-22 |
| `NIBBITS-0.12.2-REGRESSION-163` | 通过 | 普通战斗策略不受幕末 Boss 权重影响：固定双小啃兽第 `6` 回合、两次洗牌、`0` 药、`0` 战损、`0` 卖血并第 `3` 回合复用；首轮 `2.119 s / 386 MB` | 2026-08-22 |
| `LONGLINE-0.12.1-161` | 通过 | 双小啃兽第 `6` 回合、两次洗牌、`0` 药、`0` 战损、`0` 卖血并第 `3` 回合复用，首轮 `2.288 s / 386 MB`；机甲 headless `9.820 s / 2.266 GB`，Steam 可见完整 Mod 栈 `9.818 s / 2.350 GB / 0 ms GC / 8.4 ms 最大帧间隔`，均第 `8` 回合、预计战损 `31`、`0` 药并第 `3` 回合复用 | 2026-08-22 |
| `VITAL-SPARK-LIFECYCLE-160` | 通过 | 感染棱柱连续执行 `RADIATE_MOVE → PULSATE_MOVE`；玩家既有技能牌污染保持不丢失，活力火花和逐牌污染从 `2` 同步到 `4`，两步完整牌堆、Power 与 RNG 差分一致 | 2026-08-22 |
| `UNATTENDED-CHOICE-FIXES-159` | 通过 | 雕琢打击唯一候选按实际虚无状态核销、宇宙冷漠按抽牌堆顶核销，二者均进入后续回合精确复用；知识恶魔不可跳过但 `minSelect=0` 的选择自动提交 `MIND_ROT`，玩家获得腐化心智且无界面干预 | 2026-08-22 |
| `RELIC-HIDDEN-STATE-158` | 通过 | 钢笔尖按“愤怒第 `9` 击 → 重锤第 `10` 击×2”首回合 `0` 战损击杀并生成 `钢笔尖×2` 胶囊；百年积木覆盖完全格挡、首次抽 `3` 和不重复触发；金纸覆盖 `5` 次耗尽抽 `1` 与余数 `0`；持久遗物状态门禁为 `0` 缺口 | 2026-08-22 |
| `SOLVER-RUNTIME-ROBUSTNESS-157` | 通过 | 女王战同时持有两瓶迅捷药水时 UI/搜索正常且首回合结束；仅死亡候选显示 `OnlyDeath=True` 并真实死亡；`Instant + 0.05s` 仅内存覆盖，自动执行后恢复 `Normal` 且不生成测试设置文件；Steam 截图确认固定状态列与钢笔尖、音乐盒遗物后缀 | 2026-08-22 |
| `MECHA-VISIBLE-STEAM-145` | 通过 | `0.12.0`、Steam 正常可见会话、用户完整 Mod、固定机甲 `5s/60s`：首轮 `14.736 s / 2.438 GB`、GC `0 ms`、最大帧间隔 `42.5 ms` 且无 `>50 ms` 帧；第 `8` 回合、1 次洗牌、`0` 药、`0` 卖血、Unmirrored=`0`，预计战损从旧基线 `43` 降至 `31`；结束后托管堆约 `259 MB`、工作集约 `2.30 GB` | 2026-08-22 |
| `NIBBITS-ADAPTATION-REGRESSION-139` | 通过 | `0.12.0` 适配层完成后复跑固定双小啃兽：首轮 `1880.2 ms`、第 `6` 回合、两次洗牌、`0` 药、`0` 战损、`0` 卖血、Unmirrored=`0`；第 `3` 回合命中精确复用并无人值守结束战斗 | 2026-08-22 |
| `CARD-EFFECT-SPEC-BATCH-137` | 通过 | 参数化 Power、资源、自伤、最大生命和一次性 Power 消耗共 `46` 条模拟/原生完整快照差分通过 | 2026-08-22 |
| `CARD-COMPLETION-BATCH-123` | 通过 | 补齐卡牌、既有牌选择、击杀奖励、Osty、永久牌面成长和 X 费共 `32` 条差分通过；修复选择后抽牌时来源牌过早进入弃牌堆并参与同次洗牌 | 2026-08-22 |
| `CALCULATED-CARD-BATCH-136` | 通过 | `25` 个代表场景验证牌堆、历史、Power、Osty、能量、弃牌、抽牌、星能和格挡的分支内 CalculatedVar；目录强制全部 `43` 张相关卡牌有公式 | 2026-08-22 |
| `POWER-EFFECT-COMPLETION-135` | 通过 | 毒、灾厄、临时力量、撕裂、吸取、召唤、抽牌/生成触发、墨染、眩晕动态边界、毁灭和必死共 `13` 条差分通过 | 2026-08-22 |
| `CARD-GENERATION-SPEC-BATCH-138` | 通过 | `11` 类固定生成、复制、升级和随机牌堆插入效果的牌堆、牌面及 RNG 差分通过 | 2026-08-22 |
| `CARD-GENERATED-CHOICE-BATCH-121` | 通过 | 富足、发现、类星体和飞溅的生成三选一由求解器分支并自动驱动原生选择界面，`4/4` 完整差分通过 | 2026-08-22 |
| `RELIC-COMPLETION-133` | 通过 | 自成型黏土、破甲钻、螺旋飞镖、苦无、彩虹戒指、手里剑和红头骨组合触发 `6/6` 差分通过 | 2026-08-22 |
| `MECHA-VISIBLE-STEAM-103` | 通过 | 两轮 Steam 正常可见会话、完整用户 Mod 组合、`5s/60s` 与默认 `6 GB` No-GC；首轮 `7.80 s / 1.806 GB`，最终复核轮 `8.49 s / 1.818 GB`，GC 均为 `0 ms`，p95/p99 `16.7 ms`、最大帧间隔不超过 `23.6 ms`，无 `>33/50/100 ms` 帧；第 `6` 回合 `0` 药、预计掉血 `43`，第 `2-6` 回合精确复用。Reset 后托管堆约 `259 MB`、工作集约 `2.51-2.56 GB` | 2026-08-22 |
| `MECHA-FINAL-OPT-097` | 通过 | 固定机甲 `5s/60s`、统一 `12/30` Beam 保持 `1453` 展开、`13338` 转移、第 `6` 回合、`0` 药、预计掉血 `43` 和第 `2-6` 回合精确复用；headless 首轮 `7.02 s / 1.73 GB`、GC `0 ms`、最大帧间隔 `20.3 ms`，相对 `0.11.2` 的 `2.90 GB` 分配下降约 `40.2%` | 2026-08-22 |
| `LONGLINE-DIFF-OPT-101` | 通过 | 双小啃兽固定快照在验证模式对 `2712` 个增量转移同步执行完整前缀回放；状态文本、双指纹、边界、风险、死亡集合与 RNG 全部一致，最终第 `6` 回合 `0` 药、`0` 战损并逐回合复用 | 2026-08-22 |
| `NIBBITS-SNAPSHOT-RELEASE-096` | 通过 | 紧凑 History、稀疏回合末卡牌清理和历史 Simulator 释放后，双小啃兽仍为第 `6` 回合 `0/0`、跨两次洗牌并精确复用；普通搜索约 `1.83 s / 380 MB` | 2026-08-22 |
| `PERF-END-TURN-CLEANUP-FINAL-099` | 通过 | 子弹时间把未打出的打击费用降为 `0` 后执行完整玩家回合结束；模拟与原版均把牌移入弃牌堆并将打击恢复到 `1` 费，验证稀疏清理不会漏掉 EndOfTurn 费用修正 | 2026-08-22 |
| `PERF-DAMAGE-PIPELINE-100` | 通过 | 缩小甲虫两组真实/模拟伤害差分通过，覆盖力量、虚弱、易伤、格挡、回合末伤害与单目标无批量容器路径 | 2026-08-22 |
| `QUEEN-CHAINS-OPT-102` | 通过 | StateStore eager fork、ForkContext 及时释放和历史 Simulator 释放后，女王束缚锁链仍在第 `2/3` 回合逐字段一致，第 `3` 回合命中精确续用 | 2026-08-22 |
| `CORPSE-SLUGS-OPT-103` | 通过 | 紧凑 History、StateStore eager fork 与单目标伤害路径下恢复用户噬尸蛞蝓快照，全自动第 `4` 回合结束，无 pending Power 变化或 Fork 异常 | 2026-08-22 |
| `MECHA-VISIBLE-STEAM-086` | 通过 | Steam 正常可见会话、用户完整 Mod 组合、统一 `12/30` Beam 与默认 `6 GB` No-GC；首轮 `9.57 s / 2.90 GB` 求解线程分配、GC `0 ms`，最大帧间隔 `88.7 ms`、`>50 ms` 为 `1`、无 `>100 ms` 帧；第 `6` 回合 `0` 药、预计掉血 `43`，第 `2-6` 回合精确复用。必备工具第 `2-6` 回合均消费 `1` 个计划选择，每回合末胶囊完成态与部署完成日志齐全；战斗 Reset 后托管堆约 `372 MB`、工作集约 `423 MB` | 2026-08-22 |
| `NIBBITS-UNIFIED30-SOLD-CAP-084` | 通过 | 双小啃兽固定快照验证取消药水独立 Beam 后的统一 `30` 宽度与恢复后的卖血硬剪枝；首轮约 `2.25 s / 439 MB`，剪掉 `102` 条超卖血预算路线，第 `6` 回合 `0` 药、`0` 战损，第 `2-6` 回合精确复用 | 2026-08-22 |
| `QUEEN-CHAINS-REUSE-FINAL-085` | 通过 | 女王与火炬头场景强制女王首轮使用 `PUPPET_STRINGS_MOVE`；束缚锁链施加后第 `2/3` 回合均与首轮预测状态逐字段一致，第 `3` 回合命中 `SEARCH_REUSED`，夹具在命中目标续用后退出战斗 | 2026-08-22 |
| `CORPSE-SLUGS-USER-RUN-073` | 通过 | 从用户 `Y883BRPFJZ05` 跑局快照恢复噬尸蛞蝓战；同伴死亡后的 `RAVENOUS_POWER` 力量变化完成 Power 生命周期结算后再分叉，不再出现 `Cannot fork with pending Power amount changes`，全自动第 `6` 回合结束 | 2026-08-22 |
| `MECHA-VISIBLE-STEAM-071` | 通过 | 由 Steam `-applaunch 2868840` 启动正常可见游戏，加载用户完整 Mod 组合并恢复固定机甲快照；默认 `6 GB` No-GC 下首轮 `9.62 s / 2.32 GB` 求解线程分配、GC `0 ms`，最大帧间隔 `39.4 ms`、`>50/100 ms` 均为 `0`；第 `7` 回合 `0` 药、预计掉血 `40`，第 `2-7` 回合精确复用。战斗 Reset 后托管堆约 `359 MB`、工作集约 `2.78 GB`、私有提交约 `6.04 GB` | 2026-08-22 |
| `NIBBITS-FINAL-071` | 通过 | 默认 `22+7` Beam 下恢复双小啃兽固定快照；首轮约 `1.76 s / 389 MB`，第 `6` 回合 `0` 战损、`0` 药损，第 `2-6` 回合精确复用，最大帧间隔 `22.0 ms`、无 `>50 ms` 帧 | 2026-08-22 |
| `SETTINGS-NOGC-071` | 通过 | 隔离设置写入 `5.5 GB` No-GC 预算后，启动日志解析为 `5,500,000,000 B`，实际区域使用 `5.5 GB / 1.1 GB LOH`；首回合烟雾战斗正常结束，测试设置文件随后删除 | 2026-08-22 |
| `MECHA-FINAL-071` | 通过 | 固定 `MECHA_KNIGHT_ELITE` 跑局快照与 `5s/60s` 配置验证单会话 anytime Beam；首轮约 `11.7 s / 3.14 GB`，第 `9` 回合结束、`0` 药、预计掉血 `40`，第 `2-9` 回合精确续用；GC 约 `3.26 s`、单次低于 `30 ms`，headless 无 `>50 ms` 帧，战斗 Reset 后统一压缩 | 2026-08-22 |
| `SOLVER-ROUTE-HISTORY-071` | 通过 | 固定双小啃兽 `19` 张牌与 RNG 快照；历史固定计数器和单会话搜索保持第 `6` 回合 `0` 战损、`0` 药损、第 `2-6` 回合精确续用；首轮约 `2.04 s / 427 MB` | 2026-08-22 |
| `LONGLINE-DIFF-071` | 通过 | 双小啃兽固定快照对 `5748` 个增量转移同步执行完整前缀回放；状态文本、双指纹、边界、风险、牌堆与 RNG 全部一致，最终第 `6` 回合 `0/0` 并逐回合续用 | 2026-08-22 |
| `SMOKE-FINAL-071` | 通过 | `0.11.0` 最终 Release 部署后，铁甲战士在原版 headless 进程搜索并真实打出打击，首回合结束战斗 | 2026-08-22 |
| `MECHA-RF-SUSTAINED-071` | 通过 | 隔离 headless 同时加载官方 RF `0.13.8`、RitsuMetrics `0.1.37` 和 RitsuLib；`SustainedLowLatency` 下机甲首轮 `11.64 s / 3.15 GB`、GC `3.26 s/22.8 ms max`，无 `>50 ms` 帧，并保持第 `9` 回合、`0` 药、预计掉血 `40`。同栈 `Interactive` 对照出现一次 `142.5 ms` GC/`>100 ms` 帧 | 2026-08-22 |
| `MECHA-MEMORY-FULL-AUTO-FINAL-070` | 通过 | 从用户最新 `current_run.save` 提取牌组、遗物与 RNG，复现 `MECHA_KNIGHT_ELITE` 和 `5s/60s`：首轮 `16.61 s / 4.20 GB` 分配，GC 累计 `5.12 s`、单次最大 `30.5 ms`，主线程最大帧间隔 `43.9 ms` 且 `>50 ms` 为 `0`；第 `2-9` 回合精确续用并真实全自动结束。战斗 Reset 后压缩 `145.5 ms`，托管堆 `110.8 MB`、碎片 `0.16 MB`、工作集约 `2.04 GB` | 2026-08-21 |
| `GC-FREEPLAY-BULLET-TIME-070` | 通过 | 求解作用域隔离 Ritsu 免费出牌全局状态后，子弹时间、整手费用与原生结算差分继续一致 | 2026-08-21 |
| `GC-LONGLINE-DIFF-FINAL-070` | 通过 | 内存修复后长线增量/完整前缀回放逐字段一致，第 `6` 回合 `0/0`、第 `2-6` 回合精确续用；验证模式 GC 累计 `877 ms`、单次最大 `11.6 ms`、主线程最大帧间隔 `28.4 ms` | 2026-08-21 |
| `EMBEDDED-ENGINE-LONGLINE-DIFF-069` | 通过 | RF 本地版共同加载时，从只读快照恢复种子 `BJCZX3J13PZJ`；内置引擎对 `2540` 个实际增量转移逐一执行旧完整前缀回放，状态文本、双指纹、边界、风险和 RNG 全部一致；第 `6` 回合 `0` 战损、`0` 药损，第 `2-6` 回合精确复用 | 2026-08-21 |
| `EMBEDDED-NO-RF-TOOLS-TURN-START-069` | 通过 | 游戏目录已移除 RF；必备工具第 `2-5` 回合的抽 `1` 弃 `1` 全部由首轮 Beam 规划并自动提交、逐回合精确复用，跨边界后的计划外选择由守卫自动处理并重搜，最终第 `11` 回合结束且无玩家干预 | 2026-08-21 |
| `EMBEDDED-NO-RF-ENTROPY-FINAL-069` | 通过 | 游戏目录已移除 RF；熵按路线逐回合选择并变换手牌，真实 `CombatCardSelection` RNG 与预测一致，第 `2-6` 回合精确复用；跨边界后继续自动选择与重搜，最终第 `14` 回合结束 | 2026-08-21 |
| `DECOUPLED-HEADLESS-SMOKE-069` | 通过 | 独立 `APPDATA/LOCALAPPDATA`、Steam 关闭、临时 RitsuLib 投影的原版 `--headless` 进程加载 CombatSolver `0.10.0`，搜索并真实自动打出打击首回合结束战斗；进程退出后临时依赖目录被删除 | 2026-08-21 |
| `TOOLS-TURN-START-FINAL-068` | 通过 | 注入 `1` 层必备工具，真实全自动战斗每回合按搜索结果弃牌；第 `2-4` 回合逐字段精确续用，首轮未补偿项为 `0`，全程无玩家选牌 | 2026-08-21 |
| `ENTROPY-TURN-START-LIVE-068D` | 通过 | 注入 `1` 层熵，真实全自动战斗按路线逐回合选择并随机变换手牌；第 `2-4` 回合牌序、变换结果和 `CombatCardSelection` RNG 精确续用，首轮未补偿项为 `0` | 2026-08-21 |
| `UNPLANNED-TURN-CHOICE-GUARD-068` | 通过 | 注入未进入长线镜像的既定事项回合开始选牌；部署守卫从抽牌堆自动选择最高价值牌、清除旧续用并于第 `2` 回合重搜，随后继续全自动至战斗结束，无玩家选牌 | 2026-08-21 |
| `CARD-ON-PLAY-GAPS-068` | 通过 | 斗篷与匕首、闪躲翻滚连续实机/模拟差分；验证 `10` 格挡、`4` 层下回合格挡、手牌生成小刀及弃牌堆顺序 | 2026-08-21 |
| `CARD-CHOICE-TRANSFORM-FINAL-068` | 通过 | 熵接入通用原位置变换后重跑固定变换选牌实机/模拟差分，牌堆位置、牌状态和变换结果一致 | 2026-08-21 |
| `HUNTER-KILLER-TENDER-067` | 通过 | 猎人杀手战斗中给玩家注入 `1` 层 Tender 和 `8` 张零费小刀；增量分叉/完整回放一致，求解器规划并真实执行 `3` 张后首回合击杀，无 pending Power 队列或搜索失败 | 2026-08-21 |
| `RF-FORK-DIFF-067` | 通过 | Tender 历史补偿循环结算修复后，主长线完整增量差分继续通过；第 `6` 回合 `0` 战损、`0` 药损且第 `2-6` 回合精确续用 | 2026-08-21 |
| `SETTINGS-PERSISTENCE-066` | 通过 | 备份/恢复范围内写入自定义设置，跨进程加载 `1.25/7.5 s`、Beam `7/16`、节点/分支预算及 UI 坐标 `111,77`；搜索 `WEIGHTS` 使用自定义值，运行前后文件 SHA256 一致，测试文件随后删除 | 2026-08-21 |
| `SETTINGS-FINAL-066` | 通过 | 无配置文件时按默认值创建完整设置 UI 并完成搜索/自动出牌；无人测试的非持久暂停开关同步不会创建或误写用户设置文件 | 2026-08-21 |
| `RF-FORK-PERF-065-FINAL` | 通过 | 用户要求两轮最终独立进程样本通过后停止继续统计并定版；两轮均为第 `6` 回合 `0/0`、逐回合精确续用、Gen2 `0`，中点 `208,448,400 B / 1.550 s / 218.7 ms GC`，约 `198.8 MiB` | 2026-08-21 |
| `RF-FORK-DIFF-065-FINAL` | 通过 | 无行动上限与最终 COW/Hook 缓存版本在长线快照比较 `2540` 个实际增量转移和完整前缀回放；完整状态文本、双指纹、边界、风险、死亡集合和 RNG 一致，最终第 `6` 回合 `0` 战损、`0` 药损并逐回合续用 | 2026-08-21 |
| `TWO-STAGE-AGGRESSIVE-064` | 通过 | 测试态将短搜压到 `1 s` 触发深化，生产同款 `24+8` Beam 深化展开 `1219` 节点、命中 `670` 次转移缓存，在搜索空间耗尽时提前返回并将有战损路线改善为第 `6` 回合 `0/0`；默认预算仍为短搜 `3 s`、深化 `20 s` | 2026-08-21 |
| `UNBOUNDED-ACTIONS-065` | 通过 | 清空牌堆后注入 `8` 张零费小刀，求解器在同一回合规划并真实执行全部 `8` 个动作后击杀；证明原 `7` 次回合内行动上限已删除 | 2026-08-21 |
| `TWO-STAGE-UNAVOIDABLE-064` | 通过 | 固定不可避免伤害牌组触发深化；深化无严格改善时保留短结果，主动卖血仍为 `0` | 2026-08-21 |
| `RF-FORK-DIFF-061` | 通过 | 创意工坊 RF 已取消订阅，游戏只加载本地 API `1` / 上游 `598dce0` fork；长线固定快照对 `2541` 个候选同时执行增量分叉和完整前缀回放，状态文本、指纹、边界、风险和 RNG 全部一致，最终第 `6` 回合结束、`0` 战损、`0` 药损 | 2026-08-21 |
| `RF-FORK-PERF-061` | 通过 | 相同长线固定快照在三次干净游戏进程中均通过；性能中位数 `605,129,120 B / 2.394 s / 505.6 ms GC 暂停 / gc2=0`，相对旧基线分别降低约 `89.9% / 89.3% / 95.1%`，通过 `0.9 GB / 5.6 s / 暂停降低 80%` 门槛 | 2026-08-21 |
| `RF-FORK-REGRESSION-061` | 通过 | 本地 fork 最终 DLL 复跑三条卖血策略与瀑布巨兽：防御选择、不可避免伤害、稳定不卖血均保持原断言；瀑布巨兽严格第 `2` 回合结束；日志无 RF 错误、Fork 映射遗漏或搜索失败 | 2026-08-21 |
| `SOLVER-ROUTE-POLICY-060` | 通过 | 从只读快照恢复种子 `BJCZX3J13PZJ` 的完整 `19` 张牌、四件遗物和全部 RNG；首手 `7`、抽牌堆 `12`、敌人 `42/46` 与 `SLICE/HISS` 均与原局日志一致。首轮找到第 `6` 回合结束的 `0` 战损、`0` 药损路线，第 `2-6` 回合全部精确复用；同时验证生存者选牌先于来源牌进入弃牌堆。性能记录为 `2542 replays / 5.99 GB / 22.4 s`，不登记为性能通过 | 2026-08-21 |
| `SOLD-HP-POLICY-BATCH-059` | 通过 | 三份固定牌组验证稳健卖血策略：能直接击杀威胁时选择 `0/5` 而不故意卖 `4`，有防御选择时保持 `0/5` 并剪除超预算路线，无防御时实际掉血但卖血仍为 `0`；跨回合精确复用继续保留累计值 | 2026-08-21 |
| `RELIC-POWER-BATCH-058` | 通过 | 最终 Release DLL 在同一可见游戏 PID 完成两场差分：损毁头盔首次力量翻倍后正确消费状态；不安油灯使首张有效减益牌的全部减益翻倍，并跳过已翻倍临时 Power 的内部力量。另 `4` 个永久 `Deck` 遗物钩子完成全调用点静态审计；覆盖目录达到 `3035/3035`、未分析 `0` | 2026-08-21 |
| `RELIC-REACTIVE-BATCH-057` | 通过 | 最终 Release DLL 在同一真实可见游戏进程连续完成 `11` 个最终请求，关闭 `38` 个未分析遗物条目：`21` 项覆盖药水响应、格挡清空、手牌清空、星能、回合结束、充能球、空手抽牌、伤害倍率及三个动态边界，`17` 项完成源码、初始快照、召唤边界、药水重搜和纯表现静态闭环；覆盖未分析降至 `8` | 2026-08-21 |
| `RELIC-TURN-LIFECYCLE-BATCH-056` | 通过 | 最终 Release DLL 在两个真实可见游戏进程中完成 `8` 个最终请求、`15` 条完整状态差分，关闭 `24` 个未分析遗物条目并纠正 `4` 个 RF 风险/ignored 假精确条目；覆盖私有计数重置、攻击/技能/能力触发、金币与星能、跨回合能量、奥斯蒂、充能球、生成牌、格挡冷却及受伤上限，另 `2` 项完成动态边界与纯表现静态闭环；覆盖未分析降至 `46` | 2026-08-21 |
| `RELIC-TURN-START-BATCH-055` | 通过 | 最终 Release DLL 在同一真实可见游戏 PID 中完成 `5` 个最终请求、`6` 条完整状态差分，关闭 `26` 个未分析遗物条目并纠正孙子兵法 `1` 个 RF ignored 假精确条目：`25` 项覆盖首回合资源/Power/升级/伤害、攻击历史、第 `2/3` 回合能量、私有计数、充能球和精英房条件，`2` 项随机生成牌完成静态边界闭环；覆盖未分析降至 `70` | 2026-08-21 |
| `RELIC-HOOKS-BATCH-054` | 通过 | 最终 Release DLL 在同一真实可见游戏 PID 中连续完成 `7` 个请求、`8` 条完整状态差分，关闭 `20` 个遗物条目：`18` 项覆盖未来回合能量/手牌/格挡、X 值、Power 层数、费用、充能球、伤害、失血与仆从牌倍增，`2` 项完成源码与动态边界静态闭环；覆盖未分析降至 `96` | 2026-08-21 |
| `RELIC-DRAW-STATE-BATCH-053` | 通过 | 最终 Release DLL 在真实可见游戏中完成摆动球/花粉核心六回合周期、怀表 `4/0/3` 张阈值和四件首回合生成遗物快照，共 `10` 条完整状态差分；遗物私有计数另与跨回合复用文本逐回合比较；关闭 `15` 个未分析项并纠正 `1` 个 RF ignored 假精确项 | 2026-08-21 |
| `RELIC-PURE-HOOKS-BATCH-052` | 通过 | 最终 Release DLL 完成组合抽牌、组合最大能量、原生精英房轰鸣海螺及连续第 2/3 回合共 `4` 个最终请求；关闭 `20` 个遗物纯 Hook，定位并修复未来搜索仍读取实时回合号的问题；另 `11` 个范围外/纯表现钩子完成源码与构建静态闭环 | 2026-08-21 |
| `POWER-LIFECYCLE-BATCH-051` | 通过 | 最终 Release DLL 完成 `22` 个最终真实游戏请求，关闭最后 `31` 个未分析 Power 钩子并纠正 `6` 个 RF 忽略/空处理造成的假精确项；`31` 项实机闭环覆盖 Power 数值触发、资源、入场附魔、私有计数、回合末、唤醒/逃跑/选牌动态边界及凯撒巨蟹药水朝向，另 `6` 项仅完成源码与构建静态闭环 | 2026-08-20 |
| `POWER-DEATH-BATCH-050` | 通过 | 最终 Release DLL 在真实可见游戏中完成 `16` 个最终请求，关闭 `37` 个 Power 死亡、移除与清格挡条目：`33` 项实机闭环覆盖蟹之怒、自成型黏土、坚韧之环、饥饿及死亡后复活/召唤/换位等动态边界，`4` 项完成源码与构建静态闭环；开发期向错误宿主注入幻象和饥饿的两次失败保留为夹具审计证据，改用原生宿主后通过 | 2026-08-20 |
| `POWER-TURN-START-BATCH-049` | 通过 | 最终 Release DLL 在同一可见游戏进程连续完成 `19` 个请求，关闭 `22` 个回合开始/生成/随机边界/致死语义并复跑夜魇、绯红披风和野性；固定生成、奥斯蒂、充能球、私有计数、随机目标 RNG 与沙坑致死逐字段一致，七种随机生成/选牌效果均由正式搜索返回 `DynamicResolution`；另 `2` 个虚空形态瞬时时序完成源码与构建静态闭环 | 2026-08-20 |
| `POWER-END-TURN-BATCH-048` | 通过 | 同一可见游戏 PID 连续完成四场、`10` 项差分，关闭 `14` 个 Power 配对/回合末/死亡钩子；覆盖独白力量回收、湮灭施加灾厄、魔法炸弹伤害与施加者死亡，以及神气制胜和胆小的跨回合私有计数 | 2026-08-20 |
| `POWER-NATIVE-HOOKS-BATCH-047` | 通过 | 真实可见游戏对真实态与模拟态调用原生抽牌、最大能量、清格挡和清手牌纯钩子；组合 Power、实际打出友谊后的下一回合、第 031 批资源及手牌生命周期回归全部通过 | 2026-08-20 |
| `POWER-TRIGGER-BATCH-047` | 通过 | 实际打出绯红披风后推进完整下一玩家回合，验证 `1` 点自伤与 `7` 点格挡；另验证野性中途施加会继承本回合已有零费攻击历史 | 2026-08-20 |
| `ENCHANTMENTS-ORB-BATCH-046` | 通过 | 真实可见游戏完成 `13` 种附魔与等离子球的生产模拟/原生生命周期差分；覆盖附魔数值、启用状态、私有成长、重复次数、自动预出牌、清空手牌前降费和回合开始能量 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-046-EXACT` | 通过 | 真实可见游戏逐项强制并差分 `22` 个怪物行动；覆盖攻击、力量、虚弱、烟雾、偷取状态、沙坑、随机向抽牌堆/弃牌堆插牌和相关 RNG | 2026-08-20 |
| `MONSTER-DYNAMIC-BOUNDARY-BATCH-046` | 通过 | 真实可见游戏以召唤、后续 AI 私有状态和牌库改写三类代表运行正式后台搜索；三个行动均在敌方结算后、下一玩家回合建立前返回 `DynamicResolution` | 2026-08-20 |
| `POTION-ON-USE-BATCH-045` | 通过 | 真实可见游戏关闭剩余 `19` 个药水入口及再生生命周期：覆盖手牌/抽牌堆/弃牌堆选择、自动复活、最大生命、锻造、整副打击重复次数和动态生成后同回合重搜；最终狡诈药水由全自动原生使用，作废旧路线后同回合重搜并打出三张升级小刀结束战斗 | 2026-08-20 |
| `POTION-ON-USE-BATCH-044` | 通过 | 真实可见游戏完成 `30` 种确定性药水即时生产模拟/原生使用差分，另完成 `3` 条临时属性生命周期及 `2` 条无法获得能量交互差分；最终火焰药水由搜索选中、全自动通过原生队列使用、消耗槽位并在第 `1` 回合结束战斗 | 2026-08-20 |
| `CARD-ON-PLAY-BATCH-043` | 通过 | 同一可见游戏 PID `8404` 连续执行 `10` 个场景、`12` 条生产模拟/原生状态差分，关闭 `17` 个即时与跨回合卡牌条目；另以最终 DLL 的 PID `23060` 验证虚空形态实际被搜索、自动打出并强制推进至第 `2` 回合。覆盖奥斯蒂当前/最大生命、复活、X 费生成与 RNG、选牌、消耗堆连锁、实例 Power、出牌限制、回合结束/抽牌前/自动预出牌阶段和升级分支；另静态关闭 `12` 项 | 2026-08-20 |
| `CARD-ON-PLAY-BATCH-042` | 通过 | 同一可见游戏 PID `46700` 连续执行 `21` 个最终场景，关闭 `24` 个确定性 `OnPlay`；验证随机目标/插牌、击杀递归、毒触发、多敌伤害、私有计数，以及选牌、可选空选择、跨牌堆移动、变形、复制、局部费用、重复次数和 `6` 组战斗 RNG | 2026-08-20 |
| `CARD-ON-PLAY-BATCH-041` | 通过 | 同一可见游戏 PID `39540` 连续执行 `6` 个场景、`7` 条最终差分，关闭 `24` 个确定性 `OnPlay`；验证 Power、能量/星能、临时集中及回收、Orb 种类计数、跨牌堆君王之剑、锻造、虚无、小刀、局部费用和整手弃牌；另静态排除 `3` 个多人专属条目 | 2026-08-20 |
| `CARD-ON-PLAY-BATCH-040` | 通过 | 同一可见游戏 PID `31288` 连续执行 `6` 组最终差分，关闭 `28` 个确定性 `OnPlay`；验证 Power、能量/星能、按当前格挡延迟获得格挡、目标中毒/湮灭、追踪之刃生成并锻造君王之剑；另静态排除 `3` 个多人专属条目 | 2026-08-20 |
| `CARD-ON-PLAY-BATCH-039` | 通过 | 两个可见游戏进程共执行 `13` 组差分，关闭 `23` 个确定性 `OnPlay`；验证临时力量/集中力、自伤资源顺序、多层人工制品移除、双敌全体减益、小刀生成、墨染附魔、全牌堆升级、普通/升级费用持续时间和整手弃牌替换；另静态排除 `3` 个多人专属条目 | 2026-08-20 |
| `CARD-ON-PLAY-BATCH-038` | 通过 | 同一可见游戏 PID `43080` 连续执行 `7` 组差分，关闭 `25` 个确定性 `OnPlay`；验证 Power、治疗、能量/星能、疯狂进食的临时力量双 Power，以及无处可逃按已有灾厄分段计算；另静态排除 `11` 个多人专属条目 | 2026-08-20 |
| `CARD-ON-PLAY-BATCH-037` | 通过 | 同一可见游戏 PID `39500` 连续执行 `6` 组差分，关闭 `26` 个确定性 `OnPlay`；验证 Power、能量/星能、条件中毒、锻造、子弹时间零费化，以及野性/杂耍中途施加时继承已有攻击计数；另静态排除 `1` 个多人专属条目 | 2026-08-20 |
| `CARD-ON-PLAY-BATCH-036` | 通过 | 同一可见游戏 PID 连续执行 `3` 组差分，关闭 `19` 个确定性 `OnPlay`；验证 Power、余像后续格挡、扩容槽位 `3→5`，以及预判临时敏捷的玩家回合末回收；另静态排除 `1` 个多人专属条目 | 2026-08-20 |
| `CARD-ON-PLAY-BATCH-035` | 通过 | 同一可见游戏 PID 连续执行 `5` 组差分，关闭 `20` 个 RF 未镜像的卡牌 `OnPlay`；验证 Power 顺序、X 费用、星能、充能球槽位、锻造与君王之剑伤害、下回合能量及尖啸回合末恢复 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-034` | 通过 | `13` 组可见游戏差分关闭 `22` 个单人 Power 钩子；验证伤害/格挡/费用/牌去向、首次攻击/小刀/格挡预测计数，以及为你而死的单段承伤、`8` 点溢出、多段中途死亡、死亡保留、不可选中和 Power 保留 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-033` | 通过 | 最终 DLL 共执行 `16` 条生产模拟/原生回调差分；覆盖 `20` 个单人 Power 钩子，包括触发上毒、伤害修正、自伤、生命周期、Orb 唤起和私有计数；巨像奇数伤害行动前移除的首次偏差已修复并复测 | 2026-08-20 |
| `SMOKE-002` | 通过 | 铁甲战士进入 `FUZZY_WURM_CRAWLER_WEAK`；敌人 `1 HP`；向手牌注入 `STRIKE_IRONCLAD`；全自动实际出牌并结束战斗；胜利进度写入被隔离 | 2026-08-20 |
| `MONSTER-WATERFALL-001` | 通过 | `0.7.0` 最终 Release：`WATERFALL_GIANT_BOSS` 为 `1 HP` 且拥有 `SteamEruptionPower:10`；提前击杀后依次进入蓄爆与爆炸，严格在第 `2` 回合结束；首轮 `116 replays / 74.65MB`，第二回合精确复用 `0 replays / 0 bytes / 0ms` | 2026-08-21 |
| `MONSTER-AXEBOT-HAMMER-001` | 通过 | 在 `AxebotsNormal` 强制执行 `HAMMER_UPPERCUT_MOVE`；生产预测与真实 `PerformMove` 逐字段比较；确认 `14` 点伤害、`2` 层虚弱、`2` 层脆弱完全一致 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-004` | 通过 | 单次进入 `LivingFogNormal`，按需召唤青蛙骑士、电球头和气态炸弹；连续完成 `7` 个生产预测与真实 `PerformMove` 差分；全部逐字段一致 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-005` | 通过 | 单次进入 `LivingFogNormal`，召唤幽灵船和猎人杀手；连续完成 `6` 个行动差分，并新增四个战斗牌堆的卡牌计数比较；纠缠的 `5` 张暈眩与真实弃牌堆一致 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-006` | 通过 | 单次进入 `LivingFogNormal`，召唤守护机器人、感染棱柱、墨宝和环境组装师；连续完成 `8` 项差分，并按模型比较全场敌人格挡；首次缺少组装师的夹具失败已保留 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-007` | 通过 | 单次进入 `LivingFogNormal`，召唤同族信徒、同族神官和知识恶魔；连续完成 `10` 项差分；思考额外验证攻击后治疗 `30` 及力量增加 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-008` | 通过 | 单次进入 `LivingFogNormal`，召唤乐加维林族母和两种树叶史莱姆；连续完成 `9` 项差分；验证负数力量/敏捷、族母格挡和弃牌堆黏液 `0 → 2 → 3` | 2026-08-20 |
| `MONSTER-MOVES-BATCH-009` | 通过 | 单次进入 `LivingFogNormal`，召唤活体盾、蛮兽、异螨、小啃兽和啃咬机；连续完成 `13` 项差分；验证多段攻击、格挡、力量、易伤及手牌/弃牌堆状态牌 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-010` | 通过 | 单次进入 `LivingFogNormal`，在五个空槽位召唤五类怪物；连续完成 `16` 项差分；验证攻击、格挡、力量、脆弱、手牌灼傷和条件初始状态机 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-011` | 通过 | 单次进入 `LivingFogNormal`，在五个空槽位召唤五类怪物；连续完成 `13` 项差分；修复并验证扭动同时生成感染与力量 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-012` | 通过 | 单次进入 `LivingFogNormal`，在五个空槽位召唤五类怪物；同一场战斗连续完成 `11` 项差分；覆盖攻击、虚弱、脆弱、易伤和正负力量 | 2026-08-20 |
| `UNATTENDED-PROCESS-REUSE-001` | 通过 | 同一 PID `35048` 先执行第 012 批 `11` 项差分，再从主菜单接收巨斧机器人差分；日志依次为 `process_sequence=1/2`，第二批 `reused_process=True`，最后一批才退出 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-013` | 通过 | 单次进入 `LivingFogNormal`，召唤五类怪物并连续完成 `9` 项差分；验证攻击、格挡、仪式、易伤、力量累计和弃牌堆晕眩 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-014` | 通过 | 单次进入 `LivingFogNormal`，召唤五类怪物并连续完成 `10` 项差分；验证多段攻击、格挡、力量、脆弱及荆棘的增加和移除 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-015` | 通过 | 单次进入 `LivingFogNormal`，召唤五类怪物并连续完成 `16` 项差分；验证多段攻击、力量、十张黏液覆体及虚弱/易伤累计 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-016` | 通过 | 单次进入 `LivingFogNormal`，召唤五类怪物并连续完成 `13` 项差分；验证隐藏醒来行动、攻击段数、一张黏液覆体和装弹力量 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-017` | 通过 | 单次进入 `LivingFogNormal`，召唤五类怪物并连续完成 `14` 项差分；验证三代对手的弹幕力量、蟾蜍蝌蚪荆棘增减和藤蔓蹒跚者攻击 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-018` | 通过 | 单次进入 `LivingFogNormal`，召唤五类怪物并连续完成 `15` 项差分；验证入场力量后的攻击、力量累计、格挡以及弃牌堆感染和伤口 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-019` | 通过 | 单次进入 `LivingFogNormal`，先召唤女王依赖怪物，再连续完成 `14` 项差分；新增全场敌方 Power 比较，验证动态敏捷伤害、状态移除和女王群体增益 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-020` | 通过 | 单次进入 `LivingFogNormal`，四类怪物连续完成 `13` 项差分；验证累计力量、格挡、脆弱、埋地，并审计五个实例布尔字段只影响表现 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-021` | 通过 | 同一 PID 先在原生凯撒蟹 Boss 战完成双臂 `10` 项差分，再复用进程进入 `LivingFogNormal` 完成追踪手/噪音机器人 `3` 项；最后才退出游戏 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-022` | 通过 | 单次进入 `LivingFogNormal`，同一只灵魂异鱼按固定顺序完成 `5` 项差分；验证抽牌堆/弃牌堆“呼喚”累计、无实体和易伤 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-023` | 通过 | 单次进入原生 `BowlbugsWeak`，同一只盛碗虫（石）连续验证完全格挡触发 `STUNNED`、昏头转向后恢复头槌、部分格挡不触发 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-024` | 通过 | 单次进入 `LivingFogNormal` 完成 `6` 项连续差分；验证骇鳗活力获得/下一击消费，以及胧光怪全队力量进入三只怪物的后续伤害 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-026` | 通过 | 单次进入 `LivingFogNormal`，同一只永世沙漏连续两次执行“加大力度”；验证力量 `3 → 7`、弃牌堆凋萎 `1 → 2`、伤害总和 `6 → 18`，以及模拟计数器与生成牌等级一致 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-027` | 通过 | 单次进入 `LivingFogNormal`，同一只蛇行扼杀者连续两次执行“缠身”；验证 `3` 层紧缠在玩家回合结束造成 `3 HP`，再次施加后随施加者死亡完整移除 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-028` | 通过 | 同一可见游戏 PID 连续执行两场：第一场两只幽灵骑士验证现有牌/新牌受咒、后施加者死亡不清除、初始施加者死亡才清除；第二场复用进程验证完整回合结束时受咒手牌因虚无进入消耗堆 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-029` | 通过 | 同一 PID `8240` 连续执行五场、共 `9` 条差分；验证缩小/人工制品、缠结附魔与费用、昏眩每回合首张牌限制、无实体伤害上限及 `2 → 1 → 0` 生命周期 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-030` | 通过 | 同一 PID `13628` 连续执行五场、共 `11` 条差分；验证力量/虚弱/易伤伤害、敏捷/脆弱/不可格挡格挡、中毒与催化剂、残影/覆甲生命周期、双倍伤害及缓慢累计清零 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-031` | 通过 | 同一 PID `42532` 连续执行三场、共 `11` 条差分；关闭 `22` 个目录条目，验证下回合能量/抽牌/格挡、禁止抽牌/回能、费用/伤害/格挡修正、保留手牌时序和一次性 Power 生命周期 | 2026-08-20 |
| `MONSTER-MOVES-BATCH-032` | 通过 | 同一 PID `41596` 连续执行六场；关闭 `22` 个持续 Power 生命周期条目，验证抽牌、能量、辉星、Orb、全场目标、生成牌、仪式延迟，以及愤怒复制与活力消费回归 | 2026-08-20 |

运行命令：

```powershell
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CHOICES-PARADOX-SCROLLS-0160 -CharacterId SILENT -Seed YS41WKT7ZUXS -RunSnapshotPath '.local\issue-bundles\scrolls-of-biting-20260828-231054-924\raw\combat-solver\forensics\current\pre-combat\in-memory-current_run.save' -ProgressSnapshotPath '.local\issue-bundles\scrolls-of-biting-20260828-231054-924\raw\combat-solver\forensics\current\pre-combat\progress.save' -EncounterId SCROLLS_OF_BITING_NORMAL -Ascension 10 -ActIndexForTest 2 -InitialPlayerHp 60 -InitialEnemyCurrentHpsJson '[33,37,38,36]' -InitialEnemyMoveIdsJson '["CHEW","MORE_TEETH","CHOMP","MORE_TEETH"]' -ReloadRunRngAfterStateInjection -ForceShortSearchOnly -ShortSearchBudgetOverrideMilliseconds 8000 -ExpectedInitialSetupChoiceCountAtLeast 1 -ExpectedInitialSetupChoiceSourceId CHOICES_PARADOX -ExpectedInitialSetupChoiceTextStartsWith '选择悖论：选择 ' -ExpectedInitialChoiceBranchesEvaluatedAtLeast 5 -StopAfterInitialSetupAssertion -TimeoutSeconds 150 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RINGING-HAVOC-AUTOPLAY-0160-FINAL -CharacterId IRONCLAD -EncounterId LivingFogNormal -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\ringing-havoc-autoplay-0160.json -VerifyIncrementalSearch -ExitOnComplete -TimeoutSeconds 120
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId HEADBUTT-EMPTY-DISCARD-0160 -CharacterId IRONCLAD -EncounterId FUZZY_WURM_CRAWLER_WEAK -EnemyCurrentHp 50 -ClearPlayerPiles -CardsPath coverage\unattended\headbutt-empty-discard-0160-cards.json -ExpectedPlayedCardId HEADBUTT -ExpectedReusedTurn 2 -StopAfterExpectedReuse -ExpectedUnexpectedReplansAtMost 0 -DeploymentFastModeForTest Instant -DeploymentInterActionDelaySecondsForTest 0 -TimeoutSeconds 120
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId COSMIC-INDIFFERENCE-EMPTY-DISCARD-0160 -CharacterId REGENT -EncounterId FUZZY_WURM_CRAWLER_WEAK -EnemyCurrentHp 50 -ClearPlayerPiles -CardsPath coverage\unattended\cosmic-indifference-empty-discard-0160-cards.json -ExpectedPlayedCardId COSMIC_INDIFFERENCE -ExpectedReusedTurn 2 -StopAfterExpectedReuse -ExpectedUnexpectedReplansAtMost 0 -DeploymentFastModeForTest Instant -DeploymentInterActionDelaySecondsForTest 0 -ExitOnComplete -TimeoutSeconds 120
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId FAIRY-AUTOMATIC-RESCUE-FINAL2-0150 -CharacterId IRONCLAD -EncounterId FUZZY_WURM_CRAWLER_WEAK -EnemyCurrentHp 57 -InitialPlayerHp 1 -PotionId FairyInABottle -ClearPlayerPiles -CardsPath coverage\unattended\fairy-automatic-rescue-0150-cards.json -InitialEnemyMoveIdsJson '["FIRST_ACID_GOOP"]' -ExpectedInitialOnlyDeathRoutesFound 0 -ExpectedInitialCombatEndedTurn 2 -ExpectedInitialPotionCount 1 -ExpectedUsedPotionId FAIRY_IN_A_BOTTLE -ExpectedFinishedTurn 2 -ExpectedUnexpectedReplansAtMost 0 -VerifyIncrementalSearch -DeploymentFastModeForTest Instant -DeploymentInterActionDelaySecondsForTest 0 -TimeoutSeconds 180 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId INSATIABLE-MALAISE-CONTROL-150 -CharacterId SILENT -Seed 2DJ8M7EAKQUS -EncounterId THE_INSATIABLE_BOSS -EnemyCurrentHp 341 -InitialPlayerHp 24 -InitialPlayerMaxHp 57 -InitialPlayerEnergy 4 -InitialEnemyMoveIdsJson '["LIQUIFY_GROUND_MOVE"]' -ClearPlayerPiles -CardsPath coverage\unattended\insatiable-malaise-control-150-cards.json -PerformancePresetForTest High -ForceShortSearchOnly -ExpectedInitialFirstActionCardId MALAISE -StopAfterInitialSolverResultAssertion -TimeoutSeconds 180 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId BUILTIN-LISTENER-IDENTITY-533 -CharacterId SILENT -Seed BJCZX3J13PZJ -RunSnapshotPath coverage\unattended\solver-longline-run-snapshot.json -EncounterId NIBBITS_NORMAL -EnemyCurrentHp 999 -InitialPlayerHp 35 -PotionId WeakPotion -ExpectedInitialPotionCount 0 -ExpectedInitialHpLostAtMost 0 -ExpectedInitialProjectedBattleHpLostAtMost 0 -ExpectedInitialShufflesCrossedAtLeast 2 -ExpectedUnexpectedReplansAtMost 0 -ExpectedFinishedTurn 5 -VerifyCombatRootSnapshot -VerifyIncrementalSearch -DeploymentFastModeForTest Instant -DeploymentInterActionDelaySecondsForTest 0 -TimeoutSeconds 300 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MODIFIER-IDENTITY-532 -ModifierId MURDEROUS -MonsterMoveChecksPath coverage\unattended\murderous-fabricator-spawn-532.json -ExitOnComplete
pwsh -NoProfile -File tools\run-visible-steam-benchmark.ps1 -TimeoutSeconds 360
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId LAGAVULIN-SLEEP-REUSE-176 -CharacterId REGENT -EncounterId LAGAVULIN_MATRIARCH_BOSS -EnemyCurrentHp 233 -ClearPlayerPiles -CardsJson '[{"cardId":"DEFEND_REGENT","pile":"Hand","count":1}]' -InitialEnemyMoveIdsJson '["SLEEP_MOVE"]' -ExpectedReusedTurn 2 -StopAfterExpectedReuse -TimeoutSeconds 120 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId BEAT-INTO-SHAPE-PLAYABLE-175 -CharacterId REGENT -EncounterId FUZZY_WURM_CRAWLER_WEAK -EnemyCurrentHp 1 -ClearPlayerPiles -CardsJson '[{"cardId":"BEAT_INTO_SHAPE","pile":"Hand","count":1}]' -ExpectedPlayedCardId BEAT_INTO_SHAPE -ExpectedFinishedTurn 1 -TimeoutSeconds 120 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId PERFORMANCE-PRESET-LOW-171 -CharacterId IRONCLAD -EncounterId FUZZY_WURM_CRAWLER_WEAK -EnemyCurrentHp 1 -PerformancePresetForTest Low -ExpectedFinishedTurn 1 -TimeoutSeconds 120 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId PERFORMANCE-PRESET-HIGH-172 -CharacterId IRONCLAD -EncounterId FUZZY_WURM_CRAWLER_WEAK -EnemyCurrentHp 1 -PerformancePresetForTest High -ExpectedFinishedTurn 1 -TimeoutSeconds 120 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId PERFORMANCE-PRESET-CUSTOM-173 -CharacterId IRONCLAD -EncounterId FUZZY_WURM_CRAWLER_WEAK -EnemyCurrentHp 1 -PerformancePresetForTest Custom -ExpectedFinishedTurn 1 -TimeoutSeconds 120 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId INFESTED-PRISM-VITAL-SPARK-152 -CharacterId REGENT -EncounterId INFESTED_PRISMS_ELITE -EnemyCurrentHp 171 -MonsterMoveChecksPath coverage\unattended\infested-prism-vital-spark-152.json -TimeoutSeconds 180 -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId KNOWLEDGE-DEMON-SEARCH-CHOICE-162 -CharacterId REGENT -EncounterId KNOWLEDGE_DEMON_BOSS -EnemyCurrentHp 399 -ExpectedInitialChoiceBranchesEvaluatedAtLeast 2 -ExpectedInitialPlannedChoiceCardId MIND_ROT -ExpectedInitialActEndingBoss 1 -ExpectedObservedPlayerPowerId MIND_ROT_POWER -StopAfterExpectedPlayerPower -TimeoutSeconds 120 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId DEATH-TURN-PAUSE-165 -CharacterId IRONCLAD -EncounterId FUZZY_WURM_CRAWLER_WEAK -EnemyCurrentHp 57 -InitialPlayerHp 1 -ClearPlayerPiles -CardsJson '[]' -InitialEnemyMoveIdsJson '["FIRST_ACID_GOOP"]' -ExpectedInitialOnlyDeathRoutesFound 1 -ExpectedInitialDeathTurn 1 -ExpectedFullAutoPausedAtDeathTurn -TimeoutSeconds 120 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId SCULPTING-STRIKE-CHOICE-151 -CharacterId NECROBINDER -EncounterId FUZZY_WURM_CRAWLER_WEAK -EnemyCurrentHp 50 -ClearPlayerPiles -CardsPath coverage\unattended\sculpting-strike-choice-151-cards.json -ExpectedPlayedCardId SCULPTING_STRIKE -ExpectedReusedTurn 2 -StopAfterExpectedReuse -TimeoutSeconds 120 -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId COSMIC-INDIFFERENCE-IMPLICIT-151B -CharacterId REGENT -EncounterId FUZZY_WURM_CRAWLER_WEAK -EnemyCurrentHp 50 -ClearPlayerPiles -CardsPath coverage\unattended\cosmic-indifference-choice-151b-cards.json -ExpectedPlayedCardId COSMIC_INDIFFERENCE -ExpectedReusedTurn 2 -StopAfterExpectedReuse -TimeoutSeconds 120 -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId DUPLICATE-POTION-SEARCH-147 -CharacterId REGENT -EncounterId QUEEN_BOSS -EnemyCurrentHp 1 -PotionsPath coverage\unattended\duplicate-potions-147.json -ExpectedInitialExecutableActionCountAtLeast 1 -ExpectedFinishedTurnAtMost 5 -TimeoutSeconds 180 -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId PEN-NIB-ROUTE-148 -CharacterId IRONCLAD -EncounterId MECHA_KNIGHT_ELITE -EnemyCurrentHp 70 -ClearPlayerPiles -CardsPath coverage\unattended\pen-nib-route-148-cards.json -RelicsPath coverage\unattended\pen-nib-route-148-relics.json -VerifyIncrementalSearch -ExpectedInitialExecutableActionCountAtLeast 2 -ExpectedInitialRelicEffectId PEN_NIB -ExpectedInitialRelicEffectSummary '×2' -ExpectedInitialHpLostAtMost 0 -ExpectedInitialProjectedBattleHpLostAtMost 0 -ExpectedInitialOnlyDeathRoutesFound 0 -ExpectedInitialCombatEndedTurn 1 -ExpectedFinishedTurn 1 -DeploymentFastModeForTest Instant -DeploymentInterActionDelaySecondsForTest 0.05 -AssertDeploymentSpeedRestored -TimeoutSeconds 180 -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CENTENNIAL-PUZZLE-STATE-149 -CharacterId IRONCLAD -EncounterId FUZZY_WURM_CRAWLER_WEAK -EnemyCurrentHp 57 -CombatRelicsPath coverage\unattended\centennial-puzzle-state-149-relics.json -MonsterMoveChecksPath coverage\unattended\centennial-puzzle-state-149.json -TimeoutSeconds 180 -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId JOSS-PAPER-STATE-150 -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\joss-paper-state-150-relics.json -MonsterMoveChecksPath coverage\unattended\joss-paper-state-150.json -TimeoutSeconds 180 -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId ONLY-DEATH-ROUTES-150 -CharacterId IRONCLAD -EncounterId FUZZY_WURM_CRAWLER_WEAK -EnemyCurrentHp 57 -InitialPlayerHp 1 -ClearPlayerPiles -CardsJson '[]' -InitialEnemyMoveIdsJson '["FIRST_ACID_GOOP"]' -ExpectedInitialOnlyDeathRoutesFound 1 -ExpectedPlayerDeath -ExpectedFinishedTurn 1 -TimeoutSeconds 120 -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId PERF-END-TURN-CLEANUP-FINAL-099 -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\performance-end-turn-cleanup-098.json -TimeoutSeconds 180 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId PERF-DAMAGE-PIPELINE-100 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-030-damage.json -TimeoutSeconds 240 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId NIBBITS-UNIFIED30-SOLD-CAP-084 -CharacterId SILENT -Seed BJCZX3J13PZJ -RunSnapshotPath coverage\unattended\solver-longline-run-snapshot.json -EncounterId NIBBITS_NORMAL -EnemyCurrentHp 999 -InitialPlayerHp 35 -PotionId WeakPotion -ExpectedInitialPotionCount 0 -ExpectedInitialProjectedBattleHpLostAtMost 0 -ExpectedInitialSoldHp 0 -ExpectedInitialSoldHpBranchesPrunedAtLeast 1 -ExpectedReusedTurn 3 -ExpectedFinishedTurnAtMost 8 -TimeoutSeconds 300 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId QUEEN-CHAINS-REUSE-FINAL-085 -CharacterId IRONCLAD -Seed Y883BRPFJZ05 -RunSnapshotPath "$env:APPDATA\SlayTheSpire2\steam\76561198950654908\modded\profile3\saves\current_run.save" -EncounterId QUEEN_BOSS -EnemyCurrentHp 70 -InitialPlayerHp 80 -InitialEnemyMoveIdsJson '["","PUPPET_STRINGS_MOVE"]' -ExpectedReusedTurn 3 -StopAfterExpectedReuse -TimeoutSeconds 300 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CORPSE-SLUGS-USER-RUN-073 -CharacterId IRONCLAD -Seed Y883BRPFJZ05 -RunSnapshotPath "$env:APPDATA\SlayTheSpire2\steam\76561198950654908\modded\profile3\saves\current_run.save" -EncounterId CORPSE_SLUGS_WEAK -EnemyCurrentHp 999 -InitialPlayerHp 80 -ExpectedFinishedTurnAtMost 20 -TimeoutSeconds 300 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MECHA-MEMORY-FULL-AUTO-FINAL-070 -CharacterId SILENT -Seed BJCZX3J13PZJ -RunSnapshotPath coverage\unattended\mecha-knight-memory-run-snapshot.json -EncounterId MECHA_KNIGHT_ELITE -EnemyCurrentHp 300 -InitialPlayerHp 65 -ShortSearchBudgetOverrideMilliseconds 5000 -DeepSearchBudgetOverrideMilliseconds 60000 -ExpectedInitialSearchPhase Deep -ExpectedInitialDeepSearchTriggered 1 -ExpectedInitialTotalElapsedMillisecondsAtMost 25000 -ExpectedInitialTotalAllocatedBytesAtMost 4300000000 -ExpectedInitialGen2CollectionsAtMost 6 -ExpectedInitialTotalGcPauseMillisecondsAtMost 8000 -ExpectedInitialMaxGcPauseMillisecondsAtMost 50 -ExpectedInitialMaxMainThreadFrameGapMillisecondsAtMost 100 -ExpectedReusedTurn 3 -ExpectedFinishedTurn 9 -MeasureSearchPhases -TimeoutSeconds 300 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId SOLVER-ROUTE-POLICY-060 -CharacterId SILENT -Seed BJCZX3J13PZJ -RunSnapshotPath coverage\unattended\solver-longline-run-snapshot.json -EncounterId NIBBITS_NORMAL -EnemyCurrentHp 999 -InitialPlayerHp 35 -PotionId WeakPotion -ExpectedInitialPotionCount 0 -ExpectedInitialProjectedBattleHpLostAtMost 0 -ExpectedReusedTurn 3 -TimeoutSeconds 300 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId SOLD-HP-POLICY-BATCH-059-DEFENSE-CHOICE -CharacterId IRONCLAD -EncounterId NIBBITS_WEAK -EnemyCurrentHp 43 -CardsPath coverage\unattended\sold-hp-policy-batch-059-defense-choice.json -ClearPlayerPiles -ExpectedInitialSoldHpAtMost 5 -ExpectedInitialSoldHpBranchesPrunedAtLeast 1 -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId SOLD-HP-POLICY-BATCH-059-UNAVOIDABLE -CharacterId IRONCLAD -EncounterId NIBBITS_WEAK -EnemyCurrentHp 43 -CardsPath coverage\unattended\sold-hp-policy-batch-059-unavoidable.json -ClearPlayerPiles -ExpectedInitialSoldHp 0 -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId SOLD-HP-POLICY-BATCH-059-STABLE-NO-SALE -CharacterId IRONCLAD -EncounterId SLIMES_WEAK -EnemyCurrentHp 999 -CardsPath coverage\unattended\sold-hp-policy-batch-059-active-sale.json -ClearPlayerPiles -ExpectedInitialSoldHp 0 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-POWER-BATCH-058 -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-power-batch-058-relics.json -MonsterMoveChecksPath coverage\unattended\relic-power-batch-058.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-POWER-TEMPORARY-BATCH-058 -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-power-batch-058-temporary-relics.json -MonsterMoveChecksPath coverage\unattended\relic-power-batch-058-temporary.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-REACTIVE-BATCH-057-POTION-FINAL -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-reactive-batch-057-potion-relics.json -PotionCheckPath coverage\unattended\relic-reactive-batch-057-potion.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-REACTIVE-BATCH-057-TURNS-FINAL -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-reactive-batch-057-turns-relics.json -MonsterMoveChecksPath coverage\unattended\relic-reactive-batch-057-turns.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-REACTIVE-BATCH-057-TURN-END-FINAL -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-reactive-batch-057-turn-end-relics.json -MonsterMoveChecksPath coverage\unattended\relic-reactive-batch-057-turn-end.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-REACTIVE-BATCH-057-KUSARIGAMA-FINAL -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-reactive-batch-057-kusarigama-relics.json -MonsterMoveChecksPath coverage\unattended\relic-reactive-batch-057-kusarigama.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-REACTIVE-BATCH-057-STARS-FINAL -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-reactive-batch-057-stars-relics.json -MonsterMoveChecksPath coverage\unattended\relic-reactive-batch-057-stars.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-REACTIVE-BATCH-057-EMOTION-FINAL -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-reactive-batch-057-emotion-relics.json -MonsterMoveChecksPath coverage\unattended\relic-reactive-batch-057-emotion.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-REACTIVE-BATCH-057-TOP-FINAL -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-reactive-batch-057-top-relics.json -MonsterMoveChecksPath coverage\unattended\relic-reactive-batch-057-top.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-REACTIVE-BATCH-057-UNDYING-FINAL -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-reactive-batch-057-undying-relics.json -MonsterMoveChecksPath coverage\unattended\relic-reactive-batch-057-undying.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-REACTIVE-BATCH-057-PAELS-EYE-FINAL -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-reactive-batch-057-paels-eye-relics.json -MonsterMoveChecksPath coverage\unattended\relic-reactive-batch-057-boundary.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-REACTIVE-BATCH-057-HISTORY-FINAL -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-reactive-batch-057-history-course-relics.json -MonsterMoveChecksPath coverage\unattended\relic-reactive-batch-057-boundary.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-REACTIVE-BATCH-057-TOASTY-FINAL -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-reactive-batch-057-toasty-mittens-relics.json -MonsterMoveChecksPath coverage\unattended\relic-reactive-batch-057-boundary.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TURN-LIFECYCLE-DETERMINISTIC-056 -CharacterId DEFECT -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-turn-lifecycle-batch-056-deterministic-relics.json -MonsterMoveChecksPath coverage\unattended\relic-turn-lifecycle-batch-056-deterministic-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TURN-LIFECYCLE-OSTY-056 -CharacterId NECROBINDER -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-turn-lifecycle-batch-056-osty-relics.json -MonsterMoveChecksPath coverage\unattended\relic-turn-lifecycle-batch-056-osty-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TURN-LIFECYCLE-CYCLES-056 -CharacterId REGENT -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-turn-lifecycle-batch-056-cycles-relics.json -MonsterMoveChecksPath coverage\unattended\relic-turn-lifecycle-batch-056-cycles-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TURN-LIFECYCLE-ATTACKS-056 -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-turn-lifecycle-batch-056-attacks-relics.json -MonsterMoveChecksPath coverage\unattended\relic-turn-lifecycle-batch-056-attacks-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TURN-LIFECYCLE-LETTER-056 -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-turn-lifecycle-batch-056-letter-relics.json -MonsterMoveChecksPath coverage\unattended\relic-turn-lifecycle-batch-056-letter-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TURN-LIFECYCLE-LEGION-056 -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-turn-lifecycle-batch-056-legion-relics.json -MonsterMoveChecksPath coverage\unattended\relic-turn-lifecycle-batch-056-legion-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TURN-LIFECYCLE-GENERATION-056 -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-turn-lifecycle-batch-056-generation-relics.json -MonsterMoveChecksPath coverage\unattended\relic-turn-lifecycle-batch-056-generation-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TURN-LIFECYCLE-DAMAGE-056 -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-turn-lifecycle-batch-056-damage-relics.json -MonsterMoveChecksPath coverage\unattended\relic-turn-lifecycle-batch-056-damage-checks.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TURN-START-FIRST-055 -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-turn-start-batch-055-first-relics.json -MonsterMoveChecksPath coverage\unattended\relic-turn-start-batch-055-first-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TURN-START-CYCLES-055 -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-turn-start-batch-055-cycles-relics.json -MonsterMoveChecksPath coverage\unattended\relic-turn-start-batch-055-cycles-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TURN-START-TEA-055 -CharacterId IRONCLAD -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-turn-start-batch-055-tea-relics.json -MonsterMoveChecksPath coverage\unattended\relic-turn-start-batch-055-tea-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TURN-START-CORE-055 -CharacterId DEFECT -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-turn-start-batch-055-core-relics.json -MonsterMoveChecksPath coverage\unattended\relic-turn-start-batch-055-core-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TURN-START-CONCH-055 -CharacterId IRONCLAD -EncounterId KnightsElite -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-turn-start-batch-055-conch-relics.json -MonsterMoveChecksPath coverage\unattended\relic-turn-start-batch-055-conch-checks.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-PERSISTENT-054 -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-persistent-batch-054-relics.json -MonsterMoveChecksPath coverage\unattended\relic-persistent-batch-054-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-RUNIC-PYRAMID-054 -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-runic-pyramid-batch-054-relics.json -MonsterMoveChecksPath coverage\unattended\relic-runic-pyramid-batch-054-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-INFUSED-CORE-054 -CharacterId DEFECT -EnemyCurrentHp 999 -RelicsPath coverage\unattended\relic-infused-core-batch-054-relics.json -MonsterMoveChecksPath coverage\unattended\relic-infused-core-batch-054-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-DAMAGE-054 -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-damage-batch-054-relics.json -MonsterMoveChecksPath coverage\unattended\relic-damage-batch-054-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-BOOT-054 -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-boot-batch-054-relics.json -MonsterMoveChecksPath coverage\unattended\relic-boot-batch-054-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TUNGSTEN-054 -EnemyCurrentHp 999 -AdditionalMonsterId BowlbugRock -CombatRelicsPath coverage\unattended\relic-tungsten-batch-054-relics.json -MonsterMoveChecksPath coverage\unattended\relic-tungsten-batch-054-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-VITRUVIAN-054 -CharacterId NECROBINDER -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-vitruvian-batch-054-relics.json -MonsterMoveChecksPath coverage\unattended\relic-vitruvian-batch-054-checks.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-DRAW-CYCLES-053 -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-draw-state-batch-053-cycles-relics.json -MonsterMoveChecksPath coverage\unattended\relic-draw-state-batch-053-cycles-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-POCKETWATCH-053 -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-draw-state-batch-053-pocketwatch-relics.json -MonsterMoveChecksPath coverage\unattended\relic-draw-state-batch-053-pocketwatch-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-FIRST-TURN-SNAPSHOT-053 -EnemyCurrentHp 999 -RelicsPath coverage\unattended\relic-draw-state-batch-053-first-turn-relics.json -MonsterMoveChecksPath coverage\unattended\relic-draw-state-batch-053-first-turn-checks.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-PURE-DRAW-052 -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-pure-batch-052-draw-relics.json -MonsterMoveChecksPath coverage\unattended\relic-pure-batch-052-draw-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-PURE-ENERGY-052 -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-pure-batch-052-energy-relics.json -MonsterMoveChecksPath coverage\unattended\relic-pure-batch-052-energy-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-TURN-CONDITIONS-052 -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-pure-batch-052-turn-relics.json -MonsterMoveChecksPath coverage\unattended\relic-pure-batch-052-turn-checks.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId RELIC-BOOMING-CONCH-052 -EncounterId KnightsElite -EnemyCurrentHp 999 -CombatRelicsPath coverage\unattended\relic-pure-batch-052-booming-relics.json -MonsterMoveChecksPath coverage\unattended\relic-pure-batch-052-booming-checks.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-043-OSTY -CharacterId NECROBINDER -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-043-osty.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-043-DIRGE -CharacterId NECROBINDER -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-043-dirge.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-043-CHOICES -CharacterId NECROBINDER -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-043-choices.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-043-AUTOPLAY -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-043-autoplay.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-043-POWERS -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-043-powers.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-043-NORMALITY -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-043-normality.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-043-ENTHRALLED -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-043-enthralled.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-043-RETURN-AUTOPLAY -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-043-return-autoplay.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-043-UPGRADED -CharacterId NECROBINDER -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-043-upgraded.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-043-NIGHTMARE-LIFECYCLE -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-043-nightmare-lifecycle.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-043-VOID-FORM -CharacterId IRONCLAD -EnemyCurrentHp 18 -CardId VOID_FORM -ClearPlayerHand -ExpectedPlayedCardId VOID_FORM -ExpectedFinishedTurn 2 -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-BOUNCING-FLASK -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-bouncing-flask.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-DIRECT-A -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-direct-a.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-OUTBREAK -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-outbreak.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-ECHOING-SLASH -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-echoing-slash.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-OMNISLICE -CharacterId IRONCLAD -EnemyCurrentHp 999 -AdditionalMonsterId CalcifiedCultist -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-omnislice.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-CHOICE-TRANSFORM -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-choice-transform.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-CHOICE-HAND -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-choice-hand.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-CHOICE-STATE -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-choice-state.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-CHOICE-PILES -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-choice-piles.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-CHOICE-TRANSFORM-UPGRADED -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-choice-transform-upgraded.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-CHOICE-UPGRADED -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-choice-upgraded.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-PURITY-UPGRADED -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-purity-upgraded.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-CHOICE-ZERO-OPTIONAL -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-choice-zero-optional.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-HIDDEN-DAGGERS-EMPTY -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-hidden-daggers-empty.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-BRAND-EMPTY -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-brand-empty.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-SCAVENGE-EMPTY -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-scavenge-empty.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-FRANTIC-ESCAPE -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-frantic-escape.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-ECHOING-SLASH-KILL -CharacterId IRONCLAD -EnemyCurrentHp 999 -AdditionalMonsterId CalcifiedCultist -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-echoing-slash-kill.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-DIRECT-UPGRADED -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-direct-upgraded.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-END-OF-DAYS-UPGRADED -CharacterId IRONCLAD -EnemyCurrentHp 999 -AdditionalMonsterId CalcifiedCultist -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-end-of-days-upgraded.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-042-END-OF-DAYS -CharacterId IRONCLAD -EnemyCurrentHp 999 -AdditionalMonsterId CalcifiedCultist -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-042-end-of-days.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-041-POWER-A -CharacterId DEFECT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-041-power-set-a.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-041-POWER-B -CharacterId REGENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-041-power-set-b.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-041-SYNCHRONIZE -CharacterId DEFECT -EnemyCurrentHp 999 -OrbsJson '[{"orbId":"LIGHTNING_ORB","count":1},{"orbId":"FROST_ORB","count":1}]' -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-041-synchronize.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-041-TURBO-SLEEVE -CharacterId SILENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-041-turbo-up-my-sleeve.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-041-SUMMON-FORTH -CharacterId REGENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-041-summon-forth.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-041-SHADOW-STEP -CharacterId SILENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-041-shadow-step.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-040-POWER-A -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-040-power-set-a.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-040-POWER-B -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-040-power-set-b.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-040-PALE-BLUE-DOT -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-040-pale-blue-dot.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-040-RESOURCES -CharacterId REGENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-040-resources-and-targets.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-040-SEEKING-EDGE -CharacterId REGENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-040-seeking-edge.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-040-SIGNAL-BOOST -CharacterId DEFECT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-040-signal-boost.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-039-POWER -CharacterId NECROBINDER -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-039-power-set.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-039-TARGET -CharacterId SILENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-039-target-effects.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-039-TARGET-LIFECYCLE -CharacterId SILENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-039-target-lifecycle.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-039-RESOURCES -CharacterId REGENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-039-resources.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-039-SHIVS -CharacterId SILENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-039-shivs.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-039-INK -CharacterId SILENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-039-blade-of-ink.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-039-APOTHEOSIS -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-039-apotheosis.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-039-ENLIGHTENMENT -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-039-enlightenment.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-039-ENLIGHTENMENT-UPGRADED -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-039-enlightenment-upgraded.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-039-STORM -CharacterId SILENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-039-storm-of-steel.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-039-HOTFIX-LIFECYCLE -CharacterId DEFECT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-039-hotfix-lifecycle.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-039-EXPOSE-ARTIFACT -CharacterId SILENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-039-expose-artifact.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-039-HAZE-MULTI -CharacterId SILENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-039-haze-multi.json -AdditionalMonsterId DampCultist -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-038-A -EnemyCurrentHp 80 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-038-power-set-a.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-038-B -EnemyCurrentHp 80 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-038-power-set-b.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-038-DANSE -EnemyCurrentHp 80 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-038-danse-macabre.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-038-PLANNER -EnemyCurrentHp 80 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-038-master-planner.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-038-SERPENT -EnemyCurrentHp 80 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-038-serpent-form.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-038-STORM -EnemyCurrentHp 80 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-038-storm.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-038-NO-ESCAPE -EnemyCurrentHp 80 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-038-no-escape.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-037-POWER-A -CharacterId DEFECT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-037-power-set-a.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-037-POWER-B -CharacterId DEFECT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-037-power-set-b.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-037-SPECIAL -CharacterId REGENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-037-special-effects.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-037-BULLET-TIME -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-037-bullet-time.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-037-FERAL-HISTORY -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-037-feral-history.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-037-JUGGLING-HISTORY -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-037-juggling-history.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-036-A -CharacterId DEFECT -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-036-power-set-a.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-036-B -CharacterId DEFECT -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-036-power-set-b.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-036-ANTICIPATE -CharacterId DEFECT -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-036-anticipate-lifecycle.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-035-SELF -CharacterId IRONCLAD -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-035-self-powers.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-035-TARGET -CharacterId IRONCLAD -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-035-target-powers.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-035-BULK-UP -CharacterId DEFECT -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-035-bulk-up.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-035-REGENT -CharacterId REGENT -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-035-regent.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId CARD-ON-PLAY-BATCH-035-PIERCING-WAIL -CharacterId IRONCLAD -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\card-on-play-batch-035-piercing-wail-lifecycle.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-034-ACCURACY -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-034-accuracy.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-034-BLOCK -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-034-block.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-034-COST-LOCATION -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-034-cost-location.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-034-HANG -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-034-hang.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-034-HARD-TO-KILL -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-034-hard-to-kill.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-034-LEADERSHIP -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-034-leadership.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-034-LETHALITY -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-034-lethality.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-034-ONE-FOR-ALL -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-034-one-for-all.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-034-PHANTOM-BLADES -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-034-phantom-blades.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-034-SOAR -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-034-soar.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-034-TRACKING -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-034-tracking.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-034-CALCIFY -CharacterId NECROBINDER -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-034-calcify.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-034-DIE-FOR-YOU -CharacterId NECROBINDER -EnemyCurrentHp 50 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-034-die-for-you.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId SMOKE-002
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-WATERFALL-001 -EncounterId WATERFALL_GIANT_BOSS -PowerId STEAM_ERUPTION_POWER -PowerAmount 10 -ExpectedFinishedTurn 2
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-AXEBOT-HAMMER-001 -EncounterId AxebotsNormal -MonsterMoveId HAMMER_UPPERCUT_MOVE -ExpectedPlayerHpLoss 14 -ExpectedPlayerPowersJson '{"WEAK_POWER":2,"FRAIL_POWER":2}'
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-004 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-004.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-005 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-005.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-006 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-006.json -AdditionalMonsterId Fabricator
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-007 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-007.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-008 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-008.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-009 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-009.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-010 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-010.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-011 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-011.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-012 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-012.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-013 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-013.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-014 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-014.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-015 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-015.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-016 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-016.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-017 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-017.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-018 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-018.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-019 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-019.json -AdditionalMonsterId TorchHeadAmalgam
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-020 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-020.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-021-KAISER -EncounterId KaiserCrabBoss -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-021-kaiser.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-021-SUPPORT -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-021-support.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-022 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-022.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-023 -EncounterId BowlbugsWeak -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-023.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-024 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-024.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-026 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-026.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-027 -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-027.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-028-LIFECYCLE -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-028-lifecycle.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-028-TURN-END -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-028-turn-end.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-029-SHRINK -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-029-lifecycle.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-029-ARTIFACT -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-029-artifact.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-029-TANGLED -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-029-tangled.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-029-RINGING -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-029-ringing.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-029-INTANGIBLE -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-029-intangible.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-030-DAMAGE -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-030-damage.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-030-BLOCK -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-030-block.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-030-POISON -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-030-poison.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-030-BLOCK-LIFECYCLE -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-030-block-lifecycle.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-030-SLOW -EncounterId LivingFogNormal -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-030-slow.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-031-RESOURCES -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-031-resources.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-031-MODIFIERS -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-031-modifiers.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-031-LIFECYCLE -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-031-lifecycle.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-032-RESOURCES -CharacterId DEFECT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-032-resources.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-032-STARS -CharacterId REGENT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-032-stars.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-032-START-POWERS -CharacterId IRONCLAD -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-032-start-powers.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-032-COOLANT -CharacterId DEFECT -EnemyCurrentHp 999 -OrbsJson '["LIGHTNING_ORB","FROST_ORB"]' -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-032-coolant.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-032-GLOBAL -AdditionalMonsterId TurretOperator -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-032-global.json -KeepGameOpen
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-032-RITUAL -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-032-ritual.json -ExitOnComplete
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-033-COLOSSUS -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-033-colossus.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-033-TAINTED -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-033-tainted.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-033-CONCOCT -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-033-concoct.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-033-CORROSIVE-WAVE -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-033-corrosive-wave.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-033-DEMISE -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-033-demise.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-033-DISINTEGRATION -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-033-disintegration.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-033-LIFECYCLE -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-033-lifecycle.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-033-ORBS-NEMESIS -CharacterId DEFECT -EnemyCurrentHp 999 -OrbsJson '[{"orbId":"FROST_ORB","count":1}]' -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-033-orbs-nemesis.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-033-TENDER -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-033-tender.json
pwsh -NoProfile -File tools\run-unattended-test.ps1 -ScenarioId MONSTER-MOVES-BATCH-033-JUGGLING -EnemyCurrentHp 999 -MonsterMoveChecksPath coverage\unattended\monster-moves-batch-033-juggling.json -ExitOnComplete
```

最后一批通过后退出游戏：在该条命令末尾添加 `-ExitOnComplete`。失败结果会保留游戏进程并返回主菜单，修正夹具或代码后可按 DLL 是否重编译决定直接复用还是重启。`-KeepGameOpen` 仅为旧命令兼容保留；现在不传退出参数也会默认保持进程。

## 自动覆盖范围

| 范围 | 状态 | 说明 |
|---|---|---|
| 单人战斗卡牌/选牌/生成牌 | 通过 | 既有牌选择、嵌套选择、随机生成、局内变换、升级/降级/附魔及生成卡后续监听均有严格差分 |
| Power、遗物、药水、充能球 | 通过 | 当前游戏 `0.111.0` 的单人战斗行为目录无未分类、无静态行为证据缺口、无原生重扫边界 |
| 怪物行动、死亡、复活、召唤 | 通过 | 57 个补偿行动全量分片复跑；结构性复活、召唤、替换、特殊移除另有整战与定向生命周期回归 |
| 跨回合算到底 | 通过 | 同族、实验体、花园鳗、旧日雕像、女王、双小啃兽等整战在预算覆盖范围内逐回合复用；生产搜索只允许时间和节点预算终止，回合上限只用于增量验证模式 |
| 多人模式及多人专属内容 | 不在范围 | 不把多人专属选择、队友死亡后的 Hook 活性或多人卡牌记为单人适配缺口 |

## 人工待测

| ID | 状态 | 检查项 |
|---|---|---|
| `SOLVER-DISABLE-525` | 待测 | 设置中禁用后立即取消后台搜索与自动部署、关闭全自动并清除旧路线；后续回合和首回合选牌阶段均不自动求解，手操不产生重算；新战斗仍可打开设置，重新启用后按当前真实状态搜索 |
| `UI-OVERLAY-001` | 待测 | 拖动、轻量收起按钮、单行 `14px` 粗体概览、无计数的状态详情按钮、无键位提示的重新计算/执行按钮、纯“推荐路线”标题、HP/费用固定双列、始终显示“余 0 费”、完整搜索回合滚动及底栏对齐；路线用药显示为 `预计用x瓶药`，数量等于已喝加路线剩余并在跨回合复用时保持；页面不使用中圆点拼接信息 |
| `UI-FULL-AUTO-001` | 待测 | 全自动关闭时为暗色次级按钮、运行中为绿色正向按钮，战斗结束暂停开关与退出战斗清理 |
| `UI-FONT-001` | 待测 | 中文字体使用游戏思源黑体、不回退到默认日文字形；普通/富文本/按钮 `2px` 描边清晰且箭头、展开符号不缺字 |
| `PERF-FRAME-001` | 待测 | 发牌动画和多回合后台搜索期间的实际帧时间体感；日志仅作为分配与 GC 辅助证据 |
| `RF-OFFICIAL-WORKSHOP-COEXIST-069` | 通过 | RF 本地 fork 已与 `0.10.0` 共同跑完完整长线；用户随后订阅创意工坊原版 RF 并完成一次实机启动，未出现初始化或共存问题 | 2026-08-21 |

## 判定规则

- “通过”必须有同一 `runId` 的 `Passed` 结果，并核对对应 `SEARCH_REQUEST`、`RESULT`、`ACTION`、`DEPLOY_*` 和真实怪物行动日志。
- 只编译通过、只看到最终胜利或只看模拟结果都不能标记为通过。
- `RID/resources still in use at exit` 当前记录为 Godot 退出噪音；任何 `CombatSolver/Unattended FAILED`、`SEARCH_FAILURE`、`DEPLOY_FAILURE` 或状态断言失败均判定场景失败。
