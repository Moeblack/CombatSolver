using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Models.Capabilities;
using System.Collections;
using System.Reflection;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Mirrors.Hooks;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.Card;
using CombatSolver.Engine.InCombat.Simulation;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private static readonly object RitsuDefaultCapabilityRegistrationTestLock = new();
    private static bool _ritsuDefaultCapabilityRegistrationTestCompleted;

    private static void AssertForkBoundaries(CombatState combat, Player player)
    {
        CardModel card = player.PlayerCombatState?.Hand.Cards.FirstOrDefault()
            ?? throw new InvalidOperationException("Fork 边界测试要求手牌中至少有一张牌。");
        SimulatedCombatState simulatedCombat = new(combat);
        CombatPredictionSimulator simulator = new(simulatedCombat);
        AssertRitsuCapabilityFastPath(simulator, player, card);
        AssertChoiceRiskScopedToCardPlay(simulator, card);
        AssertChoiceKeyCache(simulator, player, card);
        AssertChoiceTokenSurvivesStateMutation(combat, player, card);
        AssertMonsterAiUsesCapturedMachine(combat);
        AssertCardCompletionSettlesPowerAmountChanges(combat, player);
        AssertBeforeCardPlayedPowerConsumptionCommits(combat, player);
        AssertGeneratedCardCreatorDrivesSupermassive(combat, player);

        using (simulator.PushActionSource(card, PredictionActionKind.CardPlay))
            AssertForkRejected(simulator, "completed actions");

        simulatedCombat.BeginActionChoices((IReadOnlyList<PlanCardChoice>?)null);
        try
        {
            AssertForkRejected(simulator, "action choice resolution");
        }
        finally
        {
            simulatedCombat.EndActionChoices();
        }

        using (simulatedCombat.BeginCardExecutionScope())
            AssertForkRejected(simulator, "card execution");

        simulator.ActionRelicTriggers = new ActionRelicTriggerRecorder();
        AssertForkRejected(simulator, "action relic triggers");
        simulator.ActionRelicTriggers = null;

        PenNib relic = ModelDb.All.OfType<PenNib>().Single();
        PenNibPredictionState penNib = simulator.StateStore.Get(
            (AbstractModel)relic,
            () => new PenNibPredictionState(relic));
        penNib.AttackToDouble = card;
        AssertForkRejected(simulator, "Pen Nib");
        penNib.AttackToDouble = null;

        PaelsLegion paelsLegion = ModelDb.All.OfType<PaelsLegion>().Single();
        PaelsLegionPredictionState paelsState = simulator.StateStore.Get(
            (AbstractModel)paelsLegion,
            () => new PaelsLegionPredictionState(paelsLegion));
        CardPlay paelsPlay = new()
        {
            Card = card,
            Player = player,
            Target = null,
            ResultPile = PileType.Discard,
            Resources = default,
            IsAutoPlay = false,
            PlayIndex = 0,
            PlayCount = 1,
        };
        paelsState.AffectedCardPlay = paelsPlay;
        AssertForkRejected(simulator, "Pael's Legion");
        AfterCardPlayedMirrors.CompleteOrAbort(simulator, paelsPlay, completed: true);
        if (paelsState.AffectedCardPlay != null
            || paelsState.Cooldown != paelsLegion.DynamicVars["Turns"].IntValue
            || !paelsState.TriggeredBlockLastTurn)
        {
            throw new InvalidOperationException("佩尔军团没有在完整 CardPlay 边界提交格挡触发。");
        }
        paelsState.AffectedCardPlay = paelsPlay;
        AfterCardPlayedMirrors.CompleteOrAbort(simulator, paelsPlay, completed: false);
        if (paelsState.AffectedCardPlay != null)
            throw new InvalidOperationException("佩尔军团没有在中止 CardPlay 边界清理瞬时状态。");

        Vambrace vambraceRelic = ModelDb.All.OfType<Vambrace>().Single();
        VambracePredictionState vambrace = simulator.StateStore.Get(
            (AbstractModel)vambraceRelic,
            () => new VambracePredictionState(vambraceRelic));
        vambrace.TriggeringCard = card;
        vambrace.BlockGainedThisCombat = true;
        CombatPredictionSimulator vambraceFork = simulator.Fork();
        VambracePredictionState forkedVambrace = vambraceFork.StateStore.GetReadOnly(
            (AbstractModel)vambraceRelic,
            () => new VambracePredictionState(vambraceRelic));
        if (!ReferenceEquals(forkedVambrace.TriggeringCard, card)
            || !forkedVambrace.BlockGainedThisCombat)
        {
            throw new InvalidOperationException("Vambrace 稳定战斗状态没有跨 Fork 保留。");
        }
        vambrace.TriggeringCard = null;
        vambrace.BlockGainedThisCombat = false;

        CurlUpPredictionState curlUp = simulator.StateStore.Get<CurlUpPredictionState>(card);
        curlUp.PlayedCard = card;
        AssertForkRejected(simulator, "Curl Up");
        curlUp.PlayedCard = null;

        CombatPredictionSimulator pendingHistory = new(new SimulatedCombatState(combat));
        pendingHistory.History.CardDrawn(new PredictedCard(card), fromHandDraw: false);
        AssertForkRejected(pendingHistory, "unresolved deferred entries");

        int originalEnergy = simulator.State.GetPlayerCombatState(player).Energy;
        CombatPredictionSimulator fork = simulator.Fork();
        fork.State.GetPlayerCombatState(player).GainEnergy(1);
        if (simulator.State.GetPlayerCombatState(player).Energy != originalEnergy)
            throw new InvalidOperationException("稳定边界 Fork 没有隔离玩家能量状态。");

        AssertPredictedCardForkOwnershipAndObservers(combat, player, card);
        AssertAmountOnTurnStartCacheReuse(combat, player);
        AssertSparsePowerAfflictionCardTracking(combat, player, card);
        AssertProjectedShuffleEquivalence(simulator, player);
        AssertSpawnHpUsesSimulatedCreatureState(combat);
        AssertPendingSpawnCanEnterIllusionRevive(combat);
        AssertPendingRandomBranchSpawnRollsAtTurnBoundary(combat);
        AssertDefeatedEnemyRejectsLatePowerApplication(combat, player);
        AssertWhisperingEarringOnlyRunsOnFirstTurn(simulator, simulatedCombat, player);
        AssertPredictionForkContextIdentityIndex();
        AssertForkableListEnumeration();
    }

    private static void AssertMonsterAiUsesCapturedMachine(CombatState combat)
    {
        MonsterModel live = combat.Enemies.FirstOrDefault()?.Monster
            ?? throw new InvalidOperationException("怪物行动快照测试要求至少有一名敌人。");
        MonsterModel detached = PredictionUtils.CloneModelForSimulation(live);
        BranchMonsterAiState state = BranchMonsterAi.Capture(detached);
        FieldInfo field = typeof(MonsterModel).GetField(
            "_moveStateMachine",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(MonsterModel).FullName, "_moveStateMachine");
        field.SetValue(detached, null);

        CombatPredictionSimulator simulator = new(new SimulatedCombatState(combat));
        _ = BranchMonsterAi.Advance(state, simulator, (SimulatedCombatState)simulator.State.CombatState);
    }

    private static void AssertChoiceTokenSurvivesStateMutation(
        CombatState combat,
        Player player,
        CardModel liveCard)
    {
        SimulatedCombatState simulatedCombat = new(combat);
        CombatPredictionSimulator simulator = new(simulatedCombat);
        SimPlayerCombatState state = simulator.State.GetPlayerCombatState(player);
        PredictedCard card = state.FindCard(liveCard)
            ?? throw new InvalidOperationException("选牌身份测试找不到目标手牌。");
        CardChoiceSpec spec = new(
            PlanChoiceEffect.Discard,
            PileType.Hand,
            1,
            1,
            [card],
            state.Hand.Cards,
            ReplacementValue: 0d);
        PlanCardChoice choice = CardChoiceSupport.BuildRequestedChoice(
            spec,
            [card.Preview.Id.Entry]);

        card.MutablePreview.ExhaustOnNextPlay = !card.Preview.ExhaustOnNextPlay;
        IReadOnlyList<PredictedCard> selected = CardChoiceSupport.ResolveStandaloneChoice(
            simulator,
            choice,
            [card],
            expectedCount: 1,
            PileType.Hand);
        if (!ReferenceEquals(selected.Single(), card))
            throw new InvalidOperationException("选牌令牌没有在卡牌状态变化后保持实体身份。");
    }

    private static void AssertCardCompletionSettlesPowerAmountChanges(
        CombatState combat,
        Player player)
    {
        SimulatedCombatState simulatedCombat = new(combat);
        CombatPredictionSimulator simulator = new(simulatedCombat);
        simulatedCombat.Apply<StrengthPower>(
            player.Creature,
            1,
            player.Creature);
        StrengthPower power = simulatedCombat.GetPower<StrengthPower>(player.Creature)
            ?? throw new InvalidOperationException("力量影子层数测试没有建立力量。");
        PowerLifecycleSupport.ResolvePowerAmountChanges(simulator, simulatedCombat);
        PowerAmountPredictionState shadow = simulator.StateStore.GetPowerAmount(power);
        shadow.Amount = power.Amount + 1;

        ((ICombatPredictionCardExecutionSink)simulatedCombat).CompleteCardExecution(simulator);
        if (power.Amount != shadow.Amount)
            throw new InvalidOperationException("出牌事务结束后没有提交力量影子层数。");
        _ = simulator.Fork();
    }

    private static void AssertBeforeCardPlayedPowerConsumptionCommits(
        CombatState combat,
        Player player)
    {
        SimulatedCombatState simulatedCombat = new(combat);
        CombatPredictionSimulator simulator = new(simulatedCombat);
        simulatedCombat.Apply<FreePowerPower>(player.Creature, 2, player.Creature);
        PowerLifecycleSupport.ResolvePowerAmountChanges(simulator, simulatedCombat);
        PredictedCard powerCard = PredictedCard.Create(
            ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.MachineLearning>(),
            player);
        simulator.AddGeneratedCardToCombat(
            powerCard,
            PileType.Hand,
            player,
            resultKind: CardGenerationResultKind.Fixed);

        simulator.ManualPlay(powerCard, target: null, out _);
        if (simulatedCombat.GetAmount<FreePowerPower>(player.Creature) != 1)
            throw new InvalidOperationException("免费能力层数没有在卡牌效果开始前提交消耗。");
        _ = simulator.Fork();
    }

    private static void AssertGeneratedCardCreatorDrivesSupermassive(
        CombatState combat,
        Player player)
    {
        SimulatedCombatState simulatedCombat = new(combat);
        CombatPredictionSimulator simulator = new(simulatedCombat);
        PredictedCard supermassive = PredictedCard.Create(
            ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.Supermassive>(),
            player);
        decimal baseline = CalculateSupermassive(simulator, supermassive);

        simulator.CreateAndAddGeneratedCardsToCombat<MegaCrit.Sts2.Core.Models.Cards.Debris>(
            player,
            PileType.Discard,
            1,
            creator: null);
        if (CalculateSupermassive(simulator, supermassive) != baseline)
            throw new InvalidOperationException("无创建者的生成牌被超质量体计入。");

        simulator.CreateAndAddGeneratedCardsToCombat<MegaCrit.Sts2.Core.Models.Cards.Debris>(
            player,
            PileType.Discard,
            1,
            creator: player);
        decimal expected = baseline + supermassive.Preview.DynamicVars.ExtraDamage.BaseValue;
        if (CalculateSupermassive(simulator, supermassive) != expected)
            throw new InvalidOperationException("玩家创建的生成牌没有被超质量体计入。");
    }

    private static decimal CalculateSupermassive(
        CombatPredictionSimulator simulator,
        PredictedCard card)
    {
        CalculatedVar calculated = (CalculatedVar)card.Preview.DynamicVars.CalculatedDamage;
        if (!CalculatedVarSpecRegistry.TryCalculate(calculated, simulator, card, target: null, out decimal value))
            throw new InvalidOperationException("超质量体计算夹具未命中支持注册表。");
        return value;
    }

    private static void AssertSpawnHpUsesSimulatedCreatureState(CombatState combat)
    {
        MegaCrit.Sts2.Core.Models.Monsters.ToughEgg canonical =
            ModelDb.Monster<MegaCrit.Sts2.Core.Models.Monsters.ToughEgg>();
        int minimum = canonical.MinInitialHp;
        int maximum = canonical.MaxInitialHp;
        int reserved = Math.Clamp(17, minimum, maximum);
        if (combat.Enemies.Any(enemy => enemy.MaxHp >= minimum && enemy.MaxHp <= maximum))
            return;

        SimulatedCombatState simulatedCombat = new(combat);
        CombatPredictionSimulator simulator = new(simulatedCombat);
        Creature existing = simulatedCombat.CreatePredictedMonster(
            simulator,
            (MegaCrit.Sts2.Core.Models.Monsters.ToughEgg)ModelDb
                .Monster<MegaCrit.Sts2.Core.Models.Monsters.ToughEgg>()
                .ToMutable(),
            CombatSide.Enemy,
            slot: null);
        simulatedCombat.AddPredictedMonster(existing);
        simulator.State.GetCreature(existing).SetMaxHp(reserved);
        existing.SetMaxHpInternal(minimum);
        if (simulator.State.GetCreature(existing).MaxHp != reserved)
            throw new InvalidOperationException("产卵生命判重夹具没有建立模拟/原生最大生命差异。");

        HashSet<int> spawned = [];
        for (int index = 0; index < maximum - minimum; index++)
        {
            Creature creature = simulatedCombat.CreatePredictedMonster(
                simulator,
                (MegaCrit.Sts2.Core.Models.Monsters.ToughEgg)ModelDb
                    .Monster<MegaCrit.Sts2.Core.Models.Monsters.ToughEgg>()
                    .ToMutable(),
                CombatSide.Enemy,
                slot: null);
            simulatedCombat.AddPredictedMonster(creature);
            spawned.Add(simulator.State.GetCreature(creature).MaxHp);
        }
        HashSet<int> expected = Enumerable.Range(minimum, maximum - minimum + 1)
            .Where(value => value != reserved)
            .ToHashSet();
        if (!spawned.SetEquals(expected))
        {
            throw new InvalidOperationException(
                $"新怪物生命判重没有采用模拟状态；actual={string.Join(',', spawned.Order())}。");
        }
    }

    private static void AssertPendingSpawnCanEnterIllusionRevive(CombatState combat)
    {
        SimulatedCombatState simulatedCombat = new(combat)
        {
            CurrentSide = CombatSide.Enemy,
        };
        CombatPredictionSimulator simulator = new(simulatedCombat);
        Creature source = simulatedCombat.Enemies.First();
        Creature illusion = MonsterSpawnSupport.Spawn<MegaCrit.Sts2.Core.Models.Monsters.Parafright>(
            simulator,
            simulatedCombat,
            source,
            slot: null,
            minion: true);
        if (simulatedCombat.GetPredictedMoveId(illusion) != "SLAM_MOVE")
            throw new InvalidOperationException("敌方回合生成的幻象没有保留原版初始行动记录。");

        simulatedCombat.BeginIllusionRevive(illusion);
        simulatedCombat.PrepareMonsterMoveForNextRound(simulator, illusion, performedMove: null);
        if (simulatedCombat.GetPredictedMoveId(illusion) != "REVIVE_MOVE")
            throw new InvalidOperationException("幻象复活动作被待处理的初始行动覆盖。");
    }

    private static void AssertPendingRandomBranchSpawnRollsAtTurnBoundary(CombatState combat)
    {
        SimulatedCombatState simulatedCombat = new(combat)
        {
            CurrentSide = CombatSide.Enemy,
        };
        CombatPredictionSimulator simulator = new(simulatedCombat);
        Creature source = simulatedCombat.Enemies.First();
        int rngBeforeSpawn = simulator.Rng.MonsterAi.Counter();
        Creature rat = MonsterSpawnSupport.Spawn<MegaCrit.Sts2.Core.Models.Monsters.TwoTailedRat>(
            simulator,
            simulatedCombat,
            source,
            slot: null);
        if (simulator.Rng.MonsterAi.Counter() != rngBeforeSpawn)
            throw new InvalidOperationException("敌方回合生成的随机初始行动怪物提前消费了怪物 RNG。");

        simulatedCombat.PrepareMonsterMoveForNextRound(simulator, rat, performedMove: null);
        if (simulator.Rng.MonsterAi.Counter() <= rngBeforeSpawn)
            throw new InvalidOperationException("随机初始行动怪物没有在回合边界消费怪物 RNG。");
        if (simulatedCombat.GetPredictedMoveId(rat) is not (
                "SCRATCH_MOVE" or "DISEASE_BITE_MOVE" or "SCREECH_MOVE"))
        {
            throw new InvalidOperationException("双尾鼠没有在回合边界得到合法的初始行动。");
        }
    }

    private static void AssertDefeatedEnemyRejectsLatePowerApplication(
        CombatState combat,
        Player player)
    {
        SimulatedCombatState simulatedCombat = new(combat);
        CombatPredictionSimulator simulator = new(simulatedCombat);
        Creature enemy = simulatedCombat.Enemies.First();
        simulator.State.GetCreature(enemy).CurrentHp = 0;
        CorePowerSupport.ApplyEnemyDeathPowers(
            simulator,
            simulatedCombat,
            simulatedCombat.KnownEnemies,
            new HashSet<uint>());
        simulatedCombat.Apply<VulnerablePower>(enemy, 2, player.Creature);
        if (simulatedCombat.GetAmount<VulnerablePower>(enemy) != 0)
            throw new InvalidOperationException("永久死亡的敌人仍然接收了后续 Power。");
    }

    private static void AssertWhisperingEarringOnlyRunsOnFirstTurn(
        CombatPredictionSimulator simulator,
        SimulatedCombatState combat,
        Player player)
    {
        if (!combat.RelicsOf(player).Any(static relic => relic is WhisperingEarring && !relic.IsMelted))
            return;
        int cardsBefore = simulator.State.GetPlayerCombatState(player).Hand.Cards.Count;
        combat.TriggerWhisperingEarring(simulator, player, 2, new HashSet<uint>());
        int cardsAfter = simulator.State.GetPlayerCombatState(player).Hand.Cards.Count;
        if (cardsAfter != cardsBefore)
            throw new InvalidOperationException("低语耳饰在第二回合再次自动出牌。");
    }

    private static void AssertPredictedCardForkOwnershipAndObservers(
        CombatState combat,
        Player player,
        CardModel liveCard)
    {
        SimulatedCombatState parentCombat = new(combat);
        CombatPredictionSimulator parentSimulator = new(parentCombat);
        SimPlayerCombatState parentState = parentSimulator.State.GetPlayerCombatState(player);
        PredictedCard parentCard = parentState.FindCard(liveCard)
            ?? throw new InvalidOperationException("预测卡牌 Fork 所有权测试找不到父卡牌。");
        if (!ReferenceEquals(parentCard.GetPile(parentState), parentState.Hand))
            throw new InvalidOperationException("预测卡牌没有记录父分支手牌所有权。");

        Action parentObserver = GetCardMutationObserver(parentCard);
        if (parentState.AllCards.Any(card =>
                !ReferenceEquals(GetCardMutationObserver(card), parentObserver)))
        {
            throw new InvalidOperationException("同一模拟分支没有共享单一卡牌变更 observer。");
        }
        IEnumerable<AbstractModel> parentListeners = parentCombat.IterateHookListeners();

        CombatPredictionSimulator childSimulator = parentSimulator.Fork();
        SimulatedCombatState childCombat = (SimulatedCombatState)childSimulator.State.CombatState;
        SimPlayerCombatState childState = childSimulator.State.GetPlayerCombatState(player);
        PredictedCard childCard = childState.FindCard(liveCard)
            ?? throw new InvalidOperationException("预测卡牌 Fork 所有权测试找不到子卡牌。");
        Action childObserver = GetCardMutationObserver(childCard);
        if (ReferenceEquals(parentObserver, childObserver)
            || childState.AllCards.Any(card =>
                !ReferenceEquals(GetCardMutationObserver(card), childObserver)))
        {
            throw new InvalidOperationException("卡牌变更 observer 没有按父子 Fork 隔离。");
        }
        if (!ReferenceEquals(childCard.GetPile(childState), childState.Hand)
            || childCard.GetPile(parentState) is not null
            || parentCard.GetPile(childState) is not null)
        {
            throw new InvalidOperationException("预测卡牌牌堆反向引用跨 Fork 泄漏。");
        }

        IEnumerable<AbstractModel> childListenersBefore = childCombat.IterateHookListeners();
        CardModel childPreviewBefore = childCard.Preview;
        childCard.MutablePreview.ExhaustOnNextPlay = !childPreviewBefore.ExhaustOnNextPlay;
        IEnumerable<AbstractModel> childListenersAfter = childCombat.IterateHookListeners();
        if (ReferenceEquals(childListenersBefore, childListenersAfter)
            || !childListenersAfter.Contains(childCard.Preview)
            || childListenersAfter.Contains(childPreviewBefore))
        {
            throw new InvalidOperationException("子分支卡牌变更没有精确重建 Hook listener 缓存。");
        }
        if (!ReferenceEquals(parentListeners, parentCombat.IterateHookListeners())
            || !parentListeners.Contains(parentCard.Preview)
            || parentListeners.Contains(childCard.Preview))
        {
            throw new InvalidOperationException("子分支卡牌变更污染了父 Hook listener 缓存。");
        }

        if (!childState.Hand.Remove(childCard))
            throw new InvalidOperationException("预测卡牌所有权测试无法从子手牌移除卡牌。");
        childState.DiscardPile.Add(childCard);
        if (!ReferenceEquals(childCard.GetPile(childState), childState.DiscardPile)
            || !ReferenceEquals(parentCard.GetPile(parentState), parentState.Hand))
        {
            throw new InvalidOperationException("预测卡牌移动后没有保持父子牌堆所有权隔离。");
        }
        if (!childState.DiscardPile.Remove(childCard) || childCard.GetPile(childState) is not null)
            throw new InvalidOperationException("预测卡牌移除后没有清理牌堆反向引用。");

        PredictedCard clearProbe = new(liveCard);
        SimCardPile clearPile = new(PileType.Hand, [clearProbe]);
        clearPile.Clear();
        if (clearProbe.OwnerPile is not null)
            throw new InvalidOperationException("预测牌堆清空后没有清理卡牌反向引用。");
    }

    private static Action GetCardMutationObserver(PredictedCard card)
    {
        FieldInfo observerField = typeof(PredictedCard).GetField(
            "_mutationObserver",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(PredictedCard).FullName, "_mutationObserver");
        return (Action?)observerField.GetValue(card)
            ?? throw new InvalidOperationException("预测卡牌没有安装变更 observer。");
    }

    private static void AssertAmountOnTurnStartCacheReuse(CombatState combat, Player player)
    {
        SimulatedCombatState parentCombat = new(combat);
        CombatPredictionSimulator parentSimulator = new(parentCombat);
        parentCombat.Apply<StrengthPower>(player.Creature, 2, player.Creature);
        _ = parentCombat.DrainPowerAmountChanges();
        StrengthPower parentPower = parentCombat.EffectivePowers()
            .OfType<StrengthPower>()
            .Single(power => ReferenceEquals(power.Owner, player.Creature));
        int parentAmountOnTurnStart = parentPower.AmountOnTurnStart;

        CombatPredictionSimulator childSimulator = parentSimulator.Fork();
        SimulatedCombatState childCombat = (SimulatedCombatState)childSimulator.State.CombatState;
        StrengthPower childPower = childCombat.EffectivePowers()
            .OfType<StrengthPower>()
            .Single(power => ReferenceEquals(power.Owner, player.Creature));
        IReadOnlyList<PowerModel> listenersBefore = childCombat.EffectivePowers();
        childPower.AmountOnTurnStart = childPower.Amount + 1;
        childCombat.SnapshotPowerAmountsAtTurnStart([player.Creature]);
        if (!ReferenceEquals(listenersBefore, childCombat.EffectivePowers())
            || childPower.AmountOnTurnStart != childPower.Amount)
        {
            throw new InvalidOperationException(
                "AmountOnTurnStart 更新没有复用同一分支的 Power listener 缓存。");
        }
        if (parentPower.AmountOnTurnStart != parentAmountOnTurnStart)
            throw new InvalidOperationException("AmountOnTurnStart 更新跨 Fork 污染父 Power。");
    }

    private static void AssertSparsePowerAfflictionCardTracking(
        CombatState combat,
        Player player,
        CardModel liveCard)
    {
        SimulatedCombatState parentCombat = new(combat);
        CombatPredictionSimulator parentSimulator = new(parentCombat);
        SimPlayerCombatState parentState = parentSimulator.State.GetPlayerCombatState(player);
        PredictedCard parentCard = parentState.FindCard(liveCard)
            ?? throw new InvalidOperationException("Power affliction 稀疏集合测试找不到父卡牌。");

        parentCombat.NormalizePowerCardState(parentSimulator);
        if (GetPowerAfflictionKnownCards(parentCombat) is not null)
        {
            throw new InvalidOperationException(
                "Power affliction 首次归一化不应记录战斗快照中的初始牌。");
        }

        PredictedCard generatedCard = parentCard.CreateClone();
        parentState.DiscardPile.Add(generatedCard);
        parentCombat.RegisterGeneratedCombatCard(generatedCard);
        parentCombat.NormalizePowerCardState(parentSimulator);
        HashSet<PredictedCard> parentKnown = GetPowerAfflictionKnownCards(parentCombat)
            ?? throw new InvalidOperationException("Power affliction 没有记录生成牌。");
        if (parentKnown.Count != 1 || !parentKnown.Contains(generatedCard))
            throw new InvalidOperationException("Power affliction 稀疏集合记录了非生成牌或漏掉生成牌。");

        parentCombat.NormalizePowerCardState(parentSimulator);
        if (parentKnown.Count != 1)
            throw new InvalidOperationException("Power affliction 重复归一化再次记录了同一生成牌。");

        CombatPredictionSimulator childSimulator = parentSimulator.Fork();
        SimulatedCombatState childCombat = (SimulatedCombatState)childSimulator.State.CombatState;
        PredictedCard childGeneratedCard = childSimulator.State
            .GetPlayerCombatState(player)
            .FindCard(generatedCard.Original)
            ?? throw new InvalidOperationException("Power affliction Fork 后找不到生成牌。");
        HashSet<PredictedCard> childKnown = GetPowerAfflictionKnownCards(childCombat)
            ?? throw new InvalidOperationException("Power affliction Fork 后丢失生成牌集合。");
        if (childKnown.Count != 1
            || !childKnown.Contains(childGeneratedCard)
            || childKnown.Contains(generatedCard)
            || !parentKnown.Contains(generatedCard)
            || parentKnown.Contains(childGeneratedCard))
        {
            throw new InvalidOperationException("Power affliction 稀疏集合没有按 Fork 重映射或隔离。");
        }

        if (!parentState.DiscardPile.Remove(generatedCard))
            throw new InvalidOperationException("Power affliction 测试无法移除生成牌。");
        parentCombat.UnregisterGeneratedCombatCard(generatedCard);
        parentState.DiscardPile.Add(generatedCard);
        parentCombat.RegisterGeneratedCombatCard(generatedCard);
        parentCombat.NormalizePowerCardState(parentSimulator);
        if (parentKnown.Count != 1)
        {
            throw new InvalidOperationException(
                "Power affliction 把同一 wrapper 重新入场误判为新的生成牌。");
        }

        PredictedCard secondGeneratedCard = parentCard.CreateClone();
        parentState.DiscardPile.Add(secondGeneratedCard);
        parentCombat.RegisterGeneratedCombatCard(secondGeneratedCard);
        parentCombat.NormalizePowerCardState(parentSimulator);
        if (parentKnown.Count != 2 || !parentKnown.Contains(secondGeneratedCard))
            throw new InvalidOperationException("Power affliction 没有区分两个独立生成牌 wrapper。");
    }

    private static HashSet<PredictedCard>? GetPowerAfflictionKnownCards(
        SimulatedCombatState combat)
    {
        FieldInfo field = typeof(SimulatedCombatState).GetField(
            "_powerAfflictionKnownCards",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(
                typeof(SimulatedCombatState).FullName,
                "_powerAfflictionKnownCards");
        return (HashSet<PredictedCard>?)field.GetValue(combat);
    }

    private static void AssertProjectedShuffleEquivalence(
        CombatPredictionSimulator simulator,
        Player player)
    {
        List<PredictedCard> source = simulator.State
            .GetPlayerCombatState(player)
            .AllCards
            .ToList();
        if (source.Count == 0)
            throw new InvalidOperationException("投影洗牌等价测试要求至少一张牌。");
        source.AddRange(source.AsEnumerable().Reverse().ToArray());
        source.Add(source[0]);

        List<PredictedCard> baseline = [.. source];
        List<PredictedCard> optimized = [.. source];
        var baselineRng = simulator.Rng.Shuffle.Clone();
        var optimizedRng = simulator.Rng.Shuffle.Clone();
        int sourceCounter = simulator.Rng.Shuffle.Counter();

        baseline.StableShuffle(baselineRng);
        CombatBeamSolver.StableShuffleProjection(optimized, optimizedRng);
        if (!baseline.SequenceEqual(optimized)
            || baselineRng.Counter() != optimizedRng.Counter()
            || simulator.Rng.Shuffle.Counter() != sourceCounter)
        {
            throw new InvalidOperationException(
                "投影洗牌的卡牌顺序、RNG 消耗或原 RNG 隔离与 StableShuffle 不等价。");
        }
    }

    private static void AssertPredictionForkContextIdentityIndex()
    {
        using (PredictionForkContext small = new())
        {
            for (int index = 0; index < 32; index++)
            {
                ForkIdentityProbe source = new(index);
                ForkIdentityProbe fork = new(index);
                small.Register(source, fork);
                if (!ReferenceEquals(small.RequireRemap(source), fork))
                    throw new InvalidOperationException("PredictionForkContext 线性映射不正确。");
            }
        }

        const int count = 512;
        ForkIdentityProbe[] sources = new ForkIdentityProbe[count];
        ForkIdentityProbe[] forks = new ForkIdentityProbe[count];
        using PredictionForkContext indexed = new();
        for (int index = 0; index < count; index++)
        {
            // All probes deliberately compare equal through their virtual equality members.
            // Prediction forks must nevertheless be keyed strictly by object identity.
            sources[index] = new ForkIdentityProbe(1);
            forks[index] = new ForkIdentityProbe(1);
            indexed.Register(sources[index], forks[index]);
        }
        FieldInfo bucketsField = typeof(PredictionForkContext).GetField(
            "_buckets",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(PredictionForkContext).FullName, "_buckets");
        if (bucketsField.GetValue(indexed) is not int[])
            throw new InvalidOperationException("PredictionForkContext 大映射没有启用身份哈希索引。");
        for (int index = 0; index < count; index++)
        {
            if (!indexed.TryRemap(sources[index], out ForkIdentityProbe? mapped)
                || !ReferenceEquals(mapped, forks[index])
                || !ReferenceEquals(indexed.RemapOrSelf(sources[index]), forks[index]))
            {
                throw new InvalidOperationException("PredictionForkContext 身份哈希扩容后映射不正确。");
            }
        }

        indexed.Register(sources[0], forks[0]);
        ForkIdentityProbe equalButUnknown = new(1);
        if (indexed.TryRemap(equalButUnknown, out ForkIdentityProbe? unexpected)
            || unexpected is not null
            || !ReferenceEquals(indexed.RemapOrSelf(equalButUnknown), equalButUnknown))
        {
            throw new InvalidOperationException("PredictionForkContext 错把值相等对象当成同一引用。");
        }
        try
        {
            indexed.Register(sources[0], new ForkIdentityProbe(1));
            throw new InvalidOperationException("PredictionForkContext 接受了同一源对象的不同 Fork。");
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains("was forked twice", StringComparison.Ordinal))
        {
        }
    }

    private sealed class ForkIdentityProbe(int equalityKey)
    {
        private int EqualityKey { get; } = equalityKey;

        public override bool Equals(object? obj)
            => obj is ForkIdentityProbe other && EqualityKey == other.EqualityKey;

        public override int GetHashCode()
            => EqualityKey;
    }

    private static void AssertForkableListEnumeration()
    {
        ForkableList<int> parent = new([1, 2, 3]);
        List<int>.Enumerator concreteEnumerator = parent.GetEnumerator();
        List<int> concreteValues = [];
        while (concreteEnumerator.MoveNext())
            concreteValues.Add(concreteEnumerator.Current);
        concreteEnumerator.Dispose();
        if (!concreteValues.SequenceEqual([1, 2, 3]))
            throw new InvalidOperationException("ForkableList 具体 enumerator 顺序不正确。");

        ForkableList<int> child = parent.Fork();
        child.Add(4);
        parent.Remove(1);
        if (!parent.SequenceEqual([2, 3])
            || !child.SequenceEqual([1, 2, 3, 4])
            || !((IEnumerable<int>)parent).SequenceEqual([2, 3])
            || !((IEnumerable)parent).Cast<int>().SequenceEqual([2, 3]))
        {
            throw new InvalidOperationException("ForkableList 枚举或 COW 父子隔离不正确。");
        }
    }

    private static void AssertChoiceRiskScopedToCardPlay(
        CombatPredictionSimulator simulator,
        CardModel card)
    {
        using (simulator.PushActionSource(card, PredictionActionKind.CardPlay))
        {
            simulator.History.RecordRisk(PredictionRiskReason.UnresolvedPlayerChoice);
            if (!CardSelectionCardMirrors.HasUnresolvedChoiceInCurrentAction(simulator))
                throw new InvalidOperationException("同一次出牌没有识别自己尚未解决的选牌。");
        }
        using (simulator.PushActionSource(card, PredictionActionKind.CardPlay))
        {
            if (CardSelectionCardMirrors.HasUnresolvedChoiceInCurrentAction(simulator))
                throw new InvalidOperationException("上一轮同一卡牌的选牌错误污染了新的出牌动作。");
        }
    }

    private static void AssertRitsuCapabilityFastPath(
        CombatPredictionSimulator simulator,
        Player player,
        CardModel liveCard)
    {
        PredictedCard card = simulator.State.GetPlayerCombatState(player).FindCard(liveCard)
            ?? throw new InvalidOperationException("Ritsu capability 快通道测试找不到预测卡牌。");
        CardModel preview = card.MutablePreview;
        using IDisposable isolation = SimulationNotificationIsolation.Enter();
        if (!RitsuEmptyCapabilityFastPath.CanSkip(preview))
            throw new InvalidOperationException("无 capability 卡牌没有进入 Ritsu 空路径。");
        AssertRitsuDefaultCapabilityRegistrationInvalidatesCache(preview);

        CardType overrideType = preview.Type == CardType.Attack ? CardType.Curse : CardType.Attack;
        TestCardTypeCapability capability = new(overrideType);
        ModelCapabilitySet capabilities = ModelCapabilities.Get(preview);
        List<IModelCapability> attached = (List<IModelCapability>)(typeof(ModelCapabilitySet).GetField(
                "_capabilities",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(capabilities)
            ?? throw new MissingFieldException(typeof(ModelCapabilitySet).FullName, "_capabilities"));
        FieldInfo attachedSnapshot = typeof(ModelCapabilitySet).GetField(
            "_attachedSnapshot",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(ModelCapabilitySet).FullName, "_attachedSnapshot");
        capability.Attach(preview, isInternal: true);
        attached.Add(capability);
        attachedSnapshot.SetValue(capabilities, null);
        try
        {
            if (RitsuEmptyCapabilityFastPath.CanSkip(preview) || preview.Type != overrideType)
                throw new InvalidOperationException("有 capability 卡牌没有保留 Ritsu 属性贡献逻辑。");
        }
        finally
        {
            attached.Remove(capability);
            capability.Detach(isInternal: true);
            attachedSnapshot.SetValue(capabilities, null);
        }
    }

    private static void AssertRitsuDefaultCapabilityRegistrationInvalidatesCache(
        AbstractModel cachedModel)
    {
        lock (RitsuDefaultCapabilityRegistrationTestLock)
        {
            if (_ritsuDefaultCapabilityRegistrationTestCompleted)
                return;

            int generationBefore =
                RitsuEmptyCapabilityFastPath.DefaultCapabilitySourceGenerationForTesting;
            Type cachedModelType = cachedModel.GetType();
            if (!RitsuEmptyCapabilityFastPath.HasCachedDefaultCapabilitySourceGenerationForTesting(
                    cachedModelType,
                    generationBefore))
            {
                throw new InvalidOperationException("Ritsu 默认 capability 注册测试缺少旧缓存条目。");
            }

            RegisterRitsuDefaultCapabilityCacheProbe();

            int generationAfter =
                RitsuEmptyCapabilityFastPath.DefaultCapabilitySourceGenerationForTesting;
            if (unchecked(generationAfter - generationBefore) != 1)
                throw new InvalidOperationException("Ritsu 默认 capability 注册没有推进缓存 generation。");
            if (RitsuEmptyCapabilityFastPath.HasCachedDefaultCapabilitySourceGenerationForTesting(
                    cachedModelType,
                    generationAfter))
            {
                throw new InvalidOperationException("Ritsu 默认 capability 注册后没有清理旧类型缓存。");
            }
            if (!RitsuEmptyCapabilityFastPath.CanSkip(cachedModel)
                || !RitsuEmptyCapabilityFastPath.HasCachedDefaultCapabilitySourceGenerationForTesting(
                    cachedModelType,
                    generationAfter))
            {
                throw new InvalidOperationException("Ritsu 默认 capability 注册后没有按新 generation 重建缓存。");
            }

            _ritsuDefaultCapabilityRegistrationTestCompleted = true;
        }
    }

    private static void RegisterRitsuDefaultCapabilityCacheProbe()
    {
        Type defaults = typeof(ModelCapabilities).Assembly.GetType(
            "STS2RitsuLib.Models.Capabilities.ModelCapabilityDefaults")
            ?? throw new TypeLoadException(
                "STS2RitsuLib.Models.Capabilities.ModelCapabilityDefaults");
        MethodInfo modify = defaults.GetMethod(
            "Modify",
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            types:
            [
                typeof(string),
                typeof(string),
                typeof(Type),
                typeof(Action<AbstractModel, ModelCapabilityList>),
                typeof(int),
            ],
            modifiers: null)
            ?? throw new MissingMethodException(defaults.FullName, "Modify");
        Action<AbstractModel, ModelCapabilityList> noOpModifier = static (_, _) => { };
        modify.Invoke(
            null,
            [
                Entry.ModId,
                "unattended_default_capability_cache_probe",
                typeof(RitsuDefaultCapabilityCacheProbeModel),
                noOpModifier,
                0,
            ]);
    }

    private static void AssertChoiceKeyCache(
        CombatPredictionSimulator simulator,
        Player player,
        CardModel liveCard)
    {
        PredictedCard card = simulator.State.GetPlayerCombatState(player).FindCard(liveCard)
            ?? throw new InvalidOperationException("选牌键缓存测试找不到预测卡牌。");
        string originalKey = CardChoiceSupport.ChoiceCardKey(card);
        CombatPredictionSimulator fork = simulator.Fork();
        PredictedCard forkedCard = fork.State.GetPlayerCombatState(player).FindCard(liveCard)
            ?? throw new InvalidOperationException("选牌键缓存测试找不到 Fork 卡牌。");
        if (!forkedCard.TryGetCachedChoiceKey(out string forkedCachedKey)
            || forkedCachedKey != originalKey)
        {
            throw new InvalidOperationException("选牌键缓存没有直接跨 Fork 复制。");
        }
        if (CardChoiceSupport.ChoiceCardKey(forkedCard) != originalKey)
            throw new InvalidOperationException("选牌键缓存没有跨 Fork 保留。");

        bool originalExhaust = forkedCard.Preview.ExhaustOnNextPlay;
        forkedCard.MutablePreview.ExhaustOnNextPlay = !originalExhaust;
        if (CardChoiceSupport.ChoiceCardKey(forkedCard) == originalKey)
            throw new InvalidOperationException("选牌键缓存没有在卡牌变更后失效。");
        if (CardChoiceSupport.ChoiceCardKey(card) != originalKey)
            throw new InvalidOperationException("选牌键缓存在 Fork 变更后泄漏到父状态。");
    }

    private sealed class TestCardTypeCapability(CardType type)
        : IModelCapability, ICardPropertyContributor
    {
        public string CapabilityId => "combat_solver_test_card_type";
        public AbstractModel? Owner { get; private set; }

        public void Attach(AbstractModel owner, bool isInternal = false)
            => Owner = owner;

        public void Detach(bool isInternal = false)
            => Owner = null;

        public CardType? GetCardType(CardModel card)
            => type;
    }

    private abstract class RitsuDefaultCapabilityCacheProbeModel : AbstractModel;

    private static void AssertForkRejected(
        CombatPredictionSimulator simulator,
        string expectedMessage)
    {
        try
        {
            simulator.Fork();
            throw new InvalidOperationException($"Fork 边界未拒绝：{expectedMessage}。");
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase))
        {
        }
    }
}
