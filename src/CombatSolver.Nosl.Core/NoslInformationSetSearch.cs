using System;
using System.Collections.Generic;
using System.Linq;

namespace CombatSolver.Nosl;

/// <summary>
/// A candidate action identified by a key that is meaningful in the current public information
/// set.  The action provider used by <see cref="NoslInformationSetSearch"/> receives only that
/// public key and the remaining depth, so it cannot choose a different action for a hidden world.
/// </summary>
internal sealed class NoslSearchAction<TAction> where TAction : notnull
{
    public string PublicActionKey { get; }

    public TAction Action { get; }

    public NoslSearchAction(string publicActionKey, TAction action)
    {
        if (string.IsNullOrWhiteSpace(publicActionKey))
        {
            throw new ArgumentException("A search action must have a non-empty public key.", nameof(publicActionKey));
        }
        ArgumentNullException.ThrowIfNull(action);
        PublicActionKey = publicActionKey;
        Action = action;
    }
}

/// <summary>
/// One explicit game transition.  Every option must carry the observation that a human can see
/// after the transition.  Unobserved random outcomes deliberately use the same public observation
/// key, which keeps them in one successor information set.
/// </summary>
internal sealed class NoslSearchTransition<TState> where TState : notnull
{
    public string PublicObservationKey { get; }

    public TState State { get; }

    public double ImmediateUtility { get; }

    public bool Catastrophic { get; }

    public NoslSearchTransition(
        string PublicObservationKey,
        TState State,
        double ImmediateUtility,
        bool Catastrophic)
    {
        if (string.IsNullOrWhiteSpace(PublicObservationKey))
        {
            throw new ArgumentException("A transition must have a non-empty public observation key.", nameof(PublicObservationKey));
        }
        ArgumentNullException.ThrowIfNull(State);
        if (!double.IsFinite(ImmediateUtility))
        {
            throw new ArgumentOutOfRangeException(nameof(ImmediateUtility), "Transition utility must be finite.");
        }
        this.PublicObservationKey = PublicObservationKey;
        this.State = State;
        this.ImmediateUtility = ImmediateUtility;
        this.Catastrophic = Catastrophic;
    }
}

/// <summary>
/// Resource limits for the generic information-set search.  The first implementation is intended
/// as a bridge for game adapters, so it fails closed on runaway branching instead of silently
/// truncating a distribution.
/// </summary>
internal sealed record NoslSearchLimits(
    int MaxDepth = 3,
    int MaxActionsPerInformationSet = 128,
    int MaxTransitionOutcomesPerAction = 4096,
    int MaxExpandedDecisionNodes = 10000)
{
    public static NoslSearchLimits Default { get; } = new NoslSearchLimits();

    public void Validate()
    {
        if (MaxDepth < 1 || MaxActionsPerInformationSet < 1 ||
            MaxTransitionOutcomesPerAction < 1 || MaxExpandedDecisionNodes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(NoslSearchLimits), "NOSL search limits must be positive.");
        }
    }
}

internal sealed record NoslInformationSetSearchResult<TAction>(
    string RootInformationKey,
    NoslActionRecommendation<TAction> Recommendation,
    IReadOnlyList<NoslActionCandidate<TAction>> Candidates,
    int ExpandedDecisionNodes,
    long ExpandedTransitionOutcomes) where TAction : notnull;

