using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Models.Capabilities;
using System.Reflection;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.Card;
using CombatSolver.Engine.InCombat.Simulation;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private static void AssertForkBoundaries(CombatState combat, Player player)
    {
        CardModel card = player.PlayerCombatState?.Hand.Cards.FirstOrDefault()
            ?? throw new InvalidOperationException("Fork 边界测试要求手牌中至少有一张牌。");
        SimulatedCombatState simulatedCombat = new(combat);
        CombatPredictionSimulator simulator = new(simulatedCombat);
        AssertRitsuCapabilityFastPath(simulator, player, card);
        AssertChoiceRiskScopedToCardPlay(simulator, card);

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
