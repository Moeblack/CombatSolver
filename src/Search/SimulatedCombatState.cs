using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Nodes;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.Card;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.Damage;
using CombatSolver.Engine.InCombat.Mirrors.Hooks;
using CombatSolver.Engine.InCombat.Simulation;
using System.Reflection;
using System.Collections.Concurrent;

namespace CombatSolver;

internal sealed partial class SimulatedCombatState
    : ICombatState, ICombatPredictionForkableState, ICombatPredictionHookListenerSource,
      ICombatPredictionCardEventSink, ICombatPredictionEffectSink, ICombatPredictionRosterSink,
      ICombatPredictionCreatureSemantics, ICombatPredictionMonsterStateSink,
      ICombatPredictionCardExecutionSink, ICombatPredictionPendingChoiceState,
      ICombatPredictionRunSnapshot, ICombatPredictionPlayerLimits, ICombatPredictionPlayerCardRules,
      ICombatPredictionStateOwner, ICombatPredictionRootCaptureBoundary,
      ICombatPredictionRootMaterializable, IPredictionForkBoundary
{
    private readonly IRunState _runState;
    private readonly IReadOnlyList<Creature> _playerCreatures;
    private readonly IReadOnlyList<Player> _players;
    private readonly IReadOnlyList<ModifierModel> _modifiers;
    private readonly MultiplayerScalingModel? _multiplayerScalingModel;
    private readonly EncounterModel? _encounter;
    private readonly IReadOnlyList<string> _encounterSlots;
    private readonly RootCombatHistorySnapshot _rootHistory;
    private readonly IReadOnlySet<Creature> _rootCreatures;
    private readonly AbstractModel[] _rootHookListeners;
    private readonly AbstractModel[] _rootRunHookListeners;
    private readonly IReadOnlyDictionary<Player, RelicModel[]> _rootRelics;
    private IReadOnlyDictionary<RelicModel, RelicModel>? _rootRelicSources;
    private readonly IReadOnlyDictionary<Player, int> _rootPotionSlotCounts;
    private readonly IReadOnlyDictionary<Player, int> _rootPlayerTurnNumbers;
    private readonly IReadOnlyDictionary<(Creature Owner, Type Type), int> _rootPowerAmounts;
    private readonly IReadOnlyDictionary<Player, string> _playerNames;
    private readonly IReadOnlySet<CardModel> _rootFloatingCards;
    private readonly IReadOnlySet<Creature> _rootDeadCreatures;
    private readonly SerializableRunRngSet _runRngSnapshot;
    private readonly int _currentActIndex;
    private readonly RoomType? _currentRoomType;
    private readonly MapCoord? _currentMapCoord;
    private readonly CardMultiplayerConstraint _cardMultiplayerConstraint;
    private readonly PredictionModHookSubscriberCapture _modHookSubscribers;
    private readonly IReadOnlyDictionary<Player, int> _rootMaxHandSizes;

    private sealed class CombinedRosterView(
        IReadOnlyList<Creature> first,
        IReadOnlyList<Creature> second) : IReadOnlyList<Creature>
    {
        public int Count => first.Count + second.Count;
        public Creature this[int index] => index < first.Count ? first[index] : second[index - first.Count];
        public IEnumerator<Creature> GetEnumerator()
        {
            foreach (Creature creature in first)
                yield return creature;
            foreach (Creature creature in second)
                yield return creature;
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private delegate void ApplyPowerDelegate(
        SimulatedCombatState combat,
        Creature target,
        int amount,
        Creature? applier);

    private delegate void ApplyTemporaryStrengthLossDelegate(
        SimulatedCombatState combat,
        Creature target,
        int amount,
        Creature? applier);

    private delegate void ApplyTemporaryDexterityDelegate(
        SimulatedCombatState combat,
        Creature target,
        int amount,
        Creature? applier);

    private static readonly MethodInfo GenericApplyMethod = typeof(SimulatedCombatState)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method => method.Name == nameof(Apply) && method.IsGenericMethodDefinition);
    private static readonly ConcurrentDictionary<Type, ApplyPowerDelegate> ApplyPowerDelegates = new();
    private static readonly MethodInfo GenericTemporaryStrengthLossMethod = typeof(SimulatedCombatState)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method => method.Name == nameof(ApplyTemporaryStrengthLoss)
            && method.IsGenericMethodDefinition);
    private static readonly ConcurrentDictionary<Type, ApplyTemporaryStrengthLossDelegate>
        TemporaryStrengthLossDelegates = new();
    private static readonly MethodInfo GenericTemporaryDexterityMethod = typeof(SimulatedCombatState)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method => method.Name == nameof(ApplyTemporaryDexterity)
            && method.IsGenericMethodDefinition);
    private static readonly ConcurrentDictionary<Type, ApplyTemporaryDexterityDelegate>
        TemporaryDexterityDelegates = new();
    private static readonly FieldInfo NemesisShouldApplyIntangibleField =
        typeof(NemesisPower).GetField("_shouldApplyIntangible", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(NemesisPower).FullName, "_shouldApplyIntangible");
    private static readonly FieldInfo TenderCardsPlayedField =
        typeof(TenderPower).GetField("_cardsPlayedThisTurn", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(TenderPower).FullName, "_cardsPlayedThisTurn");
    private static readonly FieldInfo NextCreatureIdField =
        typeof(CombatState).GetField("_nextCreatureId", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(CombatState).FullName, "_nextCreatureId");
    private static readonly FieldInfo AllCombatCardsField =
        typeof(CombatState).GetField("_allCards", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(CombatState).FullName, "_allCards");
    private static readonly FieldInfo MultiplayerScalingRunStateField =
        typeof(MultiplayerScalingModel).GetField("_runState", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(MultiplayerScalingModel).FullName, "_runState");
    private static readonly FieldInfo MultiplayerScalingCombatStateField =
        typeof(MultiplayerScalingModel).GetField("_combatState", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(MultiplayerScalingModel).FullName, "_combatState");

    private enum SteamEruptionPhase
    {
        AboutToBlow,
        Explode,
    }

    private Dictionary<(Creature Owner, Type Type), PowerModel>? _powers;
    private List<PredictedCard>? _generatedCombatCards;
    private List<PredictedCard>? _registeredCombatCards;
    private AbstractModel[]? _baseHookListeners;
    private IReadOnlyList<AbstractModel>? _effectiveHookListeners;
    private IReadOnlyList<AbstractModel>? _effectiveRunHookListeners;
    private IReadOnlyList<PowerModel>? _effectivePowers;
    private ForkableDictionary<Player, int>? _drawNextTurn;
    private ForkableDictionary<Creature, int>? _temporaryDexterity;
    private ForkableSet<(Creature Owner, Type Type)>? _skipNextDurationTick;
    private ForkableSet<Creature>? _skipNextMove;
    private ForkableDictionary<Creature, int>? _pressureGunBonus;
    private ForkableDictionary<Creature, int>? _steamEruptionDamage;
    private ForkableDictionary<Creature, SteamEruptionPhase>? _steamEruptionPhases;
    private ForkableDictionary<Creature, int>? _aeonglassAdditionalStrength;
    private ForkableDictionary<Creature, int>? _aeonglassWitherUpgradeCount;
    private ForkableDictionary<Creature, bool>? _nemesisShouldApplyIntangible;
    private ForkableDictionary<Creature, int>? _tenderCardsPlayed;
    private ForkableDictionary<Creature, int>? _attacksPlayedThisTurn;
    private ForkableDictionary<Creature, int>? _shivsPlayedThisTurn;
    private ForkableDictionary<Creature, int>? _blockCardsPlayedThisTurn;
    private ForkableDictionary<Creature, int>? _skillCardsPlayedThisTurn;
    private ForkableDictionary<Creature, int>? _cardsExhaustedThisTurn;
    private ForkableSet<Creature>? _doomAppliersThisTurn;
    private ForkableSet<Creature>? _unblockedDamageThisTurn;
    private ForkableDictionary<(Creature Dealer, Creature Receiver), int>? _poweredAttackHitsThisTurn;
    private ForkableDictionary<Creature, int>? _cardsDiscardedThisTurn;
    private ForkableDictionary<Creature, int>? _creatureAttacksThisTurn;
    private ForkableDictionary<Player, int>? _energySpentThisTurn;
    private ForkableDictionary<Player, int>? _starsGainedThisTurn;
    private ForkableDictionary<Player, int>? _nonHandDrawsThisTurn;
    private ForkableSet<Creature>? _enemiesIntendingAttack;
    private bool _hasPredictedEnemyIntents;
    private ForkableSet<Player>? _ringingCardPlayed;
    private ForkableDictionary<Player, int>? _playerTurnNumbers;
    private ForkableList<Creature> _allies;
    private ForkableList<Creature> _enemies;
    private ForkableList<Creature> _knownEnemies;
    private ForkableList<Creature> _escapedCreatures;
    private IReadOnlyList<Creature>? _creatures;
    private uint _nextCreatureId;
    private int _roundNumber;
    private CombatSide _currentSide;
    private bool _rootMaterialized;
    private CombatPredictionState? _predictionState;

    public SimulatedCombatState(CombatState inner)
    {
        if (!NGame.IsMainThread())
            throw new InvalidOperationException("Live combat state can only be captured on the main thread.");
        _runState = inner.RunState;
        _runRngSnapshot = inner.RunState.Rng.ToSerializable();
        _currentActIndex = inner.RunState.CurrentActIndex;
        _currentRoomType = inner.RunState.CurrentRoom?.RoomType;
        _currentMapCoord = inner.RunState.CurrentMapCoord;
        _cardMultiplayerConstraint = inner.RunState.CardMultiplayerConstraint;
        _playerCreatures = inner.PlayerCreatures.ToArray();
        _players = inner.Players.ToArray();
        _encounter = inner.Encounter;
        _encounterSlots = inner.Encounter?.Slots.ToArray() ?? [];
        _rootHistory = RootCombatHistorySnapshot.Capture();
        _rootCreatures = inner.Creatures
            .Concat(inner.Players.Select(player => player.Osty).OfType<Creature>())
            .ToHashSet();
        _rootDeadCreatures = _rootCreatures.Where(creature => creature.CurrentHp <= 0).ToHashSet();
        Dictionary<AbstractModel, AbstractModel> rootModelClones = [];
        foreach (BadgeModel badge in inner.BadgeModels)
            rootModelClones.Add(badge, PredictionUtils.CloneModelForSimulation(badge));
        if (inner.MultiplayerScalingModel is { } liveMultiplayerScaling)
        {
            MultiplayerScalingModel detachedMultiplayerScaling =
                PredictionUtils.CloneModelForSimulation(liveMultiplayerScaling);
            MultiplayerScalingRunStateField.SetValue(detachedMultiplayerScaling, null);
            MultiplayerScalingCombatStateField.SetValue(detachedMultiplayerScaling, null);
            _multiplayerScalingModel = detachedMultiplayerScaling;
            rootModelClones.Add(liveMultiplayerScaling, detachedMultiplayerScaling);
        }
        else
        {
            _multiplayerScalingModel = null;
        }
        ModifierModel[] modifiers = inner.Modifiers
            .Select(PredictionUtils.CloneModelForSimulation)
            .ToArray();
        _modifiers = modifiers;
        for (int index = 0; index < modifiers.Length; index++)
            rootModelClones.Add(inner.Modifiers[index], modifiers[index]);
        Dictionary<Player, RelicModel[]> rootRelics = [];
        Dictionary<RelicModel, RelicModel> rootRelicSources = [];
        foreach (Player player in inner.Players)
        {
            RelicModel[] relics = player.Relics
                .Select(relic => PredictionUtils.CreateRelic(relic, player))
                .ToArray();
            rootRelics.Add(player, relics);
            for (int index = 0; index < relics.Length; index++)
            {
                rootModelClones.Add(player.Relics[index], relics[index]);
                rootRelicSources.Add(relics[index], player.Relics[index]);
            }
        }
        _rootRelics = rootRelics;
        _rootRelicSources = rootRelicSources;
        _rootPotionSlotCounts = inner.Players.ToDictionary(player => player, player => player.PotionSlots.Count);
        _rootPlayerTurnNumbers = inner.Players.ToDictionary(
            player => player,
            player => player.PlayerCombatState is { } state
                ? state.TurnNumber
                : throw new InvalidOperationException($"Player {player.NetId} has no combat state to capture."));
        _rootPowerAmounts = _rootCreatures
            .SelectMany(creature => creature.Powers)
            .ToDictionary(power => (power.Owner, power.GetType()), power => power.Amount);
        _playerNames = inner.Players.ToDictionary(
            player => player,
            player => PlatformUtil.GetPlayerName(RunManager.Instance.NetService.Platform, player.NetId));
        HashSet<CardModel> piledCards = inner.Players
            .Where(player => player.PlayerCombatState != null)
            .SelectMany(player => player.PlayerCombatState!.AllCards)
            .ToHashSet();
        _rootFloatingCards = ((List<CardModel>)AllCombatCardsField.GetValue(inner)!)
            .Where(card => !piledCards.Contains(card))
            .ToHashSet();
        _potionSlots = [];
        foreach (Player player in _players)
        {
            int slotCount = _rootPotionSlotCounts[player];
            for (int slot = 0; slot < slotCount; slot++)
            {
                PotionModel? original = player.GetPotionAtSlotIndex(slot);
                PotionModel? potion = original == null
                    ? null
                    : PredictionUtils.CreatePotion(original, player);
                _potionSlots.Add((player, slot), potion);
                if (original != null)
                    rootModelClones.Add(original, potion!);
            }
        }
        AbstractModel[] liveCombatHookListeners = inner.IterateHookListeners().ToArray();
        AbstractModel[] liveRunHookListeners = inner.RunState.IterateHookListeners(inner).ToArray();
        RunState concreteRunState = inner.RunState as RunState
            ?? throw new InvalidOperationException("Combat prediction requires a concrete RunState.");
        _modHookSubscribers = PredictionModHookSubscriberCapture.Capture(
            concreteRunState,
            inner,
            liveRunHookListeners);
        _rootMaxHandSizes = _modHookSubscribers.MaxHandSizes;
        int standardCombatListenerCount =
            liveCombatHookListeners.Length - _modHookSubscribers.CombatSubscribers.Length;
        if (standardCombatListenerCount < 0)
            throw new InvalidOperationException("Combat hook listener snapshot is shorter than its mod subscriber suffix.");
        for (int index = 0; index < _modHookSubscribers.CombatSubscribers.Length; index++)
        {
            if (!ReferenceEquals(
                    liveCombatHookListeners[standardCombatListenerCount + index],
                    _modHookSubscribers.CombatSubscribers[index]))
            {
                throw new InvalidOperationException("Combat hook listener snapshot does not end with mod subscribers.");
            }
        }
        _rootHookListeners = liveCombatHookListeners
            .Take(standardCombatListenerCount)
            .Select(listener => rootModelClones.GetValueOrDefault(listener, listener))
            .Where(listener => listener is not CardModel
                and not AfflictionModel
                and not EnchantmentModel
                and not OrbModel)
            .ToArray();
        int runOnlyListenerCount = liveRunHookListeners.Length - liveCombatHookListeners.Length;
        if (runOnlyListenerCount < 0)
            throw new InvalidOperationException("Run hook listener snapshot is shorter than its combat suffix.");
        for (int index = 0; index < liveCombatHookListeners.Length; index++)
        {
            if (!ReferenceEquals(liveRunHookListeners[runOnlyListenerCount + index], liveCombatHookListeners[index]))
                throw new InvalidOperationException("Run hook listener snapshot does not end with the combat listeners.");
        }
        int standardRunListenerCount = runOnlyListenerCount - _modHookSubscribers.RunSubscribers.Length;
        if (standardRunListenerCount < 0)
            throw new InvalidOperationException("Run hook listener prefix is shorter than its mod subscriber suffix.");
        for (int index = 0; index < _modHookSubscribers.RunSubscribers.Length; index++)
        {
            if (!ReferenceEquals(
                    liveRunHookListeners[standardRunListenerCount + index],
                    _modHookSubscribers.RunSubscribers[index]))
            {
                throw new InvalidOperationException("Run hook listener prefix does not end with mod subscribers.");
            }
        }
        List<AbstractModel> rootRunHookListeners = [];
        foreach (AbstractModel listener in liveRunHookListeners.Take(standardRunListenerCount))
        {
            if (!rootModelClones.TryGetValue(listener, out AbstractModel? captured))
            {
                if (listener is CardModel card)
                {
                    CardModel clone = PredictionUtils.CloneCardStateForSimulation(card);
                    rootModelClones.Add(card, clone);
                    if (card.Enchantment != null && clone.Enchantment != null)
                        rootModelClones.TryAdd(card.Enchantment, clone.Enchantment);
                    captured = clone;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unexpected standard run hook listener {listener.GetType().FullName}.");
                }
            }
            rootRunHookListeners.Add(captured);
        }
        _rootRunHookListeners = rootRunHookListeners.ToArray();
        _allies = new ForkableList<Creature>(inner.Allies);
        _enemies = new ForkableList<Creature>(inner.Enemies);
        _knownEnemies = new ForkableList<Creature>(inner.Enemies);
        _escapedCreatures = new ForkableList<Creature>(inner.EscapedCreatures);
        _nextCreatureId = (uint)NextCreatureIdField.GetValue(inner)!;
        _roundNumber = inner.RoundNumber;
        _currentSide = inner.CurrentSide;
        _deathPhases = BuildInitialDeathPhases(inner.Enemies);
        _playerTurnNumbers = [];
        _simulatedPlayerGold = [];
        foreach (Player player in _players)
        {
            PlayerCombatState playerState = player.PlayerCombatState
                ?? throw new InvalidOperationException($"Player {player.NetId} has no combat state to capture.");
            _playerTurnNumbers.Add(player, playerState.TurnNumber);
            _simulatedPlayerGold.Add(player, player.Gold);
        }
    }

    private SimulatedCombatState(SimulatedCombatState source)
    {
        _runState = source._runState;
        _runRngSnapshot = source._runRngSnapshot;
        _currentActIndex = source._currentActIndex;
        _currentRoomType = source._currentRoomType;
        _currentMapCoord = source._currentMapCoord;
        _cardMultiplayerConstraint = source._cardMultiplayerConstraint;
        _modHookSubscribers = source._modHookSubscribers;
        _rootMaxHandSizes = source._rootMaxHandSizes;
        _playerCreatures = source._playerCreatures;
        _players = source._players;
        _modifiers = source._modifiers;
        _multiplayerScalingModel = source._multiplayerScalingModel;
        _encounter = source._encounter;
        _encounterSlots = source._encounterSlots;
        _rootHistory = source._rootHistory;
        _rootCreatures = source._rootCreatures;
        _rootHookListeners = source._rootHookListeners;
        _rootRunHookListeners = source._rootRunHookListeners;
        _rootRelics = source._rootRelics;
        _rootRelicSources = source._rootRelicSources;
        _rootPotionSlotCounts = source._rootPotionSlotCounts;
        _rootPlayerTurnNumbers = source._rootPlayerTurnNumbers;
        _rootPowerAmounts = source._rootPowerAmounts;
        _playerNames = source._playerNames;
        _rootFloatingCards = source._rootFloatingCards;
        _rootDeadCreatures = source._rootDeadCreatures;
        _allies = [];
        _enemies = [];
        _knownEnemies = [];
        _escapedCreatures = [];
    }

    public IRunState RunState => _runState;
    internal int CurrentActIndex => _currentActIndex;
    internal RoomType? CurrentRoomType => _currentRoomType;
    internal MapCoord? CurrentMapCoord => _currentMapCoord;
    public CardMultiplayerConstraint CardMultiplayerConstraint => _cardMultiplayerConstraint;
    public IReadOnlyList<Creature> Allies => _allies;
    public IReadOnlyList<Creature> Enemies => _enemies;
    public IReadOnlyList<Creature> KnownEnemies => _knownEnemies;
    public IReadOnlyList<Creature> Creatures => _creatures ??= new CombinedRosterView(_allies, _enemies);
    public IReadOnlyList<Creature> PlayerCreatures => _playerCreatures;
    public IReadOnlyList<Player> Players => _players;
    public IReadOnlyList<ModifierModel> Modifiers => _modifiers;
    public MultiplayerScalingModel? MultiplayerScalingModel => _multiplayerScalingModel;
    public int RoundNumber { get => _roundNumber; set => _roundNumber = value; }
    public CombatSide CurrentSide { get => _currentSide; set => _currentSide = value; }
    public EncounterModel? Encounter => _encounter;
    public IReadOnlyList<Creature> EscapedCreatures => _escapedCreatures;
    public IReadOnlyList<Creature> CreaturesOnCurrentSide => GetCreaturesOnSide(CurrentSide);
    public IReadOnlyList<Creature> HittableEnemies => _deathPhases == null
        ? _enemies
        : _enemies.Where(creature =>
            _deathPhases.GetValueOrDefault(creature) == PredictedDeathPhase.None).ToArray();

    internal string? NextFreeSlot()
        => _encounter == null
            ? null
            : _encounterSlots.FirstOrDefault(
                slot => _enemies.All(creature => creature.SlotName != slot),
                string.Empty);

    internal string? LastFreeSlot()
        => _encounter == null
            ? null
            : _encounterSlots.LastOrDefault(
                slot => _enemies.All(creature => creature.SlotName != slot));
    public bool HasPendingChoice => PendingTurnStartChoice != null || PendingKnowledgeDemonChoice != null;
    public event Action<ICombatState>? CreaturesChanged
    {
        add { }
        remove { }
    }

    public bool IsEnemyIntendingToAttack(Creature enemy)
    {
        if (_hasPredictedEnemyIntents)
            return _enemiesIntendingAttack?.Contains(enemy) == true;
        if (_rootMaterialized && _rootCreatures.Contains(enemy))
            throw new InvalidOperationException($"Root intent state was not captured for {enemy.Name}.");
        return enemy.Monster?.IntendsToAttack == true;
    }

    public void SetPredictedEnemyIntents(IEnumerable<Creature> attackingEnemies)
    {
        _enemiesIntendingAttack = [.. attackingEnemies];
        _hasPredictedEnemyIntents = true;
    }

    public int GetPlayerTurnNumber(Player player)
    {
        if (_playerTurnNumbers?.TryGetValue(player, out int simulated) == true)
            return simulated;
        throw new InvalidOperationException($"Player {player.NetId} is outside the captured turn state.");
    }

    public int GetRootPlayerTurnNumber(Player player)
        => _rootPlayerTurnNumbers.TryGetValue(player, out int turn)
            ? turn
            : throw new InvalidOperationException($"Player {player.NetId} is outside the captured root turn state.");

    public void AdvancePlayerTurn(Player player)
    {
        int nextTurn = GetPlayerTurnNumber(player) + 1;
        (_playerTurnNumbers ??= [])[player] = nextTurn;
    }

    public void SnapshotPowerAmountsAtTurnStart(IEnumerable<Creature> participants)
    {
        HashSet<Creature> owners = participants.ToHashSet();
        foreach (PowerModel power in EffectivePowers()
                     .Where(power => owners.Contains(power.Owner))
                     .ToArray())
        {
            PowerModel mutable = GetMutablePowerInstance(power);
            mutable.AmountOnTurnStart = mutable.Amount;
        }
        InvalidateHookListeners();
    }

    public void Apply<T>(Creature target, int amount, Creature? applier = null) where T : PowerModel
    {
        if (amount == 0)
            return;
        T canonical = ModelDb.Power<T>();
        amount = ModifyPowerAmountForRelics(canonical, target, amount, applier);
        if (canonical.GetTypeForAmount(amount) == MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff
            && ConsumeArtifact(target))
        {
            return;
        }
        PowerModel simulated = GetOrCreatePower(target, canonical, applier);
        int previousAmount = simulated._amount;
        simulated._amount = Math.Clamp(simulated._amount + amount, -999_999_999, 999_999_999);
        InvalidateHookListeners();
        int applied = simulated._amount - previousAmount;
        RecordPowerAmountChange(simulated, applied, applier);
        RecordPossessedStatChange(simulated, applied, applier);
        if (simulated is ShrinkPower shrink
            && applied != 0
            && shrink.Applier is { IsMonster: true } shrinkApplier)
        {
            ((StringVar)shrink.DynamicVars["ApplierName"]).StringValue =
                shrinkApplier.Monster!.Title.GetFormattedText();
        }
        if (simulated is DoomPower && applied > 0 && applier != null)
            (_doomAppliersThisTurn ??= []).Add(applier);
        if (simulated is RitualPower ritual
            && previousAmount <= 0
            && simulated._amount > 0
            && target.IsEnemy)
        {
            ritual._wasJustAppliedByEnemy = true;
        }
        if (simulated is KnockdownPower knockdown && applier != null)
        {
            Player? applyingPlayer = applier.Player
                ?? Players.FirstOrDefault(player => player.Creature.CombatId == applier.CombatId);
            if (applyingPlayer == null)
                throw new InvalidOperationException("击倒 Power 的施加者不是战斗中的玩家。");
            ((StringVar)knockdown.DynamicVars["Applier"]).StringValue = _playerNames[applyingPlayer];
        }
    }

    public void ApplyPower(Type powerType, Creature target, int amount, Creature? applier = null)
    {
        if (!typeof(PowerModel).IsAssignableFrom(powerType))
            throw new ArgumentException($"{powerType.FullName} is not a PowerModel type.", nameof(powerType));
        ApplyPowerDelegate apply = ApplyPowerDelegates.GetOrAdd(powerType, static type =>
            GenericApplyMethod.MakeGenericMethod(type).CreateDelegate<ApplyPowerDelegate>());
        apply(this, target, amount, applier);
    }

    public void ApplyPowerSkippingNextDurationTick(
        Type powerType,
        Creature target,
        int amount,
        Creature? applier = null)
    {
        bool alreadyPresent = EffectivePowers().Any(power =>
            power.GetType() == powerType && ReferenceEquals(power.Owner, target) && power.Amount > 0);
        ApplyPower(powerType, target, amount, applier);
        if (!alreadyPresent && amount > 0)
            (_skipNextDurationTick ??= []).Add((target, powerType));
    }

    public void ApplyTemporaryStrengthLoss(
        Type powerType,
        Creature target,
        int amount,
        Creature? applier = null)
    {
        if (!typeof(PowerModel).IsAssignableFrom(powerType))
            throw new ArgumentException($"{powerType.FullName} is not a PowerModel type.", nameof(powerType));
        ApplyTemporaryStrengthLossDelegate apply = TemporaryStrengthLossDelegates.GetOrAdd(
            powerType,
            static type => GenericTemporaryStrengthLossMethod.MakeGenericMethod(type)
                .CreateDelegate<ApplyTemporaryStrengthLossDelegate>());
        apply(this, target, amount, applier);
    }

    public void ApplyTemporaryDexterity(
        Type powerType,
        Creature target,
        int amount,
        Creature? applier = null)
    {
        if (!typeof(PowerModel).IsAssignableFrom(powerType))
            throw new ArgumentException($"{powerType.FullName} is not a PowerModel type.", nameof(powerType));
        ApplyTemporaryDexterityDelegate apply = TemporaryDexterityDelegates.GetOrAdd(
            powerType,
            static type => GenericTemporaryDexterityMethod.MakeGenericMethod(type)
                .CreateDelegate<ApplyTemporaryDexterityDelegate>());
        apply(this, target, amount, applier);
    }

    public int GetAmount<T>(Creature target) where T : PowerModel
    {
        if (_powers != null && _powers.TryGetValue((target, typeof(T)), out PowerModel? power))
            return power.Amount;
        if (_rootCreatures.Contains(target))
            return 0;
        return target.GetPower<T>()?.Amount ?? 0;
    }

    public T? GetPower<T>(Creature target) where T : PowerModel
    {
        if (_powers != null && _powers.TryGetValue((target, typeof(T)), out PowerModel? power))
            return (T)power;
        if (_rootCreatures.Contains(target))
            return null;
        return target.GetPower<T>();
    }

    internal T? GetMutablePower<T>(Creature target) where T : PowerModel
    {
        T? power = GetPower<T>(target);
        return power == null ? null : (T)GetMutablePowerInstance(power);
    }

    public void ApplyFromMonster<T>(Creature target, int amount, Creature applier) where T : PowerModel
    {
        bool alreadyPresent = GetAmount<T>(target) > 0;
        Apply<T>(target, amount, applier);
        if (!alreadyPresent && amount > 0 && GetAmount<T>(target) > 0)
            (_skipNextDurationTick ??= []).Add((target, typeof(T)));
    }

    public void TickDuration<T>(Creature target) where T : PowerModel
    {
        if (_skipNextDurationTick?.Remove((target, typeof(T))) == true)
            return;
        T? power = GetPower<T>(target);
        if (power?.SkipNextDurationTick == true)
        {
            T mutable = (T)GetOrCreatePower(target, ModelDb.Power<T>(), power.Applier);
            mutable.SkipNextDurationTick = false;
            return;
        }
        int amount = GetAmount<T>(target);
        if (amount > 0)
            SetAmount<T>(target, amount - 1);
    }

    public void SetAmount<T>(Creature target, int amount) where T : PowerModel
    {
        int current = GetAmount<T>(target);
        if (current == amount)
            return;
        T canonical = ModelDb.Power<T>();
        PowerModel simulated = GetOrCreatePower(target, canonical, null);
        simulated._amount = Math.Clamp(amount, -999_999_999, 999_999_999);
        InvalidateHookListeners();
    }

    public void SetPowerAmount(PowerModel power, int amount)
    {
        PowerModel mutable = GetMutablePowerInstance(power);
        mutable._amount = Math.Clamp(amount, -999_999_999, 999_999_999);
        InvalidateHookListeners();
    }

    public void SetPowerDynamicVar(
        CombatPredictionSimulator simulator,
        PowerModel power,
        string key,
        int value)
    {
        PowerModel mutable = GetMutablePowerInstance(power);
        if (!mutable.DynamicVars.TryGetValue(key, out var dynamicVar))
            throw new InvalidOperationException($"Power {power.Id.Entry} 不存在动态变量 {key}。");
        dynamicVar.BaseValue = value;
        simulator.StateStore.RemapModel(power, mutable);
    }

    private PowerModel GetMutablePowerInstance(PowerModel power)
    {
        if (_addedPowerInstances?.Contains(power) == true)
            return power;

        (Creature, Type) key = (power.Owner, power.GetType());
        if (_powers != null && _powers.TryGetValue(key, out PowerModel? simulated))
            return simulated;

        simulated = PredictionUtils.CloneModelForSimulation(power);
        simulated._owner = power.Owner;
        simulated._applier = power.Applier;
        simulated._target = power.Target;
        simulated._amount = power.Amount;
        (_powers ??= []).Add(key, simulated);
        InvalidateHookListeners();
        return simulated;
    }

    public void ApplyTargeted<T>(Creature owner, Creature target, int amount, Creature? applier = null)
        where T : PowerModel
    {
        if (amount == 0)
            return;
        T canonical = ModelDb.Power<T>();
        if (canonical.GetTypeForAmount(amount) == MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff
            && ConsumeArtifact(target))
        {
            return;
        }
        PowerModel simulated = GetOrCreatePower(owner, canonical, applier);
        simulated._target = target;
        simulated._amount = Math.Clamp(simulated._amount + amount, -999_999_999, 999_999_999);
        InvalidateHookListeners();
    }

    public void RecordThievery(CombatPredictionSimulator simulator, Creature owner)
    {
        ThieveryPower? source = GetPower<ThieveryPower>(owner);
        if (source?.Target?.Player is not { } target
            || simulator.State.GetCreature(source.Target).IsDead)
            return;
        int stolen = Math.Min(source.Amount, GetPlayerGold(target));
        if (stolen <= 0)
            return;
        RecordStolenGold(simulator, stolen);
        ThieveryPower simulated = (ThieveryPower)GetOrCreatePower(
            owner,
            ModelDb.Power<ThieveryPower>(),
            source.Applier);
        simulated._target = source.Target;
        simulated.DynamicVars.Gold.BaseValue += stolen;
        LosePlayerGold(target, stolen);
    }

    public bool GetNemesisShouldApplyIntangible(Creature owner)
    {
        if (_nemesisShouldApplyIntangible?.TryGetValue(owner, out bool simulated) == true)
            return simulated;
        NemesisPower power = GetPower<NemesisPower>(owner)
            ?? throw new InvalidOperationException("奈梅西斯状态缺少对应 Power。");
        return (bool)NemesisShouldApplyIntangibleField.GetValue(power)!;
    }

    public void SetNemesisShouldApplyIntangible(Creature owner, bool value)
        => (_nemesisShouldApplyIntangible ??= [])[owner] = value;

    public int GetTenderCardsPlayed(Creature owner)
    {
        if (_tenderCardsPlayed?.TryGetValue(owner, out int simulated) == true)
            return simulated;
        TenderPower power = GetPower<TenderPower>(owner)
            ?? throw new InvalidOperationException("温柔状态缺少对应 Power。");
        return (int)TenderCardsPlayedField.GetValue(power)!;
    }

    public void RecordTenderCardPlayed(Creature owner)
        => (_tenderCardsPlayed ??= [])[owner] = GetTenderCardsPlayed(owner) + 1;

    public void ResetTenderCardsPlayed(Creature owner)
        => (_tenderCardsPlayed ??= [])[owner] = 0;

    private PowerModel GetOrCreatePower<T>(Creature target, T canonical, Creature? applier)
        where T : PowerModel
    {
        (Creature, Type) key = (target, typeof(T));
        if (_powers != null && _powers.TryGetValue(key, out PowerModel? simulated))
            return simulated;

        T? existingPower = _rootCreatures.Contains(target) ? null : target.GetPower<T>();
        simulated = existingPower != null
            ? PredictionUtils.CloneModelForSimulation(existingPower)
            : canonical.ToMutable();
        simulated._owner = target;
        simulated._applier = existingPower?.Applier ?? applier;
        simulated._target = target;
        simulated._amount = existingPower?.Amount ?? 0;
        if (existingPower == null)
            simulated.AmountOnTurnStart = 0;
        (_powers ??= []).Add(key, simulated);
        InvalidateHookListeners();
        return simulated;
    }

    public void AddEnergyNextTurn(Player player, int amount)
        => Apply<EnergyNextTurnPower>(player.Creature, amount, player.Creature);

    public void AddDrawNextTurn(Player player, int amount)
    {
        _drawNextTurn ??= [];
        _drawNextTurn[player] = _drawNextTurn.GetValueOrDefault(player) + amount;
    }

    public int ConsumeEnergyNextTurn(Player player)
    {
        int amount = GetAmount<EnergyNextTurnPower>(player.Creature);
        SetAmount<EnergyNextTurnPower>(player.Creature, 0);
        return amount;
    }

    public int ConsumeDrawNextTurn(Player player)
        => Consume(_drawNextTurn, player);

    public void ApplyPiercingWail(Creature creature, int amount, Creature? applier)
        => ApplyTemporaryStrengthLoss<PiercingWailPower>(creature, amount, applier);

    public void ApplyTemporaryStrengthLoss<T>(Creature creature, int amount, Creature? applier)
        where T : PowerModel
    {
        int before = GetAmount<T>(creature);
        Apply<T>(creature, amount, applier);
        int applied = GetAmount<T>(creature) - before;
        if (applied <= 0)
            return;
        Apply<StrengthPower>(creature, -applied, applier);
    }

    public void ApplyTemporaryStrengthGain<T>(Creature creature, int amount, Creature? applier)
        where T : PowerModel
    {
        int before = GetAmount<T>(creature);
        Apply<T>(creature, amount, applier);
        int applied = GetAmount<T>(creature) - before;
        if (applied > 0)
            Apply<StrengthPower>(creature, applied, applier);
    }

    public void ApplyTemporaryDexterity<T>(Creature creature, int amount, Creature? applier)
        where T : PowerModel
    {
        int before = GetAmount<T>(creature);
        Apply<T>(creature, amount, applier);
        int applied = GetAmount<T>(creature) - before;
        if (applied <= 0)
            return;
        Apply<DexterityPower>(creature, applied, applier);
        _temporaryDexterity ??= [];
        _temporaryDexterity[creature] = _temporaryDexterity.GetValueOrDefault(creature) + applied;
    }

    public void ApplyTemporaryFocus<T>(Creature creature, int amount, Creature? applier)
        where T : PowerModel
    {
        int before = GetAmount<T>(creature);
        Apply<T>(creature, amount, applier);
        int applied = GetAmount<T>(creature) - before;
        if (applied > 0)
            Apply<FocusPower>(creature, applied, applier);
    }

    public void ApplyTemporaryFocusLoss<T>(Creature creature, int amount, Creature? applier)
        where T : PowerModel
    {
        int before = GetAmount<T>(creature);
        Apply<T>(creature, amount, applier);
        int applied = GetAmount<T>(creature) - before;
        if (applied > 0)
            Apply<FocusPower>(creature, -applied, applier);
    }

    public void ApplyAnticipate(Creature creature, int amount, Creature? applier)
    {
        int before = GetAmount<AnticipatePower>(creature);
        Apply<AnticipatePower>(creature, amount, applier);
        int applied = GetAmount<AnticipatePower>(creature) - before;
        if (applied <= 0)
            return;
        Apply<DexterityPower>(creature, applied, applier);
        _temporaryDexterity ??= [];
        _temporaryDexterity[creature] = _temporaryDexterity.GetValueOrDefault(creature) + applied;
    }

    public void RestoreTemporaryDexterity()
    {
        if (_temporaryDexterity == null)
            return;
        foreach ((Creature creature, int amount) in _temporaryDexterity)
        {
            Apply<DexterityPower>(creature, -amount);
            SetAmount<AnticipatePower>(creature, 0);
            SetAmount<SpeedPotionPower>(creature, 0);
            SetAmount<HelicalDartPower>(creature, 0);
            SetAmount<FadePower>(creature, 0);
        }
        _temporaryDexterity.Clear();
    }

    public void RestoreTemporaryStrength(IEnumerable<Creature> participants)
    {
        HashSet<Creature> participantSet = participants.ToHashSet();
        foreach (TemporaryStrengthPower power in EffectivePowers()
                     .OfType<TemporaryStrengthPower>()
                     .Where(power => participantSet.Contains(power.Owner) && power.Amount > 0)
                     .ToArray())
        {
            int strengthDelta = power.TypeForCurrentAmount == PowerType.Buff
                ? -power.Amount
                : power.Amount;
            SetPowerAmount(power, 0);
            Apply<StrengthPower>(power.Owner, strengthDelta, power.Owner);
        }
    }

    public void RestoreTemporaryFocus()
    {
        foreach (Creature creature in Creatures)
        {
            int amount = GetAmount<HotfixPower>(creature)
                + GetAmount<SynchronizePower>(creature)
                + GetAmount<FocusedStrikePower>(creature);
            if (amount > 0)
            {
                Apply<FocusPower>(creature, -amount);
                SetAmount<HotfixPower>(creature, 0);
                SetAmount<SynchronizePower>(creature, 0);
                SetAmount<FocusedStrikePower>(creature, 0);
            }
            int focusLoss = GetAmount<HyperbeamFocusDownPower>(creature);
            if (focusLoss > 0)
            {
                Apply<FocusPower>(creature, focusLoss);
                SetAmount<HyperbeamFocusDownPower>(creature, 0);
            }
        }
    }

    public void StunNextMove(Creature creature)
        => (_skipNextMove ??= []).Add(creature);

    public bool WillSkipNextMove(Creature creature)
        => _skipNextMove?.Contains(creature) == true;

    public bool ConsumeStunNextMove(Creature creature)
        => _skipNextMove?.Remove(creature) == true;

    public void IncrementCrimsonMantle(Creature owner, int block)
    {
        Apply<CrimsonMantlePower>(owner, block, owner);
        CrimsonMantlePower power = GetPower<CrimsonMantlePower>(owner)
            ?? throw new InvalidOperationException("绯红披风 Power 创建失败。");
        power.DynamicVars["SelfDamage"].BaseValue++;
    }

    public void IncrementSandpitTargeting(Creature target)
    {
        Creature owner = Enemies.FirstOrDefault(enemy => GetPower<SandpitPower>(enemy)?.Target == target)
            ?? throw new InvalidOperationException("挣扎没有找到以玩家为目标的流沙坑状态。");
        (Creature, Type) key = (owner, typeof(SandpitPower));
        if (_powers?.TryGetValue(key, out PowerModel? existing) == true)
        {
            existing._amount++;
            InvalidateHookListeners();
            return;
        }

        SandpitPower source = GetPower<SandpitPower>(owner)
            ?? throw new InvalidOperationException("挣扎找到沙坑所有者但未找到对应状态。");
        SandpitPower mutable = PredictionUtils.CloneModelForSimulation(source);
        mutable._owner = owner;
        mutable._applier = source.Applier;
        mutable._target = target;
        mutable._amount = source.Amount + 1;
        (_powers ??= []).Add(key, mutable);
        InvalidateHookListeners();
    }

    public bool TriggerPlayerTurnStart(
        CombatPredictionSimulator simulator,
        Creature owner,
        TurnStartChoiceCursor choices,
        bool isExtraTurn = false)
    {
        Player player = owner.Player
            ?? throw new InvalidOperationException("玩家回合开始钩子的持有者没有 Player。");
        _ringingCardPlayed?.Remove(player);
        if (TurnStartRelicSupport.TriggerAfterPlayerTurnStart(simulator, this, player, choices))
            return true;
        if (TurnStartPowerSupport.TriggerAfterPlayerTurnStart(simulator, this, player, choices))
            return true;
        TriggerSideTurnStart(
            simulator,
            CombatSide.Player,
            [owner],
            decrementPlating: GetPlayerTurnNumber(player) != 1,
            isExtraTurn);
        return false;
    }

    public bool TriggerPlayerTurnStart(
        CombatPredictionSimulator simulator,
        Creature owner,
        IReadOnlyList<PlanCardChoice>? turnStartChoices,
        bool isExtraTurn = false)
    {
        ClearPendingTurnStartChoice();
        return TriggerPlayerTurnStart(
            simulator,
            owner,
            new TurnStartChoiceCursor(turnStartChoices),
            isExtraTurn);
    }

    public bool ShouldClearBlock(Creature owner)
        => ShouldClearBlock(owner, out _);

    public bool ShouldClearBlock(Creature owner, out AbstractModel? preventer)
        => Hook.ShouldClearBlock(this, owner, out preventer);

    public void TriggerSideTurnStart(
        CombatPredictionSimulator simulator,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        bool decrementPlating,
        bool isExtraTurn = false)
    {
        foreach (Creature owner in participants)
            TriggerBaseSideTurnStart(owner, decrementPlating);
        PersistentPowerSupport.TriggerAfterSideTurnStart(
            simulator,
            this,
            side,
            participants,
            isExtraTurn);
        TurnStartPowerSupport.TriggerAfterSideTurnStart(simulator, this, side, participants);
        TurnStartRelicSupport.TriggerAfterSideTurnStart(simulator, this, side, participants);
        PowerLifecycleSupport.ResolvePowerAmountChanges(simulator, this);
    }

    private void TriggerBaseSideTurnStart(
        Creature owner,
        bool decrementPlating)
    {
        ResetCardLifecycleTurn(owner);
        (_attacksPlayedThisTurn ??= [])[owner] = 0;
        (_shivsPlayedThisTurn ??= [])[owner] = 0;
        (_blockCardsPlayedThisTurn ??= [])[owner] = 0;
        (_skillCardsPlayedThisTurn ??= [])[owner] = 0;
        (_cardsExhaustedThisTurn ??= [])[owner] = 0;
        (_cardsDiscardedThisTurn ??= [])[owner] = 0;
        (_creatureAttacksThisTurn ??= [])[owner] = 0;
        if (owner.Player is { } ownerPlayer)
        {
            (_energySpentThisTurn ??= [])[ownerPlayer] = 0;
            (_starsGainedThisTurn ??= [])[ownerPlayer] = 0;
            (_nonHandDrawsThisTurn ??= [])[ownerPlayer] = 0;
        }
        _doomAppliersThisTurn?.Remove(owner);
        _unblockedDamageThisTurn?.Remove(owner);
        if (_poweredAttackHitsThisTurn != null)
        {
            foreach ((Creature Dealer, Creature Receiver) key in _poweredAttackHitsThisTurn.Keys
                         .Where(key => key.Dealer == owner)
                         .ToArray())
            {
                _poweredAttackHitsThisTurn.Remove(key);
            }
        }
        TickDuration<BlurPower>(owner);
        if (GetAmount<DrawCardsNextTurnPower>(owner) > 0)
            SetAmount<DrawCardsNextTurnPower>(owner, 0);
        if (decrementPlating)
        {
            PlatingPower? plating = GetPower<PlatingPower>(owner);
            if (plating != null && plating.Amount > 0)
            {
                int decrement = plating.DynamicVars["Decrement"].IntValue;
                SetAmount<PlatingPower>(owner, plating.Amount - decrement);
            }
        }

        SlowPower? slow = GetPower<SlowPower>(owner);
        if (slow != null)
        {
            SlowPower reset = PredictionUtils.CloneModelForSimulation(slow);
            reset._owner = owner;
            reset._applier = slow.Applier;
            reset._target = slow.Target;
            reset._amount = slow.Amount;
            reset.DynamicVars["SlowAmount"].BaseValue = 0;
            reset.DynamicVars["DisplayAmount"].BaseValue = 0;
            (_powers ??= [])[(owner, typeof(SlowPower))] = reset;
            InvalidateHookListeners();
        }

    }

    public bool ConsumeRitualApplicationDelay(Creature owner)
    {
        RitualPower? ritual = GetPower<RitualPower>(owner);
        if (ritual is not { Amount: > 0 })
            return false;
        RitualPower mutable = (RitualPower)GetOrCreatePower(owner, ModelDb.Power<RitualPower>(), ritual.Applier);
        if (!mutable._wasJustAppliedByEnemy)
            return false;
        mutable._wasJustAppliedByEnemy = false;
        return true;
    }

    public void ClearNoDraw(Creature owner)
        => SetAmount<NoDrawPower>(owner, 0);

    public void IncreasePressureGun(Creature owner, int amount)
    {
        _pressureGunBonus ??= [];
        _pressureGunBonus[owner] = _pressureGunBonus.GetValueOrDefault(owner) + amount;
    }

    public int AdjustMonsterMoveDamage(Creature owner, string moveId, int damage)
    {
        if (owner.Monster?.GetType().Name == "TheForgotten" && moveId == "DREAD")
        {
            int rootDexterity = _rootPowerAmounts.GetValueOrDefault((owner, typeof(DexterityPower)));
            return damage + GetAmount<DexterityPower>(owner) - rootDexterity;
        }
        if (moveId == "PRESSURE_GUN_MOVE")
            return damage + (_pressureGunBonus?.GetValueOrDefault(owner) ?? 0);
        if (moveId == "EXPLODE_MOVE")
            return _steamEruptionDamage?.GetValueOrDefault(owner, damage) ?? damage;
        return damage;
    }

    public void PrepareSteamEruption(Creature owner)
    {
        (_steamEruptionDamage ??= [])[owner] = Math.Max(0, GetAmount<SteamEruptionPower>(owner));
        SetAmount<SteamEruptionPower>(owner, 0);
    }

    public bool TryTriggerSteamEruptionDeath(CombatPredictionSimulator simulator, Creature owner)
    {
        if (owner.Monster?.GetType().Name != "WaterfallGiant"
            || simulator.State.GetCreature(owner).IsAlive
            || GetAmount<SteamEruptionPower>(owner) <= 0
            || _steamEruptionPhases?.ContainsKey(owner) == true)
        {
            return false;
        }

        SimCreatureState creature = simulator.State.GetCreature(owner);
        creature.SetMaxHp(999_999_999);
        creature.CurrentHp = 999_999_999;
        creature.HpDisplay = HpDisplay.InfiniteWithoutNumbers;
        ForceMonsterMove(owner, "ABOUT_TO_BLOW_MOVE");
        (_steamEruptionPhases ??= [])[owner] = SteamEruptionPhase.AboutToBlow;
        RemovePowersAfterDeath(owner);
        return true;
    }

    public bool TryConsumeForcedMonsterMove(Creature owner, out string moveId, out int damage)
    {
        moveId = string.Empty;
        damage = 0;
        if (_steamEruptionPhases == null
            || !_steamEruptionPhases.TryGetValue(owner, out SteamEruptionPhase phase))
        {
            return false;
        }

        if (phase == SteamEruptionPhase.AboutToBlow)
        {
            PrepareSteamEruption(owner);
            _steamEruptionPhases[owner] = SteamEruptionPhase.Explode;
            moveId = "ABOUT_TO_BLOW_MOVE";
            return true;
        }

        damage = _steamEruptionDamage?.GetValueOrDefault(owner) ?? 0;
        _steamEruptionPhases.Remove(owner);
        moveId = "EXPLODE_MOVE";
        return true;
    }

    public bool TryGetForcedMoveId(Creature owner, out string moveId)
    {
        moveId = string.Empty;
        if (_steamEruptionPhases == null
            || !_steamEruptionPhases.TryGetValue(owner, out SteamEruptionPhase phase))
        {
            return false;
        }
        moveId = phase == SteamEruptionPhase.AboutToBlow
            ? "ABOUT_TO_BLOW_MOVE"
            : "EXPLODE_MOVE";
        return true;
    }

    public bool TryGetForcedAttackDamage(Creature owner, out int damage)
    {
        damage = 0;
        if (_steamEruptionPhases?.GetValueOrDefault(owner) != SteamEruptionPhase.Explode)
            return false;
        damage = _steamEruptionDamage?.GetValueOrDefault(owner) ?? 0;
        return true;
    }

    public int EffectiveEnemyHp(Creature enemy, SimCreatureState state)
    {
        if (_steamEruptionPhases?.ContainsKey(enemy) == true)
            return 0;
        if (enemy.Monster is TestSubject)
            return RemainingTestSubjectFormHp(enemy, state.CurrentHp);
        if (state.CurrentHp > 0)
            return state.CurrentHp;
        return RevivingEnemyHp(enemy, state.MaxHp);
    }

    public int AdvanceAeonglassAdditionalStrength(Creature owner)
    {
        int current = ReadAeonglassCounter(owner, _aeonglassAdditionalStrength, "AdditionalStrength");
        (_aeonglassAdditionalStrength ??= [])[owner] = current + 1;
        return current;
    }

    public int AdvanceAeonglassWitherUpgrade(Creature owner)
    {
        int next = ReadAeonglassCounter(owner, _aeonglassWitherUpgradeCount, "WitherUpgradeCount") + 1;
        (_aeonglassWitherUpgradeCount ??= [])[owner] = next;
        return next;
    }

    public int GetAeonglassWitherUpgradeCount(Creature owner)
        => ReadAeonglassCounter(owner, _aeonglassWitherUpgradeCount, "WitherUpgradeCount");

    public void NormalizeAeonglassWithers(CombatPredictionSimulator simulator)
    {
        int expectedUpgradeLevel = 0;
        foreach (Creature enemy in Enemies)
        {
            if (enemy.Monster?.GetType().Name == "Aeonglass")
            {
                expectedUpgradeLevel += ReadAeonglassCounter(
                    enemy,
                    _aeonglassWitherUpgradeCount,
                    "WitherUpgradeCount");
            }
        }
        if (expectedUpgradeLevel == 0)
            return;

        foreach (Player player in Players)
        {
            foreach (PredictedCard card in simulator.State.GetPlayerCombatState(player).AllCards)
            {
                if (card.MutablePreview is not Wither wither)
                    continue;
                while (wither._fakeUpgradeLevel < expectedUpgradeLevel)
                    wither.FakeUpgrade();
            }
        }
    }

    public void NormalizeCardAfflictions(CombatPredictionSimulator simulator)
    {
        NormalizeGhostSeedCards(simulator);
        foreach (Player player in Players)
        {
            int hex = GetAmount<HexPower>(player.Creature);
            int tangled = GetAmount<TangledPower>(player.Creature);
            int ringing = GetAmount<RingingPower>(player.Creature);
            foreach (PredictedCard card in simulator.State.GetPlayerCombatState(player).AllCards)
            {
                if (card.Preview.Affliction == null)
                {
                    if (hex > 0)
                        simulator.Afflict<Hexed>(card, hex);
                    else if (tangled > 0 && card.Preview.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack)
                        simulator.Afflict<Entangled>(card, 1);
                    else if (ringing > 0)
                        simulator.Afflict<Ringing>(card, 1);
                }
                else if ((hex <= 0 && card.Preview.Affliction is Hexed)
                         || (tangled <= 0 && card.Preview.Affliction is Entangled)
                         || (ringing <= 0 && card.Preview.Affliction is Ringing))
                {
                    card.MutablePreview.ClearAfflictionInternal();
                }
            }
        }
        NormalizePowerCardState(simulator);
        ApplyPhantomBladesRetain(simulator);
    }

    private void ApplyPhantomBladesRetain(CombatPredictionSimulator simulator)
    {
        foreach (Player player in Players)
        {
            if (GetAmount<PhantomBladesPower>(player.Creature) <= 0)
                continue;
            foreach (PredictedCard card in simulator.State.GetPlayerCombatState(player).AllCards)
            {
                if (card.Preview.Tags.Contains(CardTag.Shiv)
                    && !card.Preview.Keywords.Contains(CardKeyword.Retain))
                {
                    card.MutablePreview.AddKeyword(CardKeyword.Retain);
                }
            }
        }
    }

    public void RemoveHexPower(CombatPredictionSimulator simulator, Creature owner)
    {
        SetAmount<HexPower>(owner, 0);
        NormalizeCardAfflictions(simulator);
    }

    public bool CanPlayCard(CombatPredictionSimulator simulator, PredictedCard card)
    {
        if (IsCardPlayPrevented(simulator, card))
            return false;
        if (!simulator.CanPlay(card))
            return false;
        if (card.Preview.Affliction is Smog)
            return false;
        return card.Preview.Affliction is not Ringing
               || _ringingCardPlayed?.Contains(card.Preview.Owner) != true;
    }

    public IReadOnlyList<PowerModel> EffectivePowers()
    {
        if (_effectivePowers is not null)
            return _effectivePowers;
        IReadOnlyList<AbstractModel> listeners = GetEffectiveHookListeners();
        List<PowerModel> powers = new(listeners.Count);
        foreach (AbstractModel listener in listeners)
        {
            if (listener is PowerModel power)
                powers.Add(power);
        }
        _effectivePowers = powers;
        return _effectivePowers;
    }

    public IEnumerable<AbstractModel> IterateHookListeners()
        => GetEffectiveHookListeners();

    IReadOnlyList<AbstractModel> ICombatPredictionHookListenerSource.HookListeners
        => GetEffectiveHookListeners();

    IReadOnlyList<AbstractModel> ICombatPredictionHookListenerSource.RunHookListeners
        => GetEffectiveRunHookListeners();

    int ICombatPredictionPlayerLimits.GetMaxHandSize(Player player)
        => _rootMaxHandSizes.TryGetValue(player, out int maxHandSize)
            ? maxHandSize
            : throw new KeyNotFoundException($"No captured max hand size exists for player {player.NetId}.");

    int ICombatPredictionPlayerLimits.GetPotionSlotCount(Player player)
        => PotionSlotCount(player);

    bool ICombatPredictionPlayerCardRules.AreCardsFree(Player player)
        => _modHookSubscribers.EveryCardFreePlayers.Contains(player);

    void ICombatPredictionStateOwner.AttachPredictionState(CombatPredictionState predictionState)
    {
        if (_predictionState != null && !ReferenceEquals(_predictionState, predictionState))
            throw new InvalidOperationException("Combat prediction state is already attached.");
        _predictionState = predictionState;
        foreach (Player player in predictionState.Players)
        {
            predictionState.GetPlayerCombatState(player).OrbQueue
                .SetMutationObserver(InvalidateBaseHookListeners);
        }
    }

    void ICombatPredictionRootCaptureBoundary.AssertCanCaptureCreature(Creature creature)
    {
        if (_rootMaterialized
            && (_rootCreatures.Contains(creature)
                || (!ContainsCreature(creature) && !ReferenceEquals(creature.CombatState, this))))
            throw new InvalidOperationException($"Root creature state was not materialized for {creature.Name}.");
    }

    void ICombatPredictionRootCaptureBoundary.AssertCanCapturePlayer(Player player)
    {
        if (_rootMaterialized)
            throw new InvalidOperationException($"Root player state was not materialized for {player.NetId}.");
    }

    CombatPredictionRngSet ICombatPredictionRunSnapshot.CreatePredictionRngSet()
        => CombatPredictionRngSet.From(RunRngSet.FromSave(_runRngSnapshot));

    private IReadOnlyList<AbstractModel> GetEffectiveRunHookListeners()
    {
        if (_effectiveRunHookListeners != null)
            return _effectiveRunHookListeners;
        IReadOnlyList<AbstractModel> combatListeners = GetEffectiveHookListeners();
        if (_rootRunHookListeners.Length == 0)
        {
            _effectiveRunHookListeners = combatListeners;
            return _effectiveRunHookListeners;
        }
        List<AbstractModel> listeners = new(_rootRunHookListeners.Length + combatListeners.Count);
        listeners.AddRange(_rootRunHookListeners);
        listeners.AddRange(combatListeners);
        _effectiveRunHookListeners = listeners;
        return _effectiveRunHookListeners;
    }

    private IReadOnlyList<AbstractModel> GetEffectiveHookListeners()
    {
        if (_effectiveHookListeners is not null)
            return _effectiveHookListeners;

        AbstractModel[] baseListeners = GetBaseHookListeners();
        if (_powers is null && _addedPowerInstances is null)
        {
            _effectiveHookListeners = baseListeners;
            return _effectiveHookListeners;
        }

        List<AbstractModel> listeners = new(baseListeners.Length
            + (_powers?.Count ?? 0)
            + (_addedPowerInstances?.Count ?? 0));
        foreach (AbstractModel listener in baseListeners)
        {
            if (listener is PowerModel power
                && _powers?.ContainsKey((power.Owner, power.GetType())) == true)
                continue;
            listeners.Add(listener);
        }
        if (_powers != null)
        {
            foreach (PowerModel power in _powers.Values)
            {
                if (power.Amount != 0)
                    listeners.Add(power);
            }
        }
        if (_addedPowerInstances != null)
        {
            foreach (PowerModel power in _addedPowerInstances)
            {
                if (power.Amount != 0)
                    listeners.Add(power);
            }
        }
        _effectiveHookListeners = listeners;
        return _effectiveHookListeners;
    }

    private void InvalidateHookListeners()
    {
        _effectiveHookListeners = null;
        _effectiveRunHookListeners = null;
        _effectivePowers = null;
    }

    private AbstractModel[] GetBaseHookListeners()
    {
        if (_baseHookListeners != null)
            return _baseHookListeners;
        List<AbstractModel> listeners = _rootHookListeners
            .Where(listener => listener switch
            {
                MonsterModel monster => ContainsCreature(monster.Creature),
                PowerModel power => ContainsCreature(power.Owner) || _knownEnemies.Contains(power.Owner),
                _ => true,
            })
            .ToList();
        foreach (Creature creature in Creatures.Where(creature => !_rootCreatures.Contains(creature)))
        {
            listeners.AddRange(creature.Powers);
            if (creature.Monster != null)
                listeners.Add(creature.Monster);
        }
        CombatPredictionState predictionState = _predictionState
            ?? throw new InvalidOperationException("Combat prediction state is not attached.");
        foreach (Player player in Players)
            listeners.AddRange(predictionState.GetPlayerCombatState(player).OrbQueue.Orbs);
        if (_registeredCombatCards != null)
        {
            List<CardModel>? cardAttachedListenerOwners =
                _modHookSubscribers.HasBaseLibCardModifiers ? [] : null;
            foreach (PredictedCard card in _registeredCombatCards
                         .Where(card => !card.Preview.HasBeenRemovedFromState))
            {
                CardModel preview = card.Preview;
                listeners.Add(preview);
                if (preview.Affliction != null)
                    listeners.Add(preview.Affliction);
                if (preview.Enchantment != null)
                    listeners.Add(preview.Enchantment);
                cardAttachedListenerOwners?.Add(preview);
            }
            if (cardAttachedListenerOwners != null)
                _modHookSubscribers.AppendCardAttachedListeners(cardAttachedListenerOwners, listeners);
        }
        _baseHookListeners = listeners.ToArray();
        return _baseHookListeners;
    }

    internal void MaterializeRoot(CombatPredictionSimulator simulator)
    {
        if (_rootMaterialized)
            return;
        if (!NGame.IsMainThread())
            throw new InvalidOperationException("Combat prediction root can only be materialized on the main thread.");
        simulator.State.MaterializeRoot();
        _registeredCombatCards = simulator.State.Players
            .SelectMany(player => simulator.State.GetPlayerCombatState(player).AllCards)
            .ToList();
        foreach (PredictedCard card in _registeredCombatCards)
            card.SetMutationObserver(InvalidateBaseHookListeners);
        _ = GetBaseHookListeners();
        foreach (PowerModel power in _rootHookListeners.OfType<PowerModel>())
            _ = GetMutablePowerInstance(power);
        foreach (Player player in Players)
        {
            _ = GetPlayerTurnNumber(player);
            _ = GetPlayerGold(player);
            for (int slot = 0; slot < PotionSlotCount(player); slot++)
                _ = GetPotionAtSlot(player, slot);
            foreach (RelicModel relic in RelicsOf(player))
            {
                _ = GetStatefulRelicState(relic);
                RelicPredictionStateSupport.CaptureRootState(
                    simulator,
                    relic,
                    _rootRelicSources![relic]);
            }
        }
        foreach (Creature enemy in KnownEnemies)
        {
            if (enemy.Monster != null)
            {
                BranchMonsterAiState ai = GetMonsterAiState(enemy);
                string type = enemy.Monster.GetType().Name;
                if (type == "KnowledgeDemon")
                    (_knowledgeDemonCurseCounters ??= [])[enemy] = ai.KnowledgeDemonCurseCounter;
                if (type == "Aeonglass")
                {
                    (_aeonglassAdditionalStrength ??= [])[enemy] =
                        MonsterValueReader.ReadInt(enemy.Monster, "AdditionalStrength");
                    (_aeonglassWitherUpgradeCount ??= [])[enemy] =
                        MonsterValueReader.ReadInt(enemy.Monster, "WitherUpgradeCount");
                }
                if (enemy.Monster is TestSubject)
                {
                    _ = GetMonsterInt(enemy, "SecondFormHp");
                    _ = GetMonsterInt(enemy, "ThirdFormHp");
                }
            }
            _ = DescribePredictedMonsterState(enemy);
        }
        foreach (Creature creature in Creatures)
        {
            _ = HasLostHpThisTurn(creature);
            _ = WasDoomAppliedThisTurn(creature);
            _ = GetCardsDiscardedThisTurn(creature);
            _ = GetCreatureAttacksThisTurn(creature);
            _ = GetCardsExhaustedThisTurn(creature);
            _ = GetSkillCardsPlayedThisTurn(creature);
            _ = GetCardsPlayedThisTurn(creature);
            _ = GetAttacksPlayedThisTurn(creature);
            _ = GetShivsPlayedThisTurn(creature);
            _ = GetBlockCardsPlayedThisTurn(creature);
            foreach (Creature receiver in Creatures)
                _ = GetPoweredAttackHitsThisTurn(creature, receiver);
        }
        foreach (Player player in Players)
        {
            _ = GetEnergySpentThisTurn(player);
            _ = GetStarsGainedThisTurn(player);
            _ = GetNonHandDrawsThisTurn(player);
            _ = GetPreviousTurnAttack(simulator, player);
        }
        _ = GetFetchCardsPlayedThisTurn();
        _enemiesIntendingAttack = [.. Enemies.Where(enemy => enemy.Monster?.IntendsToAttack == true)];
        _hasPredictedEnemyIntents = true;
        StateFingerprintBuilder fingerprint = new();
        AppendFingerprint(ref fingerprint, simulator);
        _rootRelicSources = null;
        _rootMaterialized = true;
    }

    void ICombatPredictionRootMaterializable.MaterializeRoot(CombatPredictionSimulator simulator)
        => MaterializeRoot(simulator);

    internal int RootHookListenerCount => _baseHookListeners?.Length ?? _rootHookListeners.Length;
    internal int RootRunHookListenerCount => _rootRunHookListeners.Length;
    internal int RootRunModSubscriberCount => _modHookSubscribers.RunSubscribers.Length;
    internal int RootCombatModSubscriberCount => _modHookSubscribers.CombatSubscribers.Length;
    internal bool RootHasBaseLibCardModifiers => _modHookSubscribers.HasBaseLibCardModifiers;
    internal bool RootMultiplayerScalingIsDetached => _multiplayerScalingModel is null
        || (MultiplayerScalingRunStateField.GetValue(_multiplayerScalingModel) is null
            && MultiplayerScalingCombatStateField.GetValue(_multiplayerScalingModel) is null);

    internal IReadOnlyList<RelicModel> RelicsOf(Player player)
        => _rootRelics.TryGetValue(player, out RelicModel[]? relics)
            ? relics
            : throw new InvalidOperationException($"Player {player.NetId} is outside the captured relic inventory.");

    private int PotionSlotCount(Player player)
        => _rootPotionSlotCounts.TryGetValue(player, out int count)
            ? count
            : throw new InvalidOperationException($"Player {player.NetId} is outside the captured potion inventory.");

    public void RegisterGeneratedCombatCard(PredictedCard card)
    {
        if (_registeredCombatCards?.Contains(card) != true)
            (_registeredCombatCards ??= []).Add(card);
        card.SetMutationObserver(InvalidateBaseHookListeners);
        if (_rootHookListeners.Any(card.References)
            || _generatedCombatCards?.Contains(card) == true)
        {
            return;
        }
        (_generatedCombatCards ??= []).Add(card);
        card.SetMutationObserver(InvalidateBaseHookListeners);
        InvalidateBaseHookListeners();
    }

    public void UnregisterGeneratedCombatCard(PredictedCard card)
    {
        _registeredCombatCards?.Remove(card);
        card.SetMutationObserver(null);
        if (_generatedCombatCards?.Remove(card) != true)
            return;
        InvalidateBaseHookListeners();
    }

    private void InvalidateBaseHookListeners()
    {
        _baseHookListeners = null;
        InvalidateHookListeners();
    }

    public void AppendFingerprint(
        ref StateFingerprintBuilder fingerprint,
        CombatPredictionSimulator simulator)
    {
        ulong powersFirst = 0;
        ulong powersSecond = 0;
        int powerCount = 0;
        IReadOnlyList<PowerModel> effectivePowers = EffectivePowers();
        foreach (PowerModel power in effectivePowers)
        {
            if (power.Amount == 0)
                continue;
            AddPower(power, ref powersFirst, ref powersSecond);
            powerCount++;
        }
        AddUnordered(ref fingerprint, 'P', powerCount, powersFirst, powersSecond);

        AddPlayerIntMap(ref fingerprint, 'D', _drawNextTurn);
        AddCreatureIntMap(ref fingerprint, 'd', _temporaryDexterity);
        AddCreatureTypeSet(ref fingerprint, 'K', _skipNextDurationTick);
        AddCreatureSet(ref fingerprint, 'S', _skipNextMove);
        AddCreatureIntMap(ref fingerprint, 'G', _pressureGunBonus);
        AddCreatureIntMap(ref fingerprint, 'R', _steamEruptionDamage);
        AddSteamEruptionPhases(ref fingerprint, _steamEruptionPhases);
        AddAeonglassCounters(ref fingerprint, 'A', _aeonglassAdditionalStrength, "AdditionalStrength");
        AddAeonglassCounters(ref fingerprint, 'W', _aeonglassWitherUpgradeCount, "WitherUpgradeCount");
        AddPlayerSet(ref fingerprint, 'Q', _ringingCardPlayed);
        AddCreatureIntMap(ref fingerprint, 'a', _attacksPlayedThisTurn);
        AddCreatureIntMap(ref fingerprint, 'j', _shivsPlayedThisTurn);
        AddCreatureIntMap(ref fingerprint, 'b', _blockCardsPlayedThisTurn);
        AddCreatureIntMap(ref fingerprint, 'l', _skillCardsPlayedThisTurn);
        AddCreatureIntMap(ref fingerprint, 'x', _cardsExhaustedThisTurn);
        AddCreatureSet(ref fingerprint, 'd', _doomAppliersThisTurn);
        AddCreatureSet(ref fingerprint, 'L', _unblockedDamageThisTurn);
        AddPoweredAttackHits(ref fingerprint, _poweredAttackHitsThisTurn);
        AddCreatureIntMap(ref fingerprint, 'v', _cardsDiscardedThisTurn);
        AddCreatureIntMap(ref fingerprint, 'u', _creatureAttacksThisTurn);
        AddPlayerIntMap(ref fingerprint, 'e', _energySpentThisTurn);
        AddPlayerIntMap(ref fingerprint, 'z', _starsGainedThisTurn);
        AddPlayerIntMap(ref fingerprint, 'n', _nonHandDrawsThisTurn);
        AddCreatureIntMap(ref fingerprint, 'k', _knowledgeDemonCurseCounters);
        AddCreatureSet(ref fingerprint, 'i', _enemiesIntendingAttack);
        fingerprint.Add(_hasPredictedEnemyIntents);
        fingerprint.Add(HasPendingChoice);
        AddFeralStates(ref fingerprint, simulator, effectivePowers);
        AddJugglingStates(ref fingerprint, simulator, effectivePowers);
        AddTurnStartStates(ref fingerprint, simulator, effectivePowers);
        AppendPowerLifecycleFingerprint(ref fingerprint);
        AddNemesisStates(ref fingerprint, effectivePowers);
        AddTenderStates(ref fingerprint, effectivePowers);
        AppendCardLifecycleFingerprint(ref fingerprint, simulator);
        AppendStatefulRelicFingerprint(ref fingerprint, simulator);
        AppendRelicResourceFingerprint(ref fingerprint);
        AppendPotionFingerprint(ref fingerprint);
        AppendMonsterAiFingerprint(ref fingerprint);
        AppendMonsterStateFingerprint(ref fingerprint);
        AppendDampenFingerprint(ref fingerprint);
        AppendDeathLifecycleFingerprint(ref fingerprint);
        AppendPossessFingerprint(ref fingerprint);
        AppendAutoPlayFingerprint(ref fingerprint);
        fingerprint.Add('T');
        fingerprint.Add(OutstandingStolenResource(simulator));
    }

    private static void AddPower(PowerModel power, ref ulong first, ref ulong second)
    {
        StateFingerprintBuilder item = new();
        item.Add(power.Owner.CombatId ?? uint.MaxValue);
        item.Add(power.Applier?.CombatId ?? uint.MaxValue);
        item.Add(power.Target?.CombatId ?? uint.MaxValue);
        item.Add(power.Id.Entry);
        item.Add(power.Amount);
        item.Add(PowerLifecycleSupport.SemanticallyRelevantAmountOnTurnStart(power));
        if (power is RitualPower ritual)
            item.Add(ritual._wasJustAppliedByEnemy);
        ulong dynamicFirst = 0;
        ulong dynamicSecond = 0;
        int dynamicCount = 0;
        foreach (var dynamicVar in power.DynamicVars)
        {
            if (!SemanticStateFieldPolicy.IsSemantic(power, dynamicVar.Key, dynamicVar.Value))
                continue;
            StateFingerprintBuilder dynamicItem = new();
            dynamicItem.Add(dynamicVar.Key);
            dynamicItem.Add(dynamicVar.Value.BaseValue);
            if (dynamicVar.Value is StringVar stringVar)
                dynamicItem.Add(stringVar.StringValue);
            StateFingerprint dynamicFingerprint = dynamicItem.Finish();
            dynamicFirst += StateFingerprintBuilder.MixFirst(dynamicFingerprint.First);
            dynamicSecond += StateFingerprintBuilder.MixSecond(dynamicFingerprint.Second);
            dynamicCount++;
        }
        item.Add(dynamicCount);
        item.Add(dynamicFirst);
        item.Add(dynamicSecond);
        StateFingerprint value = item.Finish();
        first += StateFingerprintBuilder.MixFirst(value.First);
        second += StateFingerprintBuilder.MixSecond(value.Second);
    }

    private void AddFeralStates(
        ref StateFingerprintBuilder fingerprint,
        CombatPredictionSimulator simulator,
        IReadOnlyList<PowerModel> effectivePowers)
    {
        ulong first = 0;
        ulong second = 0;
        int count = 0;
        foreach (FeralPower power in effectivePowers.OfType<FeralPower>())
        {
            if (power.Amount <= 0)
                continue;
            StateFingerprintBuilder item = new();
            item.Add(power.Owner.CombatId ?? uint.MaxValue);
            item.Add(simulator.StateStore
                .Peek(power, () => new FeralPredictionState(power))
                .ZeroCostAttacksPlayed);
            AddUnorderedItem(item.Finish(), ref first, ref second);
            count++;
        }
        AddUnordered(ref fingerprint, 'F', count, first, second);
    }

    private void AddJugglingStates(
        ref StateFingerprintBuilder fingerprint,
        CombatPredictionSimulator simulator,
        IReadOnlyList<PowerModel> effectivePowers)
    {
        ulong first = 0;
        ulong second = 0;
        int count = 0;
        foreach (JugglingPower power in effectivePowers.OfType<JugglingPower>())
        {
            if (power.Amount <= 0)
                continue;
            StateFingerprintBuilder item = new();
            item.Add(power.Owner.CombatId ?? uint.MaxValue);
            item.Add(simulator.StateStore
                .Peek(power, () => new JugglingPredictionState(power))
                .AttacksPlayedThisTurn);
            AddUnorderedItem(item.Finish(), ref first, ref second);
            count++;
        }
        AddUnordered(ref fingerprint, 'J', count, first, second);
    }

    private void AddTurnStartStates(
        ref StateFingerprintBuilder fingerprint,
        CombatPredictionSimulator simulator,
        IReadOnlyList<PowerModel> effectivePowers)
    {
        ulong first = 0;
        ulong second = 0;
        int count = 0;
        foreach (PowerModel power in effectivePowers)
        {
            if (power.Amount <= 0)
                continue;
            int? value = power switch
            {
                HardenedShellPower shell => (int)simulator.StateStore
                    .Peek(shell, () => new HardenedShellPredictionState(shell))
                    .DamageReceivedThisTurn,
                AutomationPower automation => simulator.StateStore
                    .Peek(automation, () => new AutomationPredictionState(automation))
                    .CardsLeft,
                SlothPower sloth => simulator.StateStore
                    .Peek(sloth, () => new CounterPredictionState(
                        GetCardsPlayedThisTurn(sloth.Owner)))
                    .Value,
                VoidFormPower voidForm => simulator.StateStore
                    .Peek(voidForm, () => new VoidFormPredictionState(voidForm))
                    .CardsPlayedThisTurn,
                ChainsOfBindingPower chains => EncodeChainsOfBindingState(simulator, chains),
                _ => null,
            };
            if (value is not { } counter)
                continue;
            StateFingerprintBuilder item = new();
            item.Add(power.Owner.CombatId ?? uint.MaxValue);
            item.Add(power.Id.Entry);
            item.Add(counter);
            AddUnorderedItem(item.Finish(), ref first, ref second);
            count++;
        }
        AddUnordered(ref fingerprint, 'T', count, first, second);
    }

    private static int EncodeChainsOfBindingState(
        CombatPredictionSimulator simulator,
        ChainsOfBindingPower power)
    {
        ChainsOfBindingPredictionState state = simulator.StateStore.Peek(
            power,
            () => new ChainsOfBindingPredictionState(power));
        return checked(state.BoundCardsAfflictedThisTurn * 2 + (state.BoundCardPlayed ? 1 : 0));
    }

    private void AddNemesisStates(
        ref StateFingerprintBuilder fingerprint,
        IReadOnlyList<PowerModel> effectivePowers)
    {
        ulong first = 0;
        ulong second = 0;
        int count = 0;
        foreach (NemesisPower power in effectivePowers.OfType<NemesisPower>())
        {
            if (power.Amount <= 0)
                continue;
            StateFingerprintBuilder item = new();
            item.Add(power.Owner.CombatId ?? uint.MaxValue);
            item.Add(GetNemesisShouldApplyIntangible(power.Owner));
            AddUnorderedItem(item.Finish(), ref first, ref second);
            count++;
        }
        AddUnordered(ref fingerprint, 'N', count, first, second);
    }

    private void AddTenderStates(
        ref StateFingerprintBuilder fingerprint,
        IReadOnlyList<PowerModel> effectivePowers)
    {
        ulong first = 0;
        ulong second = 0;
        int count = 0;
        foreach (TenderPower power in effectivePowers.OfType<TenderPower>())
        {
            if (power.Amount <= 0)
                continue;
            StateFingerprintBuilder item = new();
            item.Add(power.Owner.CombatId ?? uint.MaxValue);
            item.Add(GetTenderCardsPlayed(power.Owner));
            AddUnorderedItem(item.Finish(), ref first, ref second);
            count++;
        }
        AddUnordered(ref fingerprint, 'Y', count, first, second);
    }

    private static void AddPlayerIntMap(
        ref StateFingerprintBuilder fingerprint,
        char marker,
        IReadOnlyDictionary<Player, int>? values)
    {
        ulong first = 0;
        ulong second = 0;
        int count = 0;
        if (values != null)
        {
            foreach ((Player player, int value) in values)
            {
                StateFingerprintBuilder item = new();
                item.Add(player.NetId);
                item.Add(value);
                AddUnorderedItem(item.Finish(), ref first, ref second);
                count++;
            }
        }
        AddUnordered(ref fingerprint, marker, count, first, second);
    }

    private static void AddCreatureIntMap(
        ref StateFingerprintBuilder fingerprint,
        char marker,
        IReadOnlyDictionary<Creature, int>? values)
    {
        ulong first = 0;
        ulong second = 0;
        int count = 0;
        if (values != null)
        {
            foreach ((Creature creature, int value) in values)
            {
                StateFingerprintBuilder item = new();
                item.Add(creature.CombatId ?? uint.MaxValue);
                item.Add(value);
                AddUnorderedItem(item.Finish(), ref first, ref second);
                count++;
            }
        }
        AddUnordered(ref fingerprint, marker, count, first, second);
    }

    private static void AddCreatureTypeSet(
        ref StateFingerprintBuilder fingerprint,
        char marker,
        IReadOnlySet<(Creature Owner, Type Type)>? values)
    {
        ulong first = 0;
        ulong second = 0;
        int count = 0;
        if (values != null)
        {
            foreach ((Creature owner, Type type) in values)
            {
                StateFingerprintBuilder item = new();
                item.Add(owner.CombatId ?? uint.MaxValue);
                item.Add(type.FullName);
                AddUnorderedItem(item.Finish(), ref first, ref second);
                count++;
            }
        }
        AddUnordered(ref fingerprint, marker, count, first, second);
    }

    private static void AddCreatureSet(
        ref StateFingerprintBuilder fingerprint,
        char marker,
        IReadOnlySet<Creature>? values)
    {
        ulong first = 0;
        ulong second = 0;
        int count = 0;
        if (values != null)
        {
            foreach (Creature creature in values)
            {
                StateFingerprintBuilder item = new();
                item.Add(creature.CombatId ?? uint.MaxValue);
                AddUnorderedItem(item.Finish(), ref first, ref second);
                count++;
            }
        }
        AddUnordered(ref fingerprint, marker, count, first, second);
    }

    private static void AddPlayerSet(
        ref StateFingerprintBuilder fingerprint,
        char marker,
        IReadOnlySet<Player>? values)
    {
        ulong first = 0;
        ulong second = 0;
        int count = 0;
        if (values != null)
        {
            foreach (Player player in values)
            {
                StateFingerprintBuilder item = new();
                item.Add(player.NetId);
                AddUnorderedItem(item.Finish(), ref first, ref second);
                count++;
            }
        }
        AddUnordered(ref fingerprint, marker, count, first, second);
    }

    private static void AddSteamEruptionPhases(
        ref StateFingerprintBuilder fingerprint,
        IReadOnlyDictionary<Creature, SteamEruptionPhase>? values)
    {
        ulong first = 0;
        ulong second = 0;
        int count = 0;
        if (values != null)
        {
            foreach ((Creature creature, SteamEruptionPhase phase) in values)
            {
                StateFingerprintBuilder item = new();
                item.Add(creature.CombatId ?? uint.MaxValue);
                item.Add((int)phase);
                AddUnorderedItem(item.Finish(), ref first, ref second);
                count++;
            }
        }
        AddUnordered(ref fingerprint, 'U', count, first, second);
    }

    private void AddAeonglassCounters(
        ref StateFingerprintBuilder fingerprint,
        char marker,
        IReadOnlyDictionary<Creature, int>? simulatedValues,
        string memberName)
    {
        ulong first = 0;
        ulong second = 0;
        int count = 0;
        foreach (Creature enemy in Enemies)
        {
            if (enemy.Monster?.GetType().Name != "Aeonglass")
                continue;
            StateFingerprintBuilder item = new();
            item.Add(enemy.CombatId ?? uint.MaxValue);
            item.Add(ReadAeonglassCounter(enemy, simulatedValues, memberName));
            AddUnorderedItem(item.Finish(), ref first, ref second);
            count++;
        }
        AddUnordered(ref fingerprint, marker, count, first, second);
    }

    private static int ReadAeonglassCounter(
        Creature owner,
        IReadOnlyDictionary<Creature, int>? simulatedValues,
        string memberName)
    {
        if (simulatedValues != null && simulatedValues.TryGetValue(owner, out int value))
            return value;
        MonsterModel monster = owner.Monster
            ?? throw new InvalidOperationException("永世沙漏计数器缺少 MonsterModel。");
        return MonsterValueReader.ReadInt(monster, memberName);
    }

    private static void AddUnorderedItem(StateFingerprint item, ref ulong first, ref ulong second)
    {
        first += StateFingerprintBuilder.MixFirst(item.First);
        second += StateFingerprintBuilder.MixSecond(item.Second);
    }

    private static void AddUnordered(
        ref StateFingerprintBuilder fingerprint,
        char marker,
        int count,
        ulong first,
        ulong second)
    {
        fingerprint.Add(marker);
        fingerprint.Add(count);
        fingerprint.Add(first);
        fingerprint.Add(second);
    }

    private bool ConsumeArtifact(Creature target)
    {
        int amount = GetAmount<ArtifactPower>(target);
        if (amount <= 0)
            return false;
        SetAmount<ArtifactPower>(target, amount - 1);
        return true;
    }

    private static int Consume(ForkableDictionary<Player, int>? values, Player player)
    {
        if (values == null)
            return 0;
        int value = values.GetValueOrDefault(player);
        values.Remove(player);
        return value;
    }

    public T CreateCard<T>(Player owner) where T : CardModel
        => (T)PredictionUtils.CreateCard(ModelDb.Card<T>(), owner);
    public CardModel CreateCard(CardModel canonicalCard, Player owner)
        => PredictionUtils.CreateCard(canonicalCard, owner);
    public CardModel CloneCard(CardModel mutableCard)
        => PredictionUtils.CloneCardStateForSimulation(mutableCard);
    public void AddCard(CardModel card, Player owner) => throw new NotSupportedException();
    public void RemoveCard(CardModel card) => throw new NotSupportedException();
    public bool ContainsCard(CardModel card)
        => _rootFloatingCards.Contains(card)
            || _registeredCombatCards?.Any(predicted => predicted.References(card)) == true;
    public void AddPlayer(Player player) => throw new NotSupportedException();
    public Creature CreateCreature(MonsterModel monster, CombatSide side, string? slot)
    {
        monster.AssertMutable();
        monster.RunRng = RunRngSet.FromSave(_runRngSnapshot);
        Creature creature = new(monster, side, slot)
        {
            CombatState = this,
            CombatId = _nextCreatureId++,
        };
        return creature;
    }
    public Creature CreatePredictedMonster(
        CombatPredictionSimulator simulator,
        MonsterModel monster,
        CombatSide side,
        string? slot)
    {
        Creature creature = CreateCreature(monster, side, slot);
        if (side == CombatSide.Enemy)
        {
            creature.SetUniqueMonsterHpValue(_enemies, simulator.Rng.Niche);
            creature.ScaleMonsterHpForMultiplayer(Encounter, Players.Count, _currentActIndex);
        }
        _ = simulator.State.GetCreature(creature);
        return creature;
    }
    public void AddPredictedMonster(Creature creature)
    {
        MonsterModel monster = creature.Monster
            ?? throw new InvalidOperationException("Only monsters can be added through the predicted monster path.");
        monster.SetUpForCombat();
        AddCreature(creature);
        if (creature.SlotName != null)
            SortEnemiesBySlotName();
    }
    public void PreparePredictedMonster(
        CombatPredictionSimulator simulator,
        Creature creature)
    {
        MonsterModel monster = creature.Monster
            ?? throw new InvalidOperationException("Only monsters can be prepared through the predicted monster path.");
        if (CurrentSide == CombatSide.Player)
        {
            MoveState initial = monster.MoveStateMachine!.RollMove(
                PlayerCreatures,
                creature,
                simulator.Rng.MonsterAi);
            RegisterMonsterAi(creature, initial);
        }
        else
        {
            RegisterPendingInitialMonsterAi(creature);
        }
    }
    public void CreatureEscaped(Creature creature)
    {
        if (!ContainsCreature(creature))
            throw new InvalidOperationException("逃跑的生物不在当前模拟战斗中。");
        foreach (PowerModel power in EffectivePowers()
                     .Where(power => power.Owner == creature && power.Amount != 0)
                     .ToArray())
        {
            SetPowerAmount(power, 0);
        }
        _escapedCreatures.Add(creature);
        _knownEnemies.Remove(creature);
        RemoveCreature(creature);
    }
    public void RemoveCreature(Creature creature, bool unattach = true)
    {
        bool removed = _allies.Remove(creature) || _enemies.Remove(creature);
        if (!removed)
            return;
        _creatures = null;
        InvalidateBaseHookListeners();
    }
    void ICombatPredictionRosterSink.RemoveCreatureFromPrediction(Creature creature)
        => RemoveCreature(creature, unattach: false);
    public bool ContainsCreature(Creature creature) => _allies.Contains(creature) || _enemies.Contains(creature);
    public bool ContainsMonster<T>() where T : MonsterModel => _enemies.Any(static creature => creature.Monster is T);
    bool ICombatPredictionCreatureSemantics.IsPrimaryEnemy(Creature creature)
        => creature.Side == CombatSide.Enemy
            && !EffectivePowers().Any(power =>
                power.Owner == creature && power.Amount > 0 && power.OwnerIsSecondaryEnemy);
    bool ICombatPredictionCreatureSemantics.IsHittable(Creature creature)
    {
        if (_deathPhases?.GetValueOrDefault(creature) is PredictedDeathPhase.Reviving
            or PredictedDeathPhase.PermanentlyDead)
        {
            return false;
        }

        foreach (AbstractModel listener in GetEffectiveHookListeners())
        {
            if (listener is ReattachPower or IllusionPower or AdaptablePower or DieForYouPower)
                continue;
            if (!listener.ShouldAllowHitting(creature))
                return false;
        }
        return true;
    }
    bool ICombatPredictionCreatureSemantics.ShouldRemoveAfterDeath(Creature creature)
        => GetAmount<AdaptablePower>(creature) <= 0
            && GetAmount<IllusionPower>(creature) <= 0
            && GetAmount<ReattachPower>(creature) <= 0
            && GetAmount<SteamEruptionPower>(creature) <= 0;
    public Creature? GetCreature(uint? combatId)
    {
        if (combatId == null)
            return null;
        return Creatures.FirstOrDefault(creature => creature.CombatId == combatId)
            ?? _knownEnemies.FirstOrDefault(creature => creature.CombatId == combatId);
    }
    public Task<Creature?> GetCreatureAsync(uint? combatId, double timeoutSec)
        => Task.FromResult(GetCreature(combatId));
    public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side)
        => side == CombatSide.Player ? _allies : _enemies;
    public IReadOnlyList<Creature> GetOpponentsOf(Creature creature)
        => GetCreaturesOnSide(creature.Side.GetOppositeSide());
    public IReadOnlyList<Creature> GetTeammatesOf(Creature creature) => GetCreaturesOnSide(creature.Side);
    public Player? GetPlayer(ulong playerId) => Players.FirstOrDefault(player => player.NetId == playerId);
    public void SortEnemiesBySlotName()
    {
        Creature[] ordered = _enemies
            .OrderBy(creature => GetSlotIndex(_encounterSlots, creature.SlotName))
            .ToArray();
        for (int index = 0; index < ordered.Length; index++)
            _enemies[index] = ordered[index];
    }
    private static int GetSlotIndex(IReadOnlyList<string> slots, string? slot)
    {
        for (int index = 0; index < slots.Count; index++)
        {
            if (string.Equals(slots[index], slot, StringComparison.Ordinal))
                return index;
        }
        return -1;
    }
    public void SetEnemyIndex(Creature creature, int index)
    {
        int current = _enemies.IndexOf(creature);
        if (current < 0)
            throw new InvalidOperationException("待移动的敌人不在当前模拟战斗中。");
        _enemies.RemoveAt(current);
        _enemies.Insert(Math.Clamp(index, 0, _enemies.Count), creature);
    }
    public void AddCreature(Creature creature)
    {
        if (!ReferenceEquals(creature.CombatState, this))
            throw new InvalidOperationException("生物属于另一场战斗。");
        if (ContainsCreature(creature))
            throw new InvalidOperationException("生物已经存在于当前模拟战斗中。");
        (creature.Side == CombatSide.Player ? _allies : _enemies).Add(creature);
        if (creature.Side == CombatSide.Enemy && !_knownEnemies.Contains(creature))
            _knownEnemies.Add(creature);
        _creatures = null;
        InvalidateBaseHookListeners();
    }
    public bool IsLiveCombat() => false;
}
