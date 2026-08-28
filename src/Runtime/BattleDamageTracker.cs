using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CombatSolver;

internal readonly record struct BattleDamageSnapshot(
    int HpLostSoFar,
    int SoldHpCommitted,
    int PotionsUsedSoFar);

internal static class BattleDamageTracker
{
    private static ICombatState? _combat;
    private static int? _lastObservedHp;
    private static int _hpLostSoFar;
    private static int _soldHpCommitted;
    private static int _potionHistoryCountAtStart;
    private static int? _plannedTurn;
    private static int _plannedTurnStartHp;
    private static int _plannedSoldHp;

    public static void Begin(ICombatState? combat)
    {
        Reset();
        _combat = combat;
        _lastObservedHp = GetSinglePlayer(combat)?.Creature.CurrentHp;
        _potionHistoryCountAtStart = CountPotionHistoryEntries();
        Entry.Logger.Info($"[CombatSolver/Test] BATTLE_DAMAGE_RESET start_hp={_lastObservedHp?.ToString() ?? "-"}");
    }

    public static BattleDamageSnapshot Observe(CombatState combat)
    {
        if (!ReferenceEquals(_combat, combat))
            Begin(combat);

        Player? player = GetSinglePlayer(combat);
        if (player == null)
            return new BattleDamageSnapshot(_hpLostSoFar, _soldHpCommitted, PotionsUsedSoFar());

        int currentHp = player.Creature.CurrentHp;
        int turn = player.PlayerCombatState?.TurnNumber ?? -1;
        if (_plannedTurn is int plannedTurn && turn > plannedTurn)
        {
            int actualLoss = Math.Max(0, _plannedTurnStartHp - currentHp);
            int committed = Math.Min(_plannedSoldHp, actualLoss);
            _soldHpCommitted += committed;
            Entry.Logger.Info(
                $"[CombatSolver/Test] BATTLE_SELL_COMMIT turn={plannedTurn} actual_hp_lost={actualLoss} planned_sold_hp={_plannedSoldHp} committed_sold_hp={committed} battle_sold_hp={_soldHpCommitted}");
            ClearPlan();
        }

        if (_lastObservedHp is int previousHp && currentHp < previousHp)
            _hpLostSoFar += previousHp - currentHp;
        _lastObservedHp = currentHp;
        return new BattleDamageSnapshot(_hpLostSoFar, _soldHpCommitted, PotionsUsedSoFar());
    }

    public static void RegisterPlan(CombatState combat, SolverResult result)
    {
        Player? player = GetSinglePlayer(combat);
        if (player?.PlayerCombatState == null)
            return;

        int turn = player.PlayerCombatState.TurnNumber;
        _plannedTurn = turn;
        _plannedTurnStartHp = player.Creature.CurrentHp;
        _plannedSoldHp = result.SoldHpByTurn.GetValueOrDefault(turn);
        Entry.Logger.Info(
            $"[CombatSolver/Test] BATTLE_SELL_PLAN turn={turn} planned_sold_hp={_plannedSoldHp} battle_sold_hp={_soldHpCommitted} battle_hp_lost={_hpLostSoFar}");
    }

    public static void Reset()
    {
        _combat = null;
        _lastObservedHp = null;
        _hpLostSoFar = 0;
        _soldHpCommitted = 0;
        _potionHistoryCountAtStart = 0;
        ClearPlan();
    }

    private static Player? GetSinglePlayer(ICombatState? combat)
        => combat?.Players.Count == 1 ? combat.Players[0] : null;

    private static int PotionsUsedSoFar()
        => Math.Max(0, CountPotionHistoryEntries() - _potionHistoryCountAtStart);

    private static int CountPotionHistoryEntries()
        => CombatManager.Instance.History.Entries.OfType<PotionUsedEntry>().Count();

    private static void ClearPlan()
    {
        _plannedTurn = null;
        _plannedTurnStartHp = 0;
        _plannedSoldHp = 0;
    }
}
