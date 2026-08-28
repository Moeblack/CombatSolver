using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Models.Capabilities;
using System.Reflection;
using CombatSolver.Engine.Common;
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

        Vambrace vambraceRelic = ModelDb.All.OfType<Vambrace>().Single();
        VambracePredictionState vambrace = simulator.StateStore.Get(
            (AbstractModel)vambraceRelic,
            () => new VambracePredictionState(vambraceRelic));
        vambrace.TriggeringCard = card;
        AssertForkRejected(simulator, "Vambrace");
        vambrace.TriggeringCard = null;

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
