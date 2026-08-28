using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Patching.Models;

namespace CombatSolver;

internal static class RitsuEmptyCapabilityFastPath
{
    private const string CardCapabilityHostTypeName =
        "STS2RitsuLib.Models.Capabilities.CardModelCapabilityHost";
    private static readonly Func<AbstractModel, bool> HasDefaultCapabilitySource =
        CreateHasDefaultCapabilitySource();

    internal static ModPatchTarget CardHostTarget(string methodName, params Type[] parameters)
    {
        Type host = typeof(ModelCapabilities).Assembly.GetType(CardCapabilityHostTypeName)
            ?? throw new TypeLoadException(CardCapabilityHostTypeName);
        return new ModPatchTarget(host, methodName, parameters);
    }

    internal static bool CanSkip(AbstractModel model)
    {
        if (!SimulationNotificationIsolation.IsActive)
            return false;
        if (ModelCapabilities.TryGet(model, out ModelCapabilitySet? capabilities))
            return capabilities.Count == 0;
        return !HasDefaultCapabilitySource(model);
    }

    private static Func<AbstractModel, bool> CreateHasDefaultCapabilitySource()
    {
        Type defaults = typeof(ModelCapabilities).Assembly.GetType(
            "STS2RitsuLib.Models.Capabilities.ModelCapabilityDefaults")
            ?? throw new TypeLoadException("STS2RitsuLib.Models.Capabilities.ModelCapabilityDefaults");
        MethodInfo method = defaults.GetMethod(
            "HasDefaultCapabilitySource",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(AbstractModel)],
            modifiers: null)
            ?? throw new MissingMethodException(defaults.FullName, "HasDefaultCapabilitySource(AbstractModel)");
        return method.CreateDelegate<Func<AbstractModel, bool>>();
    }
}

internal sealed class RitsuEmptyCardTypeFastPathPatch : IPatchMethod
{
    public static string PatchId => "combat_solver_ritsu_empty_card_type_fast_path";
    public static string Description => "求解模拟跳过空 Ritsu capability 的卡牌类型贡献管线";

    public static ModPatchTarget[] GetTargets() =>
    [
        RitsuEmptyCapabilityFastPath.CardHostTarget(
            "ApplyCardType",
            typeof(CardModel),
            typeof(CardType)),
    ];

    public static bool Prefix(CardModel card, CardType current, ref CardType __result)
    {
        if (!RitsuEmptyCapabilityFastPath.CanSkip(card))
            return true;
        __result = current;
        return false;
    }
}

internal sealed class RitsuEmptyEnergyContributorFastPathPatch : IPatchMethod
{
    public static string PatchId => "combat_solver_ritsu_empty_energy_contributor_fast_path";
    public static string Description => "求解模拟跳过空 Ritsu capability 的费用贡献者查询";

    public static ModPatchTarget[] GetTargets() =>
    [
        RitsuEmptyCapabilityFastPath.CardHostTarget(
            "HasEnergyCostContributors",
            typeof(CardModel)),
    ];

    public static bool Prefix(CardModel card, ref bool __result)
    {
        if (!RitsuEmptyCapabilityFastPath.CanSkip(card))
            return true;
        __result = false;
        return false;
    }
}

internal sealed class RitsuEmptyEnergyCostFastPathPatch : IPatchMethod
{
    public static string PatchId => "combat_solver_ritsu_empty_energy_cost_fast_path";
    public static string Description => "求解模拟跳过空 Ritsu capability 的费用修改管线";

    public static ModPatchTarget[] GetTargets() =>
    [
        RitsuEmptyCapabilityFastPath.CardHostTarget(
            "ApplyEnergyCost",
            typeof(CardModel),
            typeof(CostModifiers),
            typeof(int)),
    ];

    public static bool Prefix(CardModel card, int current, ref int __result)
    {
        if (!RitsuEmptyCapabilityFastPath.CanSkip(card))
            return true;
        __result = current;
        return false;
    }
}
