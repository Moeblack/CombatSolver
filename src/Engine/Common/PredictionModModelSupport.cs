using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver.Engine.Common;

internal static class PredictionModModelSupport
{
    private const string BaseLibCardModifierTypeName = "BaseLib.Abstracts.CardModifier";
    private static readonly Lazy<BaseLibCardModifierAdapter?> BaseLibCardModifiers =
        new(CreateBaseLibCardModifierAdapter);
    private static readonly ConditionalWeakTable<CardModel, object> BaseLibModifierCards = new();
    private static readonly object BaseLibModifierCardMarker = new();
    private static volatile bool _hasRegisteredBaseLibModifierCards;

    public static bool IsBaseLibCardModifier(AbstractModel model)
        => BaseLibCardModifiers.Value?.ModifierType.IsInstanceOfType(model) == true;

    public static void RegisterBaseLibCardModifierSources(IEnumerable<AbstractModel> subscribers)
    {
        BaseLibCardModifierAdapter? adapter = BaseLibCardModifiers.Value;
        if (adapter == null)
            return;
        foreach (AbstractModel subscriber in subscribers)
        {
            if (!adapter.ModifierType.IsInstanceOfType(subscriber))
                continue;
            CardModel owner = adapter.GetOwner(subscriber)
                ?? throw new InvalidOperationException("BaseLib card modifier has no owner during root capture.");
            _ = BaseLibModifierCards.GetValue(owner, _ => BaseLibModifierCardMarker);
            _hasRegisteredBaseLibModifierCards = true;
        }
    }

    public static void CloneCardAttachedModels(CardModel source, CardModel clone)
    {
        BaseLibCardModifierAdapter? adapter = BaseLibCardModifiers.Value;
        if (!_hasRegisteredBaseLibModifierCards
            || adapter == null
            || !BaseLibModifierCards.TryGetValue(source, out _))
            return;
        IList sourceModifiers = adapter.DirectModifiers(source);
        IList clonedModifiers = adapter.DirectModifiers(clone);
        if (clonedModifiers.Count != 0)
            throw new InvalidOperationException("BaseLib populated card modifiers before prediction clone migration.");
        foreach (object sourceModifier in sourceModifiers)
        {
            if (sourceModifier is not AbstractModel sourceModel
                || !adapter.ModifierType.IsInstanceOfType(sourceModel))
            {
                throw new InvalidOperationException("BaseLib returned an invalid source card modifier.");
            }
            AbstractModel clonedModel = PredictionUtils.CloneModelForSimulation(sourceModel);
            adapter.SetOwner(clonedModel, clone);
            clonedModifiers.Add(clonedModel);
            adapter.AfterClonedOnCard(clonedModel, clone);
        }
        _ = BaseLibModifierCards.GetValue(clone, _ => BaseLibModifierCardMarker);
    }

    public static void AppendCardAttachedListeners(CardModel card, List<AbstractModel> listeners)
    {
        BaseLibCardModifierAdapter? adapter = BaseLibCardModifiers.Value;
        if (!_hasRegisteredBaseLibModifierCards
            || adapter == null
            || !BaseLibModifierCards.TryGetValue(card, out _))
            return;
        foreach (object modifier in adapter.DirectModifiers(card))
        {
            if (modifier is not AbstractModel model
                || !adapter.ModifierType.IsInstanceOfType(model))
            {
                throw new InvalidOperationException("BaseLib returned an invalid card modifier listener.");
            }
            listeners.Add(model);
        }
    }

    private static BaseLibCardModifierAdapter? CreateBaseLibCardModifierAdapter()
    {
        Type? modifierType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(BaseLibCardModifierTypeName, throwOnError: false))
            .FirstOrDefault(type => type != null);
        if (modifierType == null)
            return null;
        MethodInfo directModifiers = modifierType.GetMethod(
            "DirectModifiers",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(CardModel)],
            modifiers: null)
            ?? throw new MissingMethodException(BaseLibCardModifierTypeName, "DirectModifiers(CardModel)");
        MethodInfo setOwner = modifierType.GetProperty("Owner", BindingFlags.Instance | BindingFlags.Public)?
            .GetSetMethod(nonPublic: true)
            ?? throw new MissingMethodException(BaseLibCardModifierTypeName, "set_Owner(CardModel)");
        MethodInfo getOwner = modifierType.GetProperty("Owner", BindingFlags.Instance | BindingFlags.Public)?
            .GetGetMethod(nonPublic: true)
            ?? throw new MissingMethodException(BaseLibCardModifierTypeName, "get_Owner()");
        MethodInfo afterClonedOnCard = modifierType.GetMethod(
            "AfterClonedOnCard",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: [typeof(CardModel)],
            modifiers: null)
            ?? throw new MissingMethodException(BaseLibCardModifierTypeName, "AfterClonedOnCard(CardModel)");
        return new BaseLibCardModifierAdapter(
            modifierType,
            directModifiers,
            getOwner,
            setOwner,
            afterClonedOnCard);
    }

    private sealed class BaseLibCardModifierAdapter(
        Type modifierType,
        MethodInfo directModifiers,
        MethodInfo getOwner,
        MethodInfo setOwner,
        MethodInfo afterClonedOnCard)
    {
        public Type ModifierType => modifierType;

        public IList DirectModifiers(CardModel card)
            => directModifiers.Invoke(null, [card]) as IList
                ?? throw new InvalidOperationException("BaseLib DirectModifiers did not return a list.");

        public CardModel? GetOwner(AbstractModel modifier)
            => getOwner.Invoke(modifier, null) as CardModel;

        public void SetOwner(AbstractModel modifier, CardModel owner)
            => setOwner.Invoke(modifier, [owner]);

        public void AfterClonedOnCard(AbstractModel modifier, CardModel card)
            => afterClonedOnCard.Invoke(modifier, [card]);
    }
}