/// <summary>
/// Small finite-horizon search over information sets.  Decisions are selected once for the whole
/// current bundle.  A chance transition is expanded first and only then split by its public
/// observation; future decisions may differ between those observed successor bundles.
/// </summary>
internal static class NoslInformationSetSearch
{
    public static NoslInformationSetSearchResult<TAction> Recommend<TState, TAction>(
        NoslScenarioBundle<TState> root,
        Func<string, int, IReadOnlyList<NoslSearchAction<TAction>>> actionProvider,
        Func<TState, TAction, int, NoslDistribution<NoslSearchTransition<TState>>> transition,
        Func<TState, NoslUtilityOutcome> terminalValue,
        int horizon,
        NoslRiskPolicy? riskPolicy = null,
        NoslSearchLimits? limits = null)
        where TState : notnull
        where TAction : notnull
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(actionProvider);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(terminalValue);
        if (horizon < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(horizon), "NOSL search horizon must be at least one action.");
        }
        NoslSearchLimits effectiveLimits = limits ?? NoslSearchLimits.Default;
        effectiveLimits.Validate();
        if (horizon > effectiveLimits.MaxDepth)
        {
            throw new InvalidOperationException("NOSL search horizon exceeds the configured limit; search was not silently truncated.");
        }
        SearchContext<TState, TAction> context = new SearchContext<TState, TAction>(
            actionProvider,
            transition,
            terminalValue,
            riskPolicy ?? NoslRiskPolicy.Default,
            effectiveLimits);
        DecisionEvaluation<TAction> evaluation = context.EvaluateDecision(ToPathBundle(root), horizon);
        return new NoslInformationSetSearchResult<TAction>(
            root.PublicInformationKey,
            evaluation.Recommendation ?? throw new InvalidOperationException("Root NOSL decision did not produce a recommendation."),
            evaluation.Candidates,
            context.ExpandedDecisionNodes,
            context.ExpandedTransitionOutcomes);
    }

    private static NoslScenarioBundle<SearchPathState<TState>> ToPathBundle<TState>(NoslScenarioBundle<TState> root)
        where TState : notnull
    {
        NoslDistribution<SearchPathState<TState>> scenarios = root.Scenarios.Map(
            state => new SearchPathState<TState>(state, 0.0, false));
        return new NoslScenarioBundle<SearchPathState<TState>>(root.PublicInformationKey, scenarios);
    }

    private sealed class SearchContext<TState, TAction>
        where TState : notnull
        where TAction : notnull
    {
        private readonly Func<string, int, IReadOnlyList<NoslSearchAction<TAction>>> _actionProvider;
        private readonly Func<TState, TAction, int, NoslDistribution<NoslSearchTransition<TState>>> _transition;
        private readonly Func<TState, NoslUtilityOutcome> _terminalValue;
        private readonly NoslRiskPolicy _riskPolicy;
        private readonly NoslSearchLimits _limits;

        public int ExpandedDecisionNodes { get; private set; }

        public long ExpandedTransitionOutcomes { get; private set; }

        public SearchContext(
            Func<string, int, IReadOnlyList<NoslSearchAction<TAction>>> actionProvider,
            Func<TState, TAction, int, NoslDistribution<NoslSearchTransition<TState>>> transition,
            Func<TState, NoslUtilityOutcome> terminalValue,
            NoslRiskPolicy riskPolicy,
            NoslSearchLimits limits)
        {
            _actionProvider = actionProvider;
            _transition = transition;
            _terminalValue = terminalValue;
            _riskPolicy = riskPolicy;
            _riskPolicy.Validate();
            _limits = limits;
        }

        public DecisionEvaluation<TAction> EvaluateDecision(
            NoslScenarioBundle<SearchPathState<TState>> bundle,
            int remainingDepth)
        {
            if (++ExpandedDecisionNodes > _limits.MaxExpandedDecisionNodes)
            {
                throw new InvalidOperationException("NOSL search exceeded the configured decision-node limit; transition was not truncated.");
            }
            if (remainingDepth == 0)
            {
                NoslDistribution<NoslUtilityOutcome> terminal = bundle.Scenarios.Map(path =>
                {
                    NoslUtilityOutcome value = _terminalValue(path.State);
                    if (!double.IsFinite(value.Utility))
                    {
                        throw new InvalidOperationException("NOSL terminal utility must be finite.");
                    }
                    return new NoslUtilityOutcome(
                        path.AccumulatedUtility + value.Utility,
                        path.Catastrophic || value.Catastrophic);
                });
                return new DecisionEvaluation<TAction>(null, Array.Empty<NoslActionCandidate<TAction>>(), terminal);
            }

            IReadOnlyList<NoslSearchAction<TAction>> provided = _actionProvider(bundle.PublicInformationKey, remainingDepth)
                ?? throw new InvalidOperationException("NOSL action provider returned null.");
            if (provided.Count == 0)
            {
                throw new InvalidOperationException($"NOSL information set '{bundle.PublicInformationKey}' has no legal action.");
            }
            if (provided.Count > _limits.MaxActionsPerInformationSet)
            {
                throw new InvalidOperationException("NOSL search exceeded the configured action limit; transition was not truncated.");
            }
            List<NoslSearchAction<TAction>> actions = provided
                .OrderBy(action => action.PublicActionKey, StringComparer.Ordinal)
                .ToList();
            HashSet<string> actionKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (NoslSearchAction<TAction> action in actions)
            {
                if (!actionKeys.Add(action.PublicActionKey))
                {
                    throw new InvalidOperationException($"Duplicate NOSL action key '{action.PublicActionKey}'.");
                }
            }

            List<NoslActionCandidate<TAction>> candidates = new List<NoslActionCandidate<TAction>>(actions.Count);
            foreach (NoslSearchAction<TAction> action in actions)
            {
                NoslDistribution<NoslUtilityOutcome> outcomes = EvaluateAction(bundle, action, remainingDepth);
                candidates.Add(new NoslActionCandidate<TAction>(action.PublicActionKey, action.Action, outcomes));
            }
            NoslActionRecommendation<TAction> recommendation = NoslDecisionPolicy.Recommend(candidates, _riskPolicy);
            NoslDistribution<NoslUtilityOutcome> selectedValue = candidates
                .Single(candidate => string.Equals(candidate.PublicActionKey, recommendation.PublicActionKey, StringComparison.Ordinal))
                .Outcomes;
            return new DecisionEvaluation<TAction>(recommendation, candidates, selectedValue);
        }

        private NoslDistribution<NoslUtilityOutcome> EvaluateAction(
            NoslScenarioBundle<SearchPathState<TState>> bundle,
            NoslSearchAction<TAction> action,
            int remainingDepth)
        {
            NoslDistribution<ObservedPath<TState>> joint = bundle.Scenarios.Bind(path =>
            {
                NoslDistribution<NoslSearchTransition<TState>> transitions = _transition(path.State, action.Action, remainingDepth)
                    ?? throw new InvalidOperationException("NOSL transition returned null.");
                if (transitions.Count > _limits.MaxTransitionOutcomesPerAction)
                {
                    throw new InvalidOperationException("NOSL action exceeded the configured transition-outcome limit; transition was not truncated.");
                }
                ExpandedTransitionOutcomes += transitions.Count;
                long maximumTransitions = (long)_limits.MaxExpandedDecisionNodes
                    * _limits.MaxActionsPerInformationSet
                    * _limits.MaxTransitionOutcomesPerAction;
                if (ExpandedTransitionOutcomes > maximumTransitions)
                {
                    throw new InvalidOperationException("NOSL search exceeded the configured transition limit; transition was not truncated.");
                }
                return transitions.Map(transition => new ObservedPath<TState>(
                    transition.PublicObservationKey,
                    new SearchPathState<TState>(
                        transition.State,
                        path.AccumulatedUtility + transition.ImmediateUtility,
                        path.Catastrophic || transition.Catastrophic)));
            }, ObservedPathComparer<TState>.Instance);

            List<NoslWeightedOutcome<NoslUtilityOutcome>> aggregate = new List<NoslWeightedOutcome<NoslUtilityOutcome>>();
            foreach (IGrouping<string, NoslWeightedOutcome<ObservedPath<TState>>> group in joint.Outcomes
                .GroupBy(outcome => outcome.Value.PublicObservationKey, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                double probability = group.Sum(outcome => outcome.Probability);
                if (!double.IsFinite(probability) || probability <= 0.0)
                {
                    throw new InvalidOperationException("NOSL transition produced invalid probability mass.");
                }
                NoslDistribution<SearchPathState<TState>> conditional = NoslDistribution<SearchPathState<TState>>.Create(
                    group.Select(outcome => new NoslWeightedOutcome<SearchPathState<TState>>(
                        outcome.Value.Path,
                        outcome.Probability / probability)));
                string successorKey = bundle.PublicInformationKey + "/obs:" + group.Key.Length + ":" + group.Key;
                DecisionEvaluation<TAction> child = EvaluateDecision(
                    new NoslScenarioBundle<SearchPathState<TState>>(successorKey, conditional),
                    remainingDepth - 1);
                aggregate.AddRange(child.Value.Outcomes.Select(outcome =>
                    new NoslWeightedOutcome<NoslUtilityOutcome>(outcome.Value, probability * outcome.Probability)));
            }
            return NoslDistribution<NoslUtilityOutcome>.Create(aggregate);
        }
    }

    private readonly record struct SearchPathState<TState>(TState State, double AccumulatedUtility, bool Catastrophic)
        where TState : notnull;

    private readonly record struct ObservedPath<TState>(string PublicObservationKey, SearchPathState<TState> Path)
        where TState : notnull;

    private sealed class ObservedPathComparer<TState> : IEqualityComparer<ObservedPath<TState>>
        where TState : notnull
    {
        public static ObservedPathComparer<TState> Instance { get; } = new ObservedPathComparer<TState>();

        public bool Equals(ObservedPath<TState> x, ObservedPath<TState> y)
        {
            return string.Equals(x.PublicObservationKey, y.PublicObservationKey, StringComparison.Ordinal)
                && EqualityComparer<SearchPathState<TState>>.Default.Equals(x.Path, y.Path);
        }

        public int GetHashCode(ObservedPath<TState> obj)
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(obj.PublicObservationKey),
                EqualityComparer<SearchPathState<TState>>.Default.GetHashCode(obj.Path));
        }
    }

    private sealed record DecisionEvaluation<TAction>(
        NoslActionRecommendation<TAction>? Recommendation,
        IReadOnlyList<NoslActionCandidate<TAction>> Candidates,
        NoslDistribution<NoslUtilityOutcome> Value)
        where TAction : notnull;
}
