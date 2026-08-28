using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using CombatSolver.Engine.Common;

namespace CombatSolver.Engine.InCombat.Mirrors.Hooks.Death;

internal static class FairyInABottleMirrors
{
    public static bool ShouldDie(FairyInABottle potion, ShouldDieMirrorContext context)
    {
        if (context.Creature == potion.Owner.Creature)
        {
            return GetState(potion, context).WasUsed;
        }

        return true;
    }

    public static void AfterPreventingDeath(
        FairyInABottle potion,
        AfterPreventingDeathMirrorContext context)
    {
        GetState(potion, context).WasUsed = true;
        if (context.CombatState is not ICombatPredictionEffectSink effects)
            throw new InvalidOperationException("瓶中仙女结算缺少可写的预测状态。");
        effects.ConsumePotion(potion);
        int maxHp = context.State.GetCreature(context.Creature).MaxHp;
        context.Simulator.Heal(context.Creature, Math.Max(1m, maxHp * 0.3m));
    }

    private static State GetState(FairyInABottle potion, CombatMirrorContext context)
    {
        return context.StateStore.Get<State>(potion);
    }

    private sealed class State : IPredictionStateForkable
    {
        public bool WasUsed { get; set; }

        public object Fork(PredictionForkContext context) => MemberwiseClone();
    }
}

internal static class LizardTailMirrors
{
    public static bool ShouldDieLate(LizardTail relic, ShouldDieMirrorContext context)
    {
        if (context.Creature == relic.Owner.Creature)
        {
            return GetState(relic, context).WasUsed;
        }

        return true;
    }

    public static void AfterPreventingDeath(LizardTail relic, AfterPreventingDeathMirrorContext context)
    {
        GetState(relic, context).WasUsed = true;

        int maxHp = context.State.GetCreature(context.Creature).MaxHp;
        var amount = Math.Max(1m, maxHp * (relic.DynamicVars.Heal.BaseValue / 100m));
        context.Simulator.Heal(context.Creature, amount);
    }

    private static LizardTailPredictionState GetState(LizardTail relic, CombatMirrorContext context)
        => context.StateStore.Get(relic, () => new LizardTailPredictionState(relic));
}

internal sealed class LizardTailPredictionState(LizardTail relic) : IPredictionStateForkable
{
    public bool WasUsed { get; set; } = relic.WasUsed;

    public object Fork(PredictionForkContext context) => MemberwiseClone();
}
