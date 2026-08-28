using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors;

namespace CombatSolver.Engine.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    /// <summary>
    /// Mirrors <see cref="CardCmd.AutoPlay"/>.
    /// </summary>
    public bool AutoPlay(
        PredictedCard card,
        Creature? target = null,
        AutoPlayType type = AutoPlayType.Default,
        bool skipXCapture = false)
    {
        if (IsOverOrEnding || State.GetCreature(card.Preview.Owner.Creature).IsDead)
        {
            return false;
        }

        if (card.HasKeyword(State, CardKeyword.Unplayable) ||
            !HookMirrors.ShouldPlay(this, card, out _, type) ||
            !TryResolveAutoPlayTarget(card, ref target))
        {
            MoveToResultPileWithoutPlaying(card);
            return false;
        }

        if (card.GetPile(State) is null)
        {
            AddToPile(card, PileType.Play);
        }

        int historyEntryStart = History.Entries.Count;
        int ownerBlockBefore = State.GetCreature(card.Preview.Owner.Creature).Block;
        // Game 0.111.0 has no vanilla BeforeCardAutoPlayed listeners; the hook catalog will expose any future addition.
        var resources = SpendResources(card, isAutoPlay: true, skipXCapture);
        OnPlayWrapper(card, target, isAutoPlay: true, resources, out _);
        if (History.Entries.Skip(historyEntryStart)
            .OfType<CombatPredictionCardPlayStartedEntry>()
            .Any(entry => ReferenceEquals(entry.Card, card))
            && State.CombatState is ICombatPredictionCardExecutionSink sink)
        {
            sink.CompleteCardExecution(this, card, target, ownerBlockBefore, historyEntryStart);
        }
        return true;
    }

    public bool PaidAutoPlay(
        PredictedCard card,
        Creature? target = null)
    {
        SpendResources(card, isAutoPlay: false);
        return AutoPlay(card, target, AutoPlayType.Default, skipXCapture: true);
    }

    public bool ResolveNestedAutoPlayChoice(PredictedCard card, string sourceId)
        => State.CombatState is not ICombatPredictionNestedChoiceSink sink
            || sink.ResolveNestedCardChoice(this, card, sourceId);

    /// <summary>
    /// Mirrors <see cref="CardPileCmd.AutoPlayFromDrawPile"/>.
    /// </summary>
    public void AutoPlayFromDrawPile(
        Player player,
        int count,
        CardPilePosition position,
        bool forceExhaust = false)
    {
        if (IsOverOrEnding)
        {
            return;
        }

        using IDisposable? scope = (State.CombatState as ICombatPredictionCardExecutionSink)
            ?.BeginCardExecutionScope();
        foreach (var card in MoveCardsForAutoPlay(player, count, position))
        {
            if (State.GetCreature(card.Preview.Owner.Creature).IsDead)
            {
                break;
            }

            card.MutablePreview.ExhaustOnNextPlay = forceExhaust;
            if (AutoPlay(card)
                && !ResolveNestedAutoPlayChoice(card, card.Preview.Id.Entry))
            {
                break;
            }
        }
    }

    // Mirrors CardPileCmd.AutoPlayFromDrawPile until the card is moved to the play pile.
    internal IReadOnlyList<PredictedCard> MoveCardsForAutoPlay(
        Player player,
        int count,
        CardPilePosition position)
    {
        var cards = new List<PredictedCard>(count);
        var playerCombatState = State.GetPlayerCombatState(player);
        var drawPile = playerCombatState.DrawPile;

        for (var i = 0; i < count; i++)
        {
            ShuffleIfNecessary(player);
            if (State.CombatState is ICombatPredictionPendingChoiceState { HasPendingChoice: true })
                break;
            var card = position switch
            {
                CardPilePosition.Top => drawPile.TopCard,
                CardPilePosition.Bottom => drawPile.BottomCard,
                CardPilePosition.Random => Rng.CombatCardSelection.NextItem(drawPile.Cards),
                _ => null
            };

            if (card is null)
            {
                break;
            }

            cards.Add(card);
            AddToPile(card, playerCombatState.PlayPile);
            History.AutoPlayFromDrawPile(card);
        }

        return cards;
    }

    // Mirrors the logic in CardCmd.AutoPlay for resolving a target when none is provided.
    private bool TryResolveAutoPlayTarget(PredictedCard card, ref Creature? target)
    {
        switch (card.Preview.TargetType)
        {
            case TargetType.AnyEnemy:
                target ??= Rng.CombatTargets.NextItem(State.HittableEnemies);
                return target != null;

            case TargetType.AnyAlly:
                target ??= Rng.CombatTargets.NextItem(State.Allies.Where(ally =>
                    ally.IsPlayer && ally != card.Preview.Owner.Creature && State.GetCreature(ally).IsAlive));
                return target != null;

            default:
                return true;
        }
    }
}
