using System;
using System.Collections.Generic;
using System.Linq;

namespace CombatSolver.Nosl;

internal readonly record struct NoslUtilityOutcome(double Utility, bool Catastrophic);

internal sealed record NoslActionCandidate<TAction>(string PublicActionKey, TAction Action, NoslDistribution<NoslUtilityOutcome> Outcomes) where TAction : notnull;

internal sealed record NoslActionRecommendation<TAction>(
    string PublicActionKey,
    TAction Action,
    NoslRiskSummary Risk,
    double RiskAdjustedScore) where TAction : notnull;

internal sealed record NoslRiskPolicy(
    double StandardDeviationPenalty = 0.15,
    double LowerTailWeight = 0.35,
    double CatastrophePenalty = 10000.0,
    double WorstQuantile = 0.1,
    double LowerTailMass = 0.1)
{
    public static NoslRiskPolicy Default { get; } = new NoslRiskPolicy();

    public double Score(NoslRiskSummary summary)
    {
        return summary.ExpectedUtility
            - StandardDeviationPenalty * summary.StandardDeviation
            + LowerTailWeight * (summary.LowerTailCvar - summary.ExpectedUtility)
            - CatastrophePenalty * summary.CatastropheProbability;
    }

    public void Validate()
    {
        if (!double.IsFinite(StandardDeviationPenalty) || StandardDeviationPenalty < 0.0 ||
            !double.IsFinite(LowerTailWeight) || LowerTailWeight < 0.0 ||
            !double.IsFinite(CatastrophePenalty) || CatastrophePenalty < 0.0 ||
            !double.IsFinite(WorstQuantile) || WorstQuantile <= 0.0 || WorstQuantile > 1.0 ||
            !double.IsFinite(LowerTailMass) || LowerTailMass <= 0.0 || LowerTailMass > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(NoslRiskPolicy), "NOSL risk policy contains an invalid value.");
        }
    }
}

internal static class NoslDecisionPolicy
{
    /// <summary>
    /// Chooses only from public action/outcome distributions. Hidden seeds and concrete hidden pile
    /// orders cannot be supplied to this API, which enforces information-set policy invariance.
    /// </summary>
    public static NoslActionRecommendation<TAction> Recommend<TAction>(IEnumerable<NoslActionCandidate<TAction>> candidates, NoslRiskPolicy? policy = null) where TAction : notnull
    {
        ArgumentNullException.ThrowIfNull(candidates);
        policy ??= NoslRiskPolicy.Default;
        policy.Validate();
        List<NoslActionRecommendation<TAction>> evaluated = new List<NoslActionRecommendation<TAction>>();
        HashSet<string> actionKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (NoslActionCandidate<TAction> candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.PublicActionKey) || !actionKeys.Add(candidate.PublicActionKey))
            {
                throw new ArgumentException("Public action keys must be non-empty and unique.", nameof(candidates));
            }
            NoslRiskSummary summary = candidate.Outcomes.Summarize(
                outcome => outcome.Utility,
                outcome => outcome.Catastrophic,
                policy.WorstQuantile,
                policy.LowerTailMass);
            evaluated.Add(new NoslActionRecommendation<TAction>(candidate.PublicActionKey, candidate.Action, summary, policy.Score(summary)));
        }
        if (evaluated.Count == 0)
        {
            throw new ArgumentException("At least one action candidate is required.", nameof(candidates));
        }
        return evaluated
            .OrderByDescending(result => result.RiskAdjustedScore)
            .ThenBy(result => result.Risk.CatastropheProbability)
            .ThenByDescending(result => result.Risk.LowerTailCvar)
            .ThenByDescending(result => result.Risk.ExpectedUtility)
            .ThenBy(result => result.PublicActionKey, StringComparer.Ordinal)
            .First();
    }
}
