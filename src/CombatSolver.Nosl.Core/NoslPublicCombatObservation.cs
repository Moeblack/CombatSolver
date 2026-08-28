using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CombatSolver.Nosl;

/// <summary>
/// The typed root payload accepted by NOSL search. It intentionally has no field for a run seed,
/// random-stream state/counter, or concrete draw-pile order.
/// </summary>
internal sealed class NoslPublicCombatObservation
{
    private readonly string[] _currentHandCardKeys;
    private readonly string[] _visiblePlayerStateKeys;
    private readonly string[] _potionSlotKeys;
    private readonly string[] _visibleEnemyStateKeys;
    private readonly string[] _orderedOrbKeys;
    private readonly string[] _futureIntentKeys;

    public int TurnNumber { get; }

    public int PlayerHealth { get; }

    public int PlayerMaxHealth { get; }

    public int PlayerBlock { get; }

    public int Energy { get; }

    public int Stars { get; }

    public IReadOnlyList<string> CurrentHandCardKeys => _currentHandCardKeys;

    public string DrawPileInformationKey { get; }

    public string DiscardPileInformationKey { get; }

    public IReadOnlyList<string> VisiblePlayerStateKeys => _visiblePlayerStateKeys;

    public IReadOnlyList<string> PotionSlotKeys => _potionSlotKeys;

    public IReadOnlyList<string> VisibleEnemyStateKeys => _visibleEnemyStateKeys;

    public IReadOnlyList<string> OrderedOrbKeys => _orderedOrbKeys;

    public IReadOnlyList<string> FutureIntentKeys => _futureIntentKeys;

    public string InformationKey { get; }

    public NoslPublicCombatObservation(
        int turnNumber,
        int playerHealth,
        int playerMaxHealth,
        int playerBlock,
        int energy,
        int stars,
        IEnumerable<string> currentHandCardKeys,
        string drawPileInformationKey,
        string discardPileInformationKey,
        IEnumerable<string> visiblePlayerStateKeys,
        IEnumerable<string> potionSlotKeys,
        IEnumerable<string> visibleEnemyStateKeys,
        IEnumerable<string> orderedOrbKeys,
        IEnumerable<string> futureIntentKeys)
    {
        if (turnNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(turnNumber));
        }
        if (playerMaxHealth < 0 || playerHealth < 0 || playerHealth > playerMaxHealth)
        {
            throw new ArgumentOutOfRangeException(nameof(playerHealth));
        }
        if (playerBlock < 0 || energy < 0 || stars < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerBlock), "Public combat resources cannot be negative.");
        }
        TurnNumber = turnNumber;
        PlayerHealth = playerHealth;
        PlayerMaxHealth = playerMaxHealth;
        PlayerBlock = playerBlock;
        Energy = energy;
        Stars = stars;
        _currentHandCardKeys = OrderedTokens(currentHandCardKeys, nameof(currentHandCardKeys), allowEmptyToken: false);
        DrawPileInformationKey = RequireToken(drawPileInformationKey, nameof(drawPileInformationKey));
        DiscardPileInformationKey = RequireToken(discardPileInformationKey, nameof(discardPileInformationKey));
        _visiblePlayerStateKeys = CanonicalMultiset(visiblePlayerStateKeys, nameof(visiblePlayerStateKeys));
        _potionSlotKeys = OrderedTokens(potionSlotKeys, nameof(potionSlotKeys), allowEmptyToken: true);
        _visibleEnemyStateKeys = CanonicalMultiset(visibleEnemyStateKeys, nameof(visibleEnemyStateKeys));
        _orderedOrbKeys = OrderedTokens(orderedOrbKeys, nameof(orderedOrbKeys), allowEmptyToken: false);
        _futureIntentKeys = OrderedTokens(futureIntentKeys, nameof(futureIntentKeys), allowEmptyToken: false);
        InformationKey = BuildInformationKey();
    }

    private string BuildInformationKey()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("turn=").Append(TurnNumber)
            .Append(";hp=").Append(PlayerHealth).Append('/').Append(PlayerMaxHealth)
            .Append(";block=").Append(PlayerBlock)
            .Append(";energy=").Append(Energy)
            .Append(";stars=").Append(Stars);
        AppendTokens(builder, "hand", _currentHandCardKeys);
        AppendToken(builder, "draw", DrawPileInformationKey);
        AppendToken(builder, "discard", DiscardPileInformationKey);
        AppendTokens(builder, "player", _visiblePlayerStateKeys);
        AppendTokens(builder, "potions", _potionSlotKeys);
        AppendTokens(builder, "enemies", _visibleEnemyStateKeys);
        AppendTokens(builder, "orbs", _orderedOrbKeys);
        AppendTokens(builder, "intents", _futureIntentKeys);
        return builder.ToString();
    }

    private static string[] CanonicalMultiset(IEnumerable<string> values, string parameterName)
    {
        return OrderedTokens(values, parameterName, allowEmptyToken: false)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] OrderedTokens(IEnumerable<string> values, string parameterName, bool allowEmptyToken)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        string[] result = values.ToArray();
        for (int i = 0; i < result.Length; i++)
        {
            if (result[i] == null || (!allowEmptyToken && string.IsNullOrWhiteSpace(result[i])))
            {
                throw new ArgumentException("Public observation tokens must be non-null and, unless explicitly allowed, non-empty.", parameterName);
            }
        }
        return result;
    }

    private static string RequireToken(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A public observation key cannot be empty.", parameterName);
        }
        return value;
    }

    private static void AppendTokens(StringBuilder builder, string name, IEnumerable<string> values)
    {
        builder.Append(';').Append(name).Append('=');
        foreach (string value in values)
        {
            builder.Append(value.Length).Append(':').Append(value).Append(';');
        }
    }

    private static void AppendToken(StringBuilder builder, string name, string value)
    {
        builder.Append(';').Append(name).Append('=').Append(value.Length).Append(':').Append(value);
    }
}
