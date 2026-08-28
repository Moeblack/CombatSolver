using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using CombatSolver.Engine.Common;

namespace CombatSolver;

internal sealed class PredictionModHookSubscriberCapture
{
    private const string BaseLibMaxHandSizeInterfaceName = "BaseLib.Hooks.IMaxHandSizeModifier";
    private const string LoadoutMaxHandSizeModifierTypeName =
        "Loadout.Services.TildeKey.LoadoutMaxHandSizeModifier";
    private static readonly HashSet<string> KnownPreRootSubscriberTypeNames =
    [
        LoadoutMaxHandSizeModifierTypeName,
        "Loadout.Services.TildeKey.LoadoutKillAllMonstersCombatHook",
        "Loadout.Services.PowerGiver.PowerGiverCombatStartHook",
    ];

    public AbstractModel[] RunSubscribers { get; }
    public AbstractModel[] CombatSubscribers { get; }
    public IReadOnlyDictionary<Player, int> MaxHandSizes { get; }
    public bool HasBaseLibCardModifiers { get; }

    private PredictionModHookSubscriberCapture(
        AbstractModel[] runSubscribers,
        AbstractModel[] combatSubscribers,
        IReadOnlyDictionary<Player, int> maxHandSizes,
        bool hasBaseLibCardModifiers)
    {
        RunSubscribers = runSubscribers;
        CombatSubscribers = combatSubscribers;
        MaxHandSizes = maxHandSizes;
        HasBaseLibCardModifiers = hasBaseLibCardModifiers;
    }

    public static PredictionModHookSubscriberCapture Capture(
        RunState runState,
        CombatState combat,
        IReadOnlyList<AbstractModel> runHookListeners)
    {
        AbstractModel[] runSubscribers = ModHelper.IterateAllRunStateSubscribers(runState).ToArray();
        AbstractModel[] combatSubscribers = ModHelper.IterateAllCombatStateSubscribers(combat).ToArray();
        bool hasBaseLibCardModifiers = combatSubscribers.Any(PredictionModModelSupport.IsBaseLibCardModifier);
        PredictionModModelSupport.RegisterBaseLibCardModifierSources(combatSubscribers);

        foreach (AbstractModel subscriber in runSubscribers)
            ValidateSubscriber(subscriber, "run");
        foreach (AbstractModel subscriber in combatSubscribers)
            ValidateSubscriber(subscriber, "combat");

        Dictionary<Player, int> maxHandSizes = [];
        foreach (Player player in combat.Players)
        {
            int maxHandSize = CardPile.MaxCardsInHand;
            foreach (AbstractModel listener in runHookListeners)
            {
                Type listenerType = listener.GetType();
                if (!listenerType.GetInterfaces().Any(@interface =>
                        @interface.FullName == BaseLibMaxHandSizeInterfaceName))
                {
                    continue;
                }
                if (listenerType.FullName != LoadoutMaxHandSizeModifierTypeName)
                {
                    throw new PredictionUnsupportedException(
                        $"Unsupported max-hand-size listener {listenerType.FullName}.");
                }
                MethodInfo modify = listenerType.GetMethod(
                    "ModifyMaxHandSizeLate",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: [typeof(Player), typeof(int)],
                    modifiers: null)
                    ?? throw new MissingMethodException(listenerType.FullName, "ModifyMaxHandSizeLate(Player, int)");
                maxHandSize = (int)(modify.Invoke(listener, [player, maxHandSize])
                    ?? throw new InvalidOperationException(
                        $"Max-hand-size listener {listenerType.FullName} returned null."));
                if (maxHandSize < 0)
                {
                    throw new InvalidOperationException(
                        $"Max-hand-size listener {listenerType.FullName} returned {maxHandSize}.");
                }
            }
            maxHandSizes.Add(player, maxHandSize);
        }

        return new PredictionModHookSubscriberCapture(
            runSubscribers,
            combatSubscribers,
            maxHandSizes,
            hasBaseLibCardModifiers);
    }

    public void AppendCardAttachedListeners(
        IEnumerable<CardModel> cards,
        List<AbstractModel> listeners)
    {
        foreach (CardModel card in cards)
            PredictionModModelSupport.AppendCardAttachedListeners(card, listeners);
    }

    private static void ValidateSubscriber(
        AbstractModel subscriber,
        string scope)
    {
        Type type = subscriber.GetType();
        if (PredictionModModelSupport.IsBaseLibCardModifier(subscriber)
            || KnownPreRootSubscriberTypeNames.Contains(type.FullName ?? string.Empty)
            || IsNonGameplayMod(type))
        {
            return;
        }
        throw new PredictionUnsupportedException(
            $"Unsupported gameplay ModHelper {scope} subscriber {type.FullName}.");
    }

    private static bool IsNonGameplayMod(Type type)
    {
        var mod = AssemblyInfo.ModForType(type, out bool isBaseGame);
        return !isBaseGame && mod?.manifest?.affectsGameplay is false;
    }
}
