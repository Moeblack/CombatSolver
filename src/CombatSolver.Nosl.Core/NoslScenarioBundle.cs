using System;
using System.Collections.Generic;
using System.Linq;

namespace CombatSolver.Nosl;

internal sealed record NoslObservedState<TState>(string ObservationKey, TState State) where TState : notnull;

internal sealed record NoslObservationBundle<TState>(
    string ObservationKey,
    double Probability,
    NoslScenarioBundle<TState> ConditionalBundle) where TState : notnull;

/// <summary>
/// A belief over possible simulated states that share one currently observable information set.
/// Decisions are evaluated across the whole bundle; an observation is the only operation that
/// may split it into different future decision sets.
/// </summary>
internal sealed class NoslScenarioBundle<TState> where TState : notnull
{
    public string PublicInformationKey { get; }

    public NoslDistribution<TState> Scenarios { get; }

    public NoslScenarioBundle(string publicInformationKey, NoslDistribution<TState> scenarios)
    {
        if (string.IsNullOrWhiteSpace(publicInformationKey))
        {
            throw new ArgumentException("A scenario bundle must have a public information key.", nameof(publicInformationKey));
        }
        PublicInformationKey = publicInformationKey;
        Scenarios = scenarios ?? throw new ArgumentNullException(nameof(scenarios));
    }

    public NoslActionCandidate<TAction> EvaluateSharedAction<TAction>(
        string publicActionKey,
        TAction action,
        Func<TState, TAction, NoslDistribution<NoslUtilityOutcome>> transition) where TAction : notnull
    {
        if (string.IsNullOrWhiteSpace(publicActionKey))
        {
            throw new ArgumentException("An action must have a public key.", nameof(publicActionKey));
        }
        ArgumentNullException.ThrowIfNull(transition);
        NoslDistribution<NoslUtilityOutcome> outcomes = Scenarios.Bind(state => transition(state, action));
        return new NoslActionCandidate<TAction>(publicActionKey, action, outcomes);
    }

    public IReadOnlyList<NoslObservationBundle<TNextState>> Observe<TNextState>(
        Func<TState, NoslDistribution<NoslObservedState<TNextState>>> transition) where TNextState : notnull
    {
        ArgumentNullException.ThrowIfNull(transition);
        NoslDistribution<NoslObservedState<TNextState>> joint = Scenarios.Bind(transition, NoslObservedStateComparer<TNextState>.Instance);
        List<NoslObservationBundle<TNextState>> bundles = new List<NoslObservationBundle<TNextState>>();
        foreach (IGrouping<string, NoslWeightedOutcome<NoslObservedState<TNextState>>> group in joint.Outcomes
            .GroupBy(outcome => outcome.Value.ObservationKey, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(group.Key))
            {
                throw new InvalidOperationException("An observable transition produced an empty observation key.");
            }
            double probability = group.Sum(outcome => outcome.Probability);
            NoslDistribution<TNextState> conditional = NoslDistribution<TNextState>.Create(
                group.Select(outcome => new NoslWeightedOutcome<TNextState>(outcome.Value.State, outcome.Probability / probability)));
            string successorKey = PublicInformationKey + "/obs:" + group.Key.Length + ":" + group.Key;
            bundles.Add(new NoslObservationBundle<TNextState>(
                group.Key,
                probability,
                new NoslScenarioBundle<TNextState>(successorKey, conditional)));
        }
        return bundles;
    }
}

internal sealed class NoslObservedStateComparer<TState> : IEqualityComparer<NoslObservedState<TState>> where TState : notnull
{
    public static NoslObservedStateComparer<TState> Instance { get; } = new NoslObservedStateComparer<TState>();

    public bool Equals(NoslObservedState<TState>? x, NoslObservedState<TState>? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }
        return x != null
            && y != null
            && string.Equals(x.ObservationKey, y.ObservationKey, StringComparison.Ordinal)
            && EqualityComparer<TState>.Default.Equals(x.State, y.State);
    }

    public int GetHashCode(NoslObservedState<TState> obj)
    {
        return HashCode.Combine(StringComparer.Ordinal.GetHashCode(obj.ObservationKey), EqualityComparer<TState>.Default.GetHashCode(obj.State));
    }
}
