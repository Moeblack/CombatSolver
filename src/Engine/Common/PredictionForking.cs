using System.Buffers;
using MegaCrit.Sts2.Core.Combat;

namespace CombatSolver.Engine.Common;

internal interface ICombatPredictionForkableState : ICombatState
{
    ICombatState Fork(PredictionForkContext context);
}

internal interface ICombatPredictionHookListenerSource
{
    IReadOnlyList<MegaCrit.Sts2.Core.Models.AbstractModel> HookListeners { get; }

    IReadOnlyList<MegaCrit.Sts2.Core.Models.AbstractModel> RunHookListeners { get; }
}

internal interface ICombatPredictionRunSnapshot
{
    MegaCrit.Sts2.Core.Entities.Cards.CardMultiplayerConstraint CardMultiplayerConstraint { get; }

    CombatSolver.Engine.InCombat.Simulation.CombatPredictionRngSet CreatePredictionRngSet();
}

internal interface ICombatPredictionPlayerLimits
{
    int GetMaxHandSize(MegaCrit.Sts2.Core.Entities.Players.Player player);

    int GetPotionSlotCount(MegaCrit.Sts2.Core.Entities.Players.Player player);
}

internal interface ICombatPredictionPlayerCardRules
{
    bool AreCardsFree(MegaCrit.Sts2.Core.Entities.Players.Player player);
}

internal interface ICombatPredictionPetState
{
    MegaCrit.Sts2.Core.Entities.Creatures.Creature? GetOsty(
        MegaCrit.Sts2.Core.Entities.Players.Player player);
}

internal interface ICombatPredictionStateOwner
{
    void AttachPredictionState(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionState predictionState);
}

internal interface ICombatPredictionRootCaptureBoundary
{
    void AssertCanCaptureCreature(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature);

    void AssertCanCapturePlayer(MegaCrit.Sts2.Core.Entities.Players.Player player);
}

internal interface ICombatPredictionRootMaterializable
{
    void MaterializeRoot(CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator);
}

internal interface ICombatPredictionCardEventSink
{
    void RecordCardExhausted(MegaCrit.Sts2.Core.Entities.Creatures.Creature actor);

    void RecordDamageReceived(
        MegaCrit.Sts2.Core.Entities.Creatures.Creature receiver,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature? dealer,
        MegaCrit.Sts2.Core.Entities.Creatures.DamageResult result);

    void RecordCardDiscarded(MegaCrit.Sts2.Core.Entities.Creatures.Creature actor);

    void RecordCreatureAttacked(MegaCrit.Sts2.Core.Entities.Creatures.Creature actor);

    void RecordEnergySpent(MegaCrit.Sts2.Core.Entities.Players.Player player, int amount);

    void AfterEnergySpent(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator,
        CombatSolver.Engine.Common.PredictedCard card,
        int amount);

    void AfterStarsSpent(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator,
        CombatSolver.Engine.Common.PredictedCard card,
        int amount);

    void RecordStarsGained(MegaCrit.Sts2.Core.Entities.Players.Player player, int amount);

    void RecordCardDrawn(MegaCrit.Sts2.Core.Entities.Players.Player player, bool fromHandDraw);

    void AfterCardEnteredCombat(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator,
        CombatSolver.Engine.Common.PredictedCard card);

    void AfterCardRemovedFromCombat(CombatSolver.Engine.Common.PredictedCard card);

    void AfterHandEmptied(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator,
        MegaCrit.Sts2.Core.Entities.Players.Player player);
}

internal interface ICombatPredictionCardExecutionSink
{
    IDisposable BeginCardExecutionScope();

    void RecordCardPlayStarted(CombatSolver.Engine.Common.PredictedCard card);

    void ApplyCardPlayEffects(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator,
        CombatSolver.Engine.Common.PredictedCard card,
        MegaCrit.Sts2.Core.Entities.Cards.CardPlay cardPlay,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature? target,
        int ownerBlockBefore,
        decimal cardBlockGained,
        int historyEntryStart);

    void CompleteCardPlayEffects(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator,
        CombatSolver.Engine.Common.PredictedCard card,
        int ownerBlockBefore,
        int historyEntryStart);

    void CompleteCardExecution(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator);
}

internal interface ICombatPredictionEffectSink
{
    void SummonOsty(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator,
        MegaCrit.Sts2.Core.Entities.Players.Player player,
        int amount);

    void ApplyPower(
        Type powerType,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature target,
        int amount,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier = null);

    void SetPowerAmount(MegaCrit.Sts2.Core.Models.PowerModel power, int amount);

    void SetPowerDynamicVar(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator,
        MegaCrit.Sts2.Core.Models.PowerModel power,
        string key,
        int value);

    bool TryProcurePotion(
        MegaCrit.Sts2.Core.Entities.Players.Player player,
        MegaCrit.Sts2.Core.Models.PotionModel potion);

    void ConsumePotion(MegaCrit.Sts2.Core.Models.PotionModel potion);

    void BeforePotionUsed(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator,
        MegaCrit.Sts2.Core.Models.PotionModel potion,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature? target);

