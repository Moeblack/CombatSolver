using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver.Engine.Common;

internal sealed class PredictedCard : IComparable<PredictedCard>
{
    private sealed class PreviewStorage(CardModel? preview)
    {
        public CardModel? Preview { get; } = preview;
        public volatile bool Shared;
    }

    private readonly CardModel _original;
    private PreviewStorage _previewStorage;
    private int _mutationVersion;
    private bool _hasCachedFingerprint;
    private ulong _cachedFingerprintFirst;
    private ulong _cachedFingerprintSecond;
    private string? _cachedChoiceKey;
    private SimCardPile? _ownerPile;
    private Action? _mutationObserver;

    public PredictedCard(CardModel original, CardModel? preview = null)
    {
        _original = original;
        _previewStorage = new PreviewStorage(preview);
    }

    private PredictedCard(CardModel original, PreviewStorage previewStorage)
    {
        _original = original;
        _previewStorage = previewStorage;
    }

    public CardModel Original => _original;

    public CardModel Preview => _previewStorage.Preview ?? _original;

    public int MutationVersion => _mutationVersion;

    internal SimCardPile? OwnerPile => _ownerPile;

    public CardModel MutablePreview
    {
        get
        {
            _mutationVersion++;
            _hasCachedFingerprint = false;
            _cachedChoiceKey = null;
            _ownerPile?.InvalidateFingerprint();
            _mutationObserver?.Invoke();
            CardModel? preview = _previewStorage.Preview;
            if (preview is null)
            {
                preview = PredictionUtils.CloneCardStateForSimulation(_original);
                _previewStorage = new PreviewStorage(preview);
            }
            else if (_previewStorage.Shared)
            {
                preview = PredictionUtils.CloneCardStateForSimulation(preview);
                _previewStorage = new PreviewStorage(preview);
            }
            return preview;
        }
    }

    internal void MaterializePreview()
    {
        if (_previewStorage.Preview is not null)
            return;
        _previewStorage = new PreviewStorage(PredictionUtils.CloneCardStateForSimulation(_original));
    }

    public static List<PredictedCard> FromCards(IEnumerable<CardModel> cards)
    {
        return cards.Select(card => new PredictedCard(card)).ToList();
    }

    public static PredictedCard FromGenerated(CardModel card)
    {
        return new(card, card);
    }

    public static PredictedCard Create(CardModel canonicalCard, Player player)
    {
        return FromGenerated(PredictionUtils.CreateCard(canonicalCard, player));
    }

    public bool References(object? card)
    {
        return ReferenceEquals(_original, card) || ReferenceEquals(_previewStorage.Preview, card);
    }

    // Clones the prediction wrapper state only. Combat effects that generate a gameplay
    // clone of a card should use CombatPredictedCardExtensions.CreateClone instead.
    public PredictedCard Clone()
    {
        return new(_original, _previewStorage.Preview is { } preview
            ? PredictionUtils.CloneCardStateForSimulation(preview)
            : null);
    }

    internal PredictedCard Fork(PredictionForkContext context)
    {
        _previewStorage.Shared = true;
        PredictedCard fork = new(_original, _previewStorage)
        {
            _mutationVersion = _mutationVersion,
            _hasCachedFingerprint = _hasCachedFingerprint,
            _cachedFingerprintFirst = _cachedFingerprintFirst,
            _cachedFingerprintSecond = _cachedFingerprintSecond,
            _cachedChoiceKey = _cachedChoiceKey,
        };
        context.Register(this, fork);
        return fork;
    }

    internal bool TryGetCachedFingerprint(out ulong first, out ulong second)
    {
        first = _cachedFingerprintFirst;
        second = _cachedFingerprintSecond;
        return _hasCachedFingerprint;
    }

    internal void SetCachedFingerprint(ulong first, ulong second)
    {
        _cachedFingerprintFirst = first;
        _cachedFingerprintSecond = second;
        _hasCachedFingerprint = true;
    }

    internal bool TryGetCachedChoiceKey(out string key)
    {
        key = _cachedChoiceKey ?? string.Empty;
        return _cachedChoiceKey is not null;
    }

    internal void SetCachedChoiceKey(string key)
        => _cachedChoiceKey = key;

    internal void SetOwnerPile(SimCardPile? pile)
    {
        _ownerPile = pile;
    }

    internal void SetMutationObserver(Action? observer)
    {
        _mutationObserver = observer;
    }

    public int CompareTo(PredictedCard? other)
    {
        return Preview.CompareTo(other?.Preview);
    }
}
