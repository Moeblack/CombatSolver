using System;
using System.Collections.Generic;
using System.Linq;

namespace CombatSolver.Nosl;

/// <summary>
/// The subset of a card's public semantics needed by the inexpensive hand advisor.  Values are
/// deliberately estimates, not a replacement for the game's card resolver.
/// </summary>
internal sealed record NoslHandCard
{
    public string PublicKey { get; }

    public int EnergyCost { get; }

    public double Damage { get; }

    public double Block { get; }

    public int CardsDrawn { get; }

    public int EnergyGained { get; }

    public bool IsPlayable { get; }

    public NoslHandCard(
        string publicKey,
        int energyCost,
        double damage = 0.0,
        double block = 0.0,
        int cardsDrawn = 0,
        int energyGained = 0,
        bool isPlayable = true)
    {
        if (string.IsNullOrWhiteSpace(publicKey))
        {
            throw new ArgumentException("A hand card requires a non-empty public key.", nameof(publicKey));
        }
        if (energyCost < 0 || cardsDrawn < 0 || energyGained < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(energyCost), "Card costs, draws, and gains must be non-negative.");
        }
        if (!double.IsFinite(damage) || damage < 0.0 || !double.IsFinite(block) || block < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(damage), "Card damage and block must be finite and non-negative.");
        }
        PublicKey = publicKey;
        EnergyCost = energyCost;
        Damage = damage;
        Block = block;
        CardsDrawn = cardsDrawn;
        EnergyGained = energyGained;
        IsPlayable = isPlayable;
    }
}

internal sealed record NoslHandAction
{
    public string PublicActionKey { get; }

    public int? CardIndex { get; }

    public bool EndsTurn => !CardIndex.HasValue;

    public NoslHandAction(string publicActionKey, int? cardIndex)
    {
        if (string.IsNullOrWhiteSpace(publicActionKey))
        {
            throw new ArgumentException("A hand action requires a non-empty public key.", nameof(publicActionKey));
        }
        if (cardIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cardIndex));
        }
        PublicActionKey = publicActionKey;
        CardIndex = cardIndex;
    }
}

internal sealed record NoslHandHeuristicOptions(
    double DamageWeight = 1.0,
    double BlockWeight = 1.0,
    double IncomingDamageWeight = 1.5,
    double FutureCardWeight = 0.35)
{
    public NoslHandHeuristicOptions Validate()
    {
        if (!double.IsFinite(DamageWeight) || DamageWeight < 0.0 ||
            !double.IsFinite(BlockWeight) || BlockWeight < 0.0 ||
            !double.IsFinite(IncomingDamageWeight) || IncomingDamageWeight < 0.0 ||
            !double.IsFinite(FutureCardWeight) || FutureCardWeight < 0.0 || FutureCardWeight > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(NoslHandHeuristicOptions), "Hand heuristic weights must be finite and non-negative.");
        }
        return this;
    }
}

internal sealed record NoslHandHeuristicResult(
    NoslActionRecommendation<NoslHandAction> Recommendation,
    IReadOnlyList<NoslActionCandidate<NoslHandAction>> Candidates);

/// <summary>
/// A deterministic public-hand advisor.  A caller supplies an explicit distribution for the next
/// visible draw; the advisor never manufactures a seed or reads an engine random stream.
/// </summary>
internal static class NoslHandHeuristic
{
    public static NoslHandHeuristicResult Recommend(
        IReadOnlyList<NoslHandCard> hand,
        int energy,
        int incomingDamage,
        int playerHealth,
        int playerBlock = 0,
        NoslDistribution<NoslHandCard>? nextDraw = null,
        NoslHandHeuristicOptions? options = null,
        NoslRiskPolicy? riskPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(hand);
        if (energy < 0 || incomingDamage < 0 || playerHealth < 0 || playerBlock < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(energy), "Public combat resources cannot be negative.");
        }
        NoslHandHeuristicOptions settings = (options ?? new NoslHandHeuristicOptions()).Validate();
        NoslHandCard[] cards = hand.ToArray();
        List<NoslActionCandidate<NoslHandAction>> candidates = new List<NoslActionCandidate<NoslHandAction>>(cards.Length + 1);
        NoslHandAction endTurn = new NoslHandAction("end-turn", null);
        candidates.Add(new NoslActionCandidate<NoslHandAction>(
            endTurn.PublicActionKey,
            endTurn,
            NoslDistribution<NoslUtilityOutcome>.Certain(EvaluateOutcome(
                damage: 0.0,
                block: playerBlock,
                incomingDamage,
                playerHealth,
                catastrophic: false,
                settings))));

