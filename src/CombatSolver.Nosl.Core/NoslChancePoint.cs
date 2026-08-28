using System;
using System.Collections.Generic;
using System.Linq;

namespace CombatSolver.Nosl;

internal enum NoslChanceKind
{
    DrawFromUnknownOrder,
    Shuffle,
    RandomTarget,
    CardGeneration,
    PotionGeneration,
    CardSelection,
    EnergyCost,
    OrbGeneration,
    MonsterIntent,
    Niche
}

internal sealed record NoslChanceOption<T>(string PublicKey, T Value, double Weight) where T : notnull;

internal sealed record NoslChanceResolution<T>(NoslDistribution<T> Distribution, bool IsExact, int ScenarioCount) where T : notnull;

/// <summary>
/// A chance point described only by public options and rule weights. Large points use a fixed
/// low-discrepancy sequence, never the game's seed/counter or a synthetic prediction seed.
/// </summary>
internal sealed class NoslChancePoint<T> where T : notnull
{
    private static readonly int[] PrimeBases = { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53 };

    private readonly NoslChanceOption<T>[] _options;

    public NoslChanceKind Kind { get; }

    public string PublicId { get; }

    public IReadOnlyList<NoslChanceOption<T>> Options => _options;

    public NoslChancePoint(NoslChanceKind kind, string publicId, IEnumerable<NoslChanceOption<T>> options)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            throw new ArgumentException("Chance point must have a public id.", nameof(publicId));
        }
        ArgumentNullException.ThrowIfNull(options);
        Kind = kind;
        PublicId = publicId;
        _options = options.OrderBy(option => option.PublicKey, StringComparer.Ordinal).ToArray();
        if (_options.Length == 0)
        {
            throw new ArgumentException("Chance point must have at least one option.", nameof(options));
        }
        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (NoslChanceOption<T> option in _options)
        {
            if (string.IsNullOrWhiteSpace(option.PublicKey) || !keys.Add(option.PublicKey))
            {
                throw new ArgumentException("Chance option public keys must be non-empty and unique.", nameof(options));
            }
            if (!double.IsFinite(option.Weight) || option.Weight <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Chance option weights must be finite and positive.");
            }
        }
    }

    public NoslChanceResolution<T> Resolve(int maxExactOptions, int sampleCount, int chanceDepth = 0)
    {
        if (maxExactOptions < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExactOptions));
        }
        if (sampleCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        }
        NoslDistribution<T> exact = NoslDistribution<T>.Create(_options.Select(option => new NoslWeightedOutcome<T>(option.Value, option.Weight)));
        if (_options.Length <= maxExactOptions)
        {
            return new NoslChanceResolution<T>(exact, IsExact: true, ScenarioCount: _options.Length);
        }
        int prime = PrimeBases[Math.Abs(chanceDepth % PrimeBases.Length)];
        double rotation = PublicRotation(PublicId, Kind, chanceDepth);
        List<NoslWeightedOutcome<T>> samples = new List<NoslWeightedOutcome<T>>(sampleCount);
        for (int i = 0; i < sampleCount; i++)
        {
            double quantile = RadicalInverse(i + 1, prime) + rotation;
            quantile -= Math.Floor(quantile);
            samples.Add(new NoslWeightedOutcome<T>(Select(exact.Outcomes, quantile), 1.0 / sampleCount));
        }
        return new NoslChanceResolution<T>(NoslDistribution<T>.Create(samples), IsExact: false, ScenarioCount: sampleCount);
    }

    private static T Select(IReadOnlyList<NoslWeightedOutcome<T>> options, double quantile)
    {
        double cumulative = 0.0;
        for (int i = 0; i < options.Count; i++)
        {
            cumulative += options[i].Probability;
            if (quantile < cumulative || i == options.Count - 1)
            {
                return options[i].Value;
            }
        }
        throw new InvalidOperationException("Unreachable chance selection state.");
    }

    private static double RadicalInverse(int index, int numberBase)
    {
        double inverse = 1.0 / numberBase;
        double factor = inverse;
        double result = 0.0;
        while (index > 0)
        {
            result += index % numberBase * factor;
            index /= numberBase;
            factor *= inverse;
        }
        return result;
    }

    private static double PublicRotation(string publicId, NoslChanceKind kind, int chanceDepth)
    {
        // Stable public-data scrambling prevents artificial correlation between different chance
        // points. This is not a game/prediction seed and cannot depend on hidden state.
        ulong hash = 14695981039346656037UL;
        foreach (char value in publicId)
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
        hash ^= (uint)kind;
        hash *= 1099511628211UL;
        hash ^= unchecked((uint)chanceDepth);
        hash *= 1099511628211UL;
        return (hash >> 11) * (1.0 / 9007199254740992.0);
    }
}