    void AfterPotionUsed(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator,
        MegaCrit.Sts2.Core.Models.PotionModel potion,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature? target);

    void LosePlayerGold(MegaCrit.Sts2.Core.Entities.Players.Player player, int amount);

    void ApplyPowerSkippingNextDurationTick(
        Type powerType,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature target,
        int amount,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier = null);

    void ApplyTemporaryStrengthLoss(
        Type powerType,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature target,
        int amount,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier = null);

    void ApplyTemporaryDexterity(
        Type powerType,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature target,
        int amount,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier = null);

    bool GetRedSkullStrengthApplied(MegaCrit.Sts2.Core.Models.Relics.RedSkull relic);

    void SetRedSkullStrengthApplied(MegaCrit.Sts2.Core.Models.Relics.RedSkull relic, bool value);

    void DoomKill(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator,
        IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> creatures);

    void StunNextMove(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature);

    void ForceStunnedMove(
        MegaCrit.Sts2.Core.Entities.Creatures.Creature creature,
        string? nextMoveId = null);

}

internal interface ICombatPredictionRosterSink
{
    void RemoveCreatureFromPrediction(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature);
}

internal interface ICombatPredictionChoiceSink
{
    bool ResolvePileChoice(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator,
        string sourceId,
        MegaCrit.Sts2.Core.Entities.Players.Player player,
        MegaCrit.Sts2.Core.Entities.Cards.PileType sourcePile,
        int count);
}

internal interface ICombatPredictionPendingChoiceState
{
    bool HasPendingChoice { get; }
}

internal interface ICombatPredictionNestedChoiceSink
{
    bool ResolveNestedCardChoice(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator,
        CombatSolver.Engine.Common.PredictedCard card,
        string sourceId);
}

internal interface ICombatPredictionManualCardChoiceSink
{
    bool ResolveManualCardChoice(
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator simulator,
        CombatSolver.Engine.Common.PredictedCard card);
}

internal interface ICombatPredictionCreatureSemantics
{
    bool IsPrimaryEnemy(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature);

    bool IsHittable(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature);

    bool ShouldRemoveAfterDeath(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature);
}

internal interface ICombatPredictionMonsterStateSink
{
    bool GetMonsterBool(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature, string name);

    int GetAeonglassWitherUpgradeCount(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature);

    void SetMonsterBool(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature, string name, bool value);

    string GetPredictedMoveId(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature);

    string GetNextMoveIdFromStateLog(
        MegaCrit.Sts2.Core.Entities.Creatures.Creature creature,
        MegaCrit.Sts2.Core.Random.Rng rng);

    void ForceMonsterMove(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature, string moveId);
}

internal interface IPredictionStateForkable
{
    object Fork(PredictionForkContext context);
}

internal interface IPredictionForkBoundary
{
    void AssertForkable();
}

internal sealed class PredictionForkContext : IDisposable
{
    private object[] _sources = ArrayPool<object>.Shared.Rent(40);
    private object[] _forks = ArrayPool<object>.Shared.Rent(40);
    private int _count;

    public void Register<T>(T source, T fork)
        where T : class
    {
        if (ReferenceEquals(source, fork))
            return;
        int existingIndex = Find(source);
        if (existingIndex >= 0)
        {
            object existing = _forks[existingIndex];
            if (!ReferenceEquals(existing, fork))
                throw new InvalidOperationException($"Prediction object {source.GetType().FullName} was forked twice.");
            return;
        }
        if (_count == _sources.Length)
        {
            object[] sources = ArrayPool<object>.Shared.Rent(_count * 2);
            object[] forks = ArrayPool<object>.Shared.Rent(_count * 2);
            Array.Copy(_sources, sources, _count);
            Array.Copy(_forks, forks, _count);
            ArrayPool<object>.Shared.Return(_sources, clearArray: true);
            ArrayPool<object>.Shared.Return(_forks, clearArray: true);
            _sources = sources;
            _forks = forks;
        }
        _sources[_count] = source;
        _forks[_count] = fork;
        _count++;
    }

    public T RemapOrSelf<T>(T value)
        where T : class
    {
        int index = Find(value);
        return index >= 0 ? (T)_forks[index] : value;
    }

    public bool TryRemap<T>(T value, out T? fork)
        where T : class
    {
        int index = Find(value);
        if (index >= 0)
        {
            fork = (T)_forks[index];
            return true;
        }
        fork = null;
        return false;
    }

    public T RequireRemap<T>(T value)
        where T : class
    {
        int index = Find(value);
        return index >= 0
            ? (T)_forks[index]
            : throw new InvalidOperationException(
                $"Prediction object {value.GetType().FullName} has no fork mapping.");
    }

    private int Find(object source)
    {
        for (int index = _count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(_sources[index], source))
                return index;
        }
        return -1;
    }

    public void Dispose()
    {
        object[] sources = _sources;
        object[] forks = _forks;
        _sources = [];
        _forks = [];
        _count = 0;
        ArrayPool<object>.Shared.Return(sources, clearArray: true);
        ArrayPool<object>.Shared.Return(forks, clearArray: true);
    }
}
