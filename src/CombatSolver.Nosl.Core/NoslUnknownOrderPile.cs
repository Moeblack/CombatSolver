using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CombatSolver.Nosl;

/// <summary>
/// The information-set representation of a visible draw pile. The cards are visible, but their
/// hidden engine order is not. Only explicitly revealed top/bottom placements carry order.
/// </summary>
internal sealed class NoslUnknownOrderPile<T> where T : notnull
{
    private readonly T[] _knownTop;
    private readonly T[] _unknownMiddle;
    private readonly T[] _knownBottom;
    private readonly Func<T, string> _publicKey;

    public IReadOnlyList<T> KnownTop => _knownTop;

    public IReadOnlyList<T> UnknownMiddle => _unknownMiddle;

    public IReadOnlyList<T> KnownBottom => _knownBottom;

    public int Count => _knownTop.Length + _unknownMiddle.Length + _knownBottom.Length;

    public string InformationKey { get; }

    private NoslUnknownOrderPile(IEnumerable<T> knownTop, IEnumerable<T> unknownMiddle, IEnumerable<T> knownBottom, Func<T, string> publicKey)
    {
        _publicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
        _knownTop = knownTop.ToArray();
        _unknownMiddle = unknownMiddle.OrderBy(publicKey, StringComparer.Ordinal).ToArray();
        _knownBottom = knownBottom.ToArray();
        ValidateKeys(_knownTop);
        ValidateKeys(_unknownMiddle);
        ValidateKeys(_knownBottom);
        InformationKey = BuildInformationKey();
    }

    public static NoslUnknownOrderPile<T> FromVisibleContents(IEnumerable<T> visibleCards, Func<T, string> publicKey)
    {
        ArgumentNullException.ThrowIfNull(visibleCards);
        return new NoslUnknownOrderPile<T>(Array.Empty<T>(), visibleCards, Array.Empty<T>(), publicKey);
    }

    public NoslUnknownOrderPile<T> PlaceOnTop(T card)
    {
        return new NoslUnknownOrderPile<T>(new[] { card }.Concat(_knownTop), _unknownMiddle, _knownBottom, _publicKey);
    }

    public NoslUnknownOrderPile<T> PlaceOnBottom(T card)
    {
        return new NoslUnknownOrderPile<T>(_knownTop, _unknownMiddle, _knownBottom.Append(card), _publicKey);
    }

    public NoslUnknownOrderPile<T> ForgetAllOrder(IEnumerable<T>? additionalVisibleCards = null)
    {
        IEnumerable<T> all = _knownTop.Concat(_unknownMiddle).Concat(_knownBottom);
        if (additionalVisibleCards != null)
        {
            all = all.Concat(additionalVisibleCards);
        }
        return FromVisibleContents(all, _publicKey);
    }

    public NoslDistribution<NoslPileDraw<T>> Draw()
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("Cannot draw from an empty NOSL pile.");
        }
        if (_knownTop.Length > 0)
        {
            T card = _knownTop[0];
            NoslUnknownOrderPile<T> remaining = new NoslUnknownOrderPile<T>(_knownTop.Skip(1), _unknownMiddle, _knownBottom, _publicKey);
            return NoslDistribution<NoslPileDraw<T>>.Certain(new NoslPileDraw<T>(_publicKey(card), card, remaining));
        }
        if (_unknownMiddle.Length == 0)
        {
            T card = _knownBottom[0];
            NoslUnknownOrderPile<T> remaining = new NoslUnknownOrderPile<T>(Array.Empty<T>(), Array.Empty<T>(), _knownBottom.Skip(1), _publicKey);
            return NoslDistribution<NoslPileDraw<T>>.Certain(new NoslPileDraw<T>(_publicKey(card), card, remaining));
        }
        List<NoslWeightedOutcome<NoslPileDraw<T>>> outcomes = new List<NoslWeightedOutcome<NoslPileDraw<T>>>();
        foreach (IGrouping<string, T> group in _unknownMiddle.GroupBy(_publicKey, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            T representative = group.First();
            bool removed = false;
            List<T> remainingCards = new List<T>(_unknownMiddle.Length - 1);
            foreach (T candidate in _unknownMiddle)
            {
                if (!removed && string.Equals(_publicKey(candidate), group.Key, StringComparison.Ordinal))
                {
                    removed = true;
                    continue;
                }
                remainingCards.Add(candidate);
            }
            NoslUnknownOrderPile<T> remaining = new NoslUnknownOrderPile<T>(Array.Empty<T>(), remainingCards, _knownBottom, _publicKey);
            double probability = (double)group.Count() / _unknownMiddle.Length;
            outcomes.Add(new NoslWeightedOutcome<NoslPileDraw<T>>(new NoslPileDraw<T>(group.Key, representative, remaining), probability));
        }
        return NoslDistribution<NoslPileDraw<T>>.Create(outcomes, NoslPileDrawComparer<T>.Instance);
    }

    private string BuildInformationKey()
    {
        StringBuilder builder = new StringBuilder();
        AppendSegment(builder, 'T', _knownTop.Select(_publicKey));
        AppendSegment(builder, 'U', _unknownMiddle.Select(_publicKey));
        AppendSegment(builder, 'B', _knownBottom.Select(_publicKey));
        return builder.ToString();
    }

    private static void AppendSegment(StringBuilder builder, char marker, IEnumerable<string> keys)
    {
        builder.Append(marker).Append('[');
        foreach (string key in keys)
        {
            builder.Append(key.Length).Append(':').Append(key).Append(';');
        }
        builder.Append(']');
    }

    private void ValidateKeys(IEnumerable<T> cards)
    {
        foreach (T card in cards)
        {
            string key = _publicKey(card);
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Every NOSL-visible card must have a non-empty public key.", nameof(cards));
            }
        }
    }
}

internal sealed record NoslPileDraw<T>(string ObservationKey, T Card, NoslUnknownOrderPile<T> RemainingPile) where T : notnull;

internal sealed class NoslPileDrawComparer<T> : IEqualityComparer<NoslPileDraw<T>> where T : notnull
{
    public static NoslPileDrawComparer<T> Instance { get; } = new NoslPileDrawComparer<T>();

    public bool Equals(NoslPileDraw<T>? x, NoslPileDraw<T>? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }
        return x != null && y != null && string.Equals(x.ObservationKey, y.ObservationKey, StringComparison.Ordinal) && string.Equals(x.RemainingPile.InformationKey, y.RemainingPile.InformationKey, StringComparison.Ordinal);
    }

    public int GetHashCode(NoslPileDraw<T> obj)
    {
        return HashCode.Combine(StringComparer.Ordinal.GetHashCode(obj.ObservationKey), StringComparer.Ordinal.GetHashCode(obj.RemainingPile.InformationKey));
    }
}
