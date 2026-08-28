using System;

namespace CombatSolver.Nosl;

internal enum NoslKnowledgeField
{
    CurrentHand,
    DrawPileContents,
    DiscardPileContents,
    PlayerHealth,
    PlayerStatuses,
    Potions,
    VisibleMonsterIntent,
    FutureMonsterIntentFromVisibleOracle,
    RunSeed,
    RngCounter,
    RngState,
    DrawPileOrder,
    FutureDraw,
    FutureRandomTarget,
    FutureGeneratedCard,
    FutureRandomCost
}

internal enum NoslObservationSource
{
    LiveVisibleState,
    RulesDerived,
    UserVisibleIntentOracle,
    HiddenEngineState
}

internal static class NoslKnowledgePolicy
{
    public static bool IsAllowed(NoslKnowledgeField field)
    {
        return field is NoslKnowledgeField.CurrentHand
            or NoslKnowledgeField.DrawPileContents
            or NoslKnowledgeField.DiscardPileContents
            or NoslKnowledgeField.PlayerHealth
            or NoslKnowledgeField.PlayerStatuses
            or NoslKnowledgeField.Potions
            or NoslKnowledgeField.VisibleMonsterIntent
            or NoslKnowledgeField.FutureMonsterIntentFromVisibleOracle;
    }

    public static void RequireAllowed(NoslKnowledgeField field, NoslObservationSource source)
    {
        if (!IsAllowed(field))
        {
            throw new InvalidOperationException($"NOSL forbids reading {field}.");
        }
        if (source == NoslObservationSource.HiddenEngineState)
        {
            throw new InvalidOperationException($"NOSL forbids obtaining {field} from hidden engine state.");
        }
        if (field == NoslKnowledgeField.FutureMonsterIntentFromVisibleOracle && source != NoslObservationSource.UserVisibleIntentOracle)
        {
            throw new InvalidOperationException("Future monster intent is allowed only when supplied by the user-visible intent oracle; it may not be reconstructed from MonsterAi RNG.");
        }
    }
}
