using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using CombatSolver.Engine.InCombat.Simulation;

namespace CombatSolver;

internal static class MonsterMoveSemantics
{
    public static bool ApplyForecastMove(
        CombatPredictionSimulator simulator,
        SimulatedCombatState combat,
        ForecastMove move,
        Creature player,
        IReadOnlyList<PlanCardChoice>? plannedChoices = null)
    {
        SimCreatureState simulatedPlayer = simulator.State.GetCreature(player);
        MonsterMoveEffects.ApplyBeforeAttack(simulator, combat, move, player);
        bool fullyBlockedAttack = false;
        int suckTriggeredHits = 0;
        bool consumedVigor = move.AttackHits.Count > 0 && combat.GetAmount<VigorPower>(move.Owner) > 0;
        foreach (ForecastAttackHit hit in move.AttackHits)
        {
            int baseDamage = combat.AdjustMonsterMoveDamage(move.Owner, move.Move.Id, hit.BaseDamage);
            IReadOnlyList<DamageResult> results = DamagePlayer(
                simulator,
                combat,
                move.Owner,
                player,
                baseDamage);
            foreach (DamageResult result in results)
            {
                if (ReferenceEquals(result.Receiver, player) && result.WasFullyBlocked)
                    fullyBlockedAttack = true;
            }
            HashSet<Creature> petOwners = results
                .Where(result => result.Receiver.IsPet)
                .Select(result => result.Receiver.PetOwner?.Creature)
                .OfType<Creature>()
                .ToHashSet();
            if (results.Any(result => result.UnblockedDamage > 0 && !petOwners.Contains(result.Receiver)))
                suckTriggeredHits++;
            if (simulatedPlayer.IsDead)
                return true;
        }

        if (consumedVigor)
            combat.SetAmount<VigorPower>(move.Owner, 0);
        if (fullyBlockedAttack && combat.GetAmount<ImbalancedPower>(move.Owner) > 0)
        {
            if (move.Owner.Monster is BowlbugRock)
                combat.ForceStunnedMove(move.Owner, "HEADBUTT_MOVE");
            combat.StunNextMove(move.Owner);
        }
        int suck = combat.GetAmount<SuckPower>(move.Owner);
        if (suck > 0 && suckTriggeredHits > 0)
            combat.Apply<StrengthPower>(move.Owner, suck * suckTriggeredHits, move.Owner);

        MonsterMoveEffects.Apply(simulator, combat, move, player, plannedChoices);
        simulator.SynchronizePowerAmountPredictionStates();
        PowerLifecycleSupport.ResolvePowerAmountChanges(simulator, combat);
        combat.NormalizeAeonglassWithers(simulator);
        combat.NormalizeCardAfflictions(simulator);
        return simulatedPlayer.IsDead;
    }

    public static IReadOnlyList<DamageResult> DamagePlayer(
        CombatPredictionSimulator simulator,
        SimulatedCombatState combat,
        Creature attacker,
        Creature player,
        int baseDamage)
    {
        Creature? osty = player.Player?.Osty;
        int? suppressedDieForYou = null;
        if (osty != null
            && simulator.State.GetCreature(osty).IsDead
            && combat.GetAmount<DieForYouPower>(osty) is > 0 and var amount)
        {
            suppressedDieForYou = amount;
            combat.SetAmount<DieForYouPower>(osty, 0);
        }

        try
        {
            return simulator.Damage(player, baseDamage, ValueProp.Move, attacker);
        }
        finally
        {
            if (suppressedDieForYou is { } restoredAmount)
                combat.SetAmount<DieForYouPower>(osty!, restoredAmount);
        }
    }
}
