using System;
using System.Collections.Generic;
using System.Linq;

namespace CombatSolver.Nosl;

internal readonly record struct NoslWeightedOutcome<T>(T Value, double Probability) where T : notnull;

/// <summary>
/// A normalized finite distribution. It deliberately has no random-number API: callers either
/// enumerate every outcome or pass the distribution to <see cref="NoslChancePoint{T}"/>.
/// </summary>
internal sealed class NoslDistribution<T> where T : notnull
{
    private readonly NoslWeightedOutcome<T>[] _outcomes;

    public IReadOnlyList<NoslWeightedOutcome<T>> Outcomes => _outcomes;

    public int Count => _outcomes.Length;

    private NoslDistribution(NoslWeightedOutcome<T>[] outcomes)
    {
        _outcomes = outcomes;
    }

    public static NoslDistribution<T> Certain(T value)
    {
        return new NoslDistribution<T>(new NoslWeightedOutcome<T>[1]
        {
            new NoslWeightedOutcome<T>(value, 1.0)
        });
    }

    public static NoslDistribution<T> Create(IEnumerable<NoslWeightedOutcome<T>> outcomes, IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        comparer ??= EqualityComparer<T>.Default;
        Dictionary<T, int> indexes = new Dictionary<T, int>(comparer);
        List<T> values = new List<T>();
        List<double> masses = new List<double>();
        foreach (NoslWeightedOutcome<T> outcome in outcomes)
        {
            if (!double.IsFinite(outcome.Probability) || outcome.Probability < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(outcomes), "Outcome probability must be finite and non-negative.");
            }
            if (outcome.Probability == 0.0)
            {
                continue;
            }
            if (indexes.TryGetValue(outcome.Value, out int index))
            {
                masses[index] += outcome.Probability;
            }
            else
            {
                indexes.Add(outcome.Value, values.Count);
                values.Add(outcome.Value);
                masses.Add(outcome.Probability);
            }
        }
        if (values.Count == 0)
        {
            throw new ArgumentException("A distribution must contain positive probability mass.", nameof(outcomes));
        }
        double total = CompensatedSum(masses);
        if (!double.IsFinite(total) || total <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(outcomes), "Total probability mass must be finite and positive.");
        }
        NoslWeightedOutcome<T>[] normalized = new NoslWeightedOutcome<T>[values.Count];
        for (int i = 0; i < normalized.Length; i++)
        {
            normalized[i] = new NoslWeightedOutcome<T>(values[i], masses[i] / total);
        }
        return new NoslDistribution<T>(normalized);
    }

    public NoslDistribution<TResult> Map<TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? comparer = null) where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(selector);
        return NoslDistribution<TResult>.Create(_outcomes.Select(outcome => new NoslWeightedOutcome<TResult>(selector(outcome.Value), outcome.Probability)), comparer);
    }

    public NoslDistribution<TResult> Bind<TResult>(Func<T, NoslDistribution<TResult>> selector, IEqualityComparer<TResult>? comparer = null) where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(selector);
        return NoslDistribution<TResult>.Create(
            _outcomes.SelectMany(outcome => selector(outcome.Value).Outcomes.Select(child => new NoslWeightedOutcome<TResult>(child.Value, outcome.Probability * child.Probability))),
            comparer);
    }

    public double ExpectedValue(Func<T, double> valueSelector)
    {
        ArgumentNullException.ThrowIfNull(valueSelector);
        return CompensatedSum(_outcomes.Select(outcome => ValidateMetric(valueSelector(outcome.Value)) * outcome.Probability));
    }

    public NoslRiskSummary Summarize(Func<T, double> utilitySelector, Func<T, bool>? catastrophicSelector = null, double worstQuantile = 0.1, double lowerTailMass = 0.1)
    {
        ArgumentNullException.ThrowIfNull(utilitySelector);
        ValidateUnitInterval(worstQuantile, nameof(worstQuantile));
        ValidateUnitInterval(lowerTailMass, nameof(lowerTailMass));
        List<(double Utility, double Probability, bool Catastrophic)> ordered = _outcomes
            .Select(outcome => (ValidateMetric(utilitySelector(outcome.Value)), outcome.Probability, catastrophicSelector?.Invoke(outcome.Value) ?? false))
            .OrderBy(outcome => outcome.Item1)
            .ToList();
        double mean = CompensatedSum(ordered.Select(outcome => outcome.Utility * outcome.Probability));
        double variance = CompensatedSum(ordered.Select(outcome =>
        {
            double delta = outcome.Utility - mean;
            return outcome.Probability * delta * delta;
        }));
        double catastropheProbability = CompensatedSum(ordered.Where(outcome => outcome.Catastrophic).Select(outcome => outcome.Probability));
        double cumulative = 0.0;
        double quantile = ordered[^1].Utility;
        foreach ((double utility, double probability, _) in ordered)
        {
            cumulative += probability;
            if (cumulative + 1e-15 >= worstQuantile)
            {
                quantile = utility;
                break;
            }
        }
        double remaining = lowerTailMass;
        double tailTotal = 0.0;
        foreach ((double utility, double probability, _) in ordered)
        {
            if (remaining <= 1e-15)
            {
                break;
            }
            double included = Math.Min(remaining, probability);
            tailTotal += included * utility;
            remaining -= included;
        }
        return new NoslRiskSummary(
            ExpectedUtility: mean,
            Variance: Math.Max(0.0, variance),
            WorstQuantileUtility: quantile,
            LowerTailCvar: tailTotal / lowerTailMass,
            MinimumUtility: ordered[0].Utility,
            MaximumUtility: ordered[^1].Utility,
            CatastropheProbability: Math.Clamp(catastropheProbability, 0.0, 1.0));
    }

    private static double ValidateMetric(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Outcome utility must be finite.");
        }
        return value;
    }

    private static void ValidateUnitInterval(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0.0 || value > 1.0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be in (0, 1].");
        }
    }

    private static double CompensatedSum(IEnumerable<double> values)
    {
        double sum = 0.0;
        double compensation = 0.0;
        foreach (double value in values)
        {
            double corrected = value - compensation;
            double next = sum + corrected;
            compensation = next - sum - corrected;
            sum = next;
        }
        return sum;
    }
}

internal readonly record struct NoslRiskSummary(
    double ExpectedUtility,
    double Variance,
    double WorstQuantileUtility,
    double LowerTailCvar,
    double MinimumUtility,
    double MaximumUtility,
    double CatastropheProbability)
{
    public double StandardDeviation => Math.Sqrt(Variance);
}
