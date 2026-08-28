using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Simulation;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private const string BaseLibCardModifierTypeName = "BaseLib.Abstracts.CardModifier";

    private static async Task AssertBaseLibCardModifierBoundaryAsync(
        CombatState combat,
        Player player)
    {
        if (!NGame.IsMainThread())
            throw new InvalidOperationException("BaseLib CardModifier 边界测试必须从主线程开始。");
        PlayerCombatState playerState = player.PlayerCombatState
            ?? throw new InvalidOperationException("BaseLib CardModifier 边界测试找不到玩家战斗状态。");
        CardModel owner = playerState.AllCards.FirstOrDefault()
            ?? throw new InvalidOperationException("BaseLib CardModifier 边界测试需要至少一张战斗卡牌。");

        Type modifierBaseType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(BaseLibCardModifierTypeName, throwOnError: false))
            .FirstOrDefault(type => type != null)
            ?? throw new InvalidOperationException("完整 Mod 栈没有加载 BaseLib CardModifier。");
        MethodInfo directModifiers = modifierBaseType.GetMethod(
            "DirectModifiers",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(CardModel)],
            modifiers: null)
            ?? throw new MissingMethodException(BaseLibCardModifierTypeName, "DirectModifiers(CardModel)");
        PropertyInfo ownerProperty = modifierBaseType.GetProperty(
            "Owner",
            BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(BaseLibCardModifierTypeName, "Owner");
        FieldInfo amountField = modifierBaseType.GetField(
            "<Amount>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(BaseLibCardModifierTypeName, "<Amount>k__BackingField");

        Type fixtureType = CreateBaseLibCardModifierFixtureType(modifierBaseType);
        AbstractModel liveModifier = (AbstractModel)RuntimeHelpers.GetUninitializedObject(fixtureType);
        ownerProperty.SetValue(liveModifier, owner);
        amountField.SetValue(liveModifier, 7);
        IList liveModifiers = GetDirectModifiers(directModifiers, owner);
        liveModifiers.Add(liveModifier);

        try
        {
            if (!combat.IterateHookListeners().Contains(liveModifier))
                throw new InvalidOperationException("BaseLib 没有把测试 CardModifier 登记为实机战斗 listener。");

            CombatRootSnapshot root = CombatRootSnapshot.Capture(combat);
            if (!root.CapturedBaseLibCardModifiers)
                throw new InvalidOperationException("根快照没有识别实机 BaseLib CardModifier subscriber。");

            CombatPredictionSimulator fork = await Task.Run(root.ForkSimulator);
            SimPlayerCombatState predictedPlayer = fork.State.GetPlayerCombatState(player);
            PredictedCard predictedCard = predictedPlayer.FindCard(owner)
                ?? throw new InvalidOperationException("预测分支没有找到 CardModifier 的 Owner 卡牌。");
            SimulatedCombatState predictedCombat = (SimulatedCombatState)fork.State.CombatState;
            AbstractModel firstClone = AssertModifierClone(
                predictedCombat,
                predictedCard.Preview,
                liveModifier,
                fixtureType,
                ownerProperty,
                amountField);

            CardModel sharedPreview = predictedCard.Preview;
            CardModel mutablePreview = predictedCard.MutablePreview;
            if (ReferenceEquals(sharedPreview, mutablePreview))
                throw new InvalidOperationException("预测卡牌写入没有触发 Preview 写时复制。");
            AbstractModel secondClone = AssertModifierClone(
                predictedCombat,
                mutablePreview,
                liveModifier,
                fixtureType,
                ownerProperty,
                amountField);
            if (ReferenceEquals(firstClone, secondClone))
                throw new InvalidOperationException("卡牌写时复制后仍复用了旧 BaseLib CardModifier listener。");
        }
        finally
        {
            liveModifiers.Remove(liveModifier);
            ownerProperty.SetValue(liveModifier, null);
        }
    }

    private static AbstractModel AssertModifierClone(
        SimulatedCombatState combat,
        CardModel expectedOwner,
        AbstractModel liveModifier,
        Type fixtureType,
        PropertyInfo ownerProperty,
        FieldInfo amountField)
    {
        AbstractModel clone = ((ICombatPredictionHookListenerSource)combat).HookListeners
            .Single(listener => listener.GetType() == fixtureType);
        if (ReferenceEquals(clone, liveModifier)
            || !ReferenceEquals(ownerProperty.GetValue(clone), expectedOwner)
            || (int?)amountField.GetValue(clone) != 7)
        {
            throw new InvalidOperationException(
                "BaseLib CardModifier 没有以独立克隆、正确 Owner 和原状态进入预测 listener。");
        }
        return clone;
    }

    private static IList GetDirectModifiers(MethodInfo directModifiers, CardModel card)
        => directModifiers.Invoke(null, [card]) as IList
            ?? throw new InvalidOperationException("BaseLib DirectModifiers 没有返回 IList。");

    private static Type CreateBaseLibCardModifierFixtureType(Type baseType)
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"CombatSolver.BaseLibFixture.{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        TypeBuilder type = assembly.DefineDynamicModule("Fixture")
            .DefineType(
                "CombatSolver.UnattendedBaseLibCardModifier",
                TypeAttributes.Public | TypeAttributes.Sealed,
                baseType);
        type.DefineDefaultConstructor(MethodAttributes.Public);
        return type.CreateType();
    }
}