        for (int index = 0; index < cards.Length; index++)
        {
            NoslHandCard card = cards[index];
            if (!card.IsPlayable || card.EnergyCost > energy)
            {
                continue;
            }
            NoslHandAction action = new NoslHandAction(ActionKey(card, index), index);
            if (card.CardsDrawn > 0 && nextDraw == null)
            {
                throw new InvalidOperationException(
                    $"Card '{card.PublicKey}' draws a random card, but no explicit NOSL draw distribution was supplied.");
            }
            NoslDistribution<NoslHandCard> chance = card.CardsDrawn > 0
                ? nextDraw!
                : NoslDistribution<NoslHandCard>.Certain(new NoslHandCard("no-future-card", 0));
            NoslDistribution<NoslUtilityOutcome> outcomes = chance.Map(drawn =>
            {
                int remainingEnergy = energy - card.EnergyCost + card.EnergyGained;
                NoslHandCard? followUp = card.CardsDrawn > 0
                    ? BestFollowUp(cards, drawn, index, remainingEnergy, settings)
                    : null;
                double damage = card.Damage + (followUp?.Damage ?? 0.0) * settings.FutureCardWeight;
                double block = playerBlock + card.Block + (followUp?.Block ?? 0.0) * settings.FutureCardWeight;
                int unblocked = Math.Max(0, incomingDamage - (int)Math.Floor(block));
                bool catastrophic = unblocked >= playerHealth && playerHealth > 0;
                return EvaluateOutcome(damage, block, incomingDamage, playerHealth, catastrophic, settings);
            });
            candidates.Add(new NoslActionCandidate<NoslHandAction>(action.PublicActionKey, action, outcomes));
        }

        NoslActionRecommendation<NoslHandAction> recommendation = NoslDecisionPolicy.Recommend(candidates, riskPolicy);
        return new NoslHandHeuristicResult(recommendation, candidates);
    }

    private static NoslHandCard? BestFollowUp(
        IReadOnlyList<NoslHandCard> hand,
        NoslHandCard drawn,
        int currentIndex,
        int energy,
        NoslHandHeuristicOptions options)
    {
        IEnumerable<NoslHandCard> choices = hand
            .Where((card, index) => index != currentIndex)
            .Append(drawn)
            .Where(card => card.IsPlayable && card.EnergyCost <= energy);
        return choices
            .OrderByDescending(card => card.Damage * options.DamageWeight + card.Block * options.BlockWeight)
            .ThenBy(card => card.EnergyCost)
            .ThenBy(card => card.PublicKey, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static NoslUtilityOutcome EvaluateOutcome(
        double damage,
        double block,
        int incomingDamage,
        int playerHealth,
        bool catastrophic,
        NoslHandHeuristicOptions options)
    {
        int unblocked = Math.Max(0, incomingDamage - (int)Math.Floor(block));
        bool lethal = playerHealth > 0 && unblocked >= playerHealth;
        double utility = damage * options.DamageWeight
            + block * options.BlockWeight
            - unblocked * options.IncomingDamageWeight;
        return new NoslUtilityOutcome(utility, catastrophic || lethal);
    }

    private static string ActionKey(NoslHandCard card, int index)
    {
        return $"play:{index}:{card.PublicKey}";
    }
}
