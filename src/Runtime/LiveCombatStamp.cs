using System.Text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver;

/// <summary>
/// 搜索开始时的完整可见状态文本，用于拒绝已经过期的后台结果；不做摘要或哈希。
/// </summary>
internal sealed record LiveCombatStamp(string StateText)
{
    public static LiveCombatStamp Capture(CombatState state)
    {
        Player player = LocalContext.GetMe(state)
            ?? throw new InvalidOperationException("找不到本地玩家。");
        PlayerCombatState pcs = player.PlayerCombatState
            ?? throw new InvalidOperationException("玩家没有战斗状态。");
        StringBuilder text = new();
        text.Append("round=").Append(state.RoundNumber)
            .Append(";turn=").Append(pcs.TurnNumber)
            .Append(";side=").Append(state.CurrentSide)
            .Append(";phase=").Append(pcs.Phase)
            .Append(";hp=").Append(player.Creature.CurrentHp)
            .Append(";block=").Append(player.Creature.Block)
            .Append(";energy=").Append(pcs.Energy)
            .Append(";stars=").Append(pcs.Stars)
            .Append(";osty=").Append(player.Osty?.CombatId ?? uint.MaxValue)
            .Append('/').Append(player.Osty?.CurrentHp ?? 0)
            .Append('/').Append(player.Osty?.MaxHp ?? 0);

        for (int i = 0; i < state.Enemies.Count; i++)
        {
            var enemy = state.Enemies[i];
            text.Append(";enemy[").Append(i).Append("]=")
                .Append(enemy.Monster?.Id.Entry ?? "null").Append('/')
                .Append(enemy.CurrentHp).Append('/').Append(enemy.Block).Append('/')
                .Append(enemy.Monster?.NextMove?.Id ?? "null");
        }

        AppendPile(text, pcs.Hand, 'H');
        AppendPile(text, pcs.DrawPile, 'D');
        AppendPile(text, pcs.DiscardPile, 'C');
        AppendPile(text, pcs.ExhaustPile, 'X');
        AppendPile(text, pcs.PlayPile, 'P');
        return new LiveCombatStamp(text.ToString());
    }

    private static void AppendPile(StringBuilder text, CardPile pile, char marker)
    {
        text.Append(';').Append(marker).Append('=');
        foreach (CardModel card in pile.Cards)
        {
            text.Append(card.Id.Entry).Append('+').Append(card.CurrentUpgradeLevel)
                .Append(':').Append(card.EnergyCost.GetWithModifiers(CostModifiers.All))
                .Append(':').Append(card.GetStarCostWithModifiers())
                .Append(':').Append(card.BaseReplayCount)
                .Append(':').Append(card.ExhaustOnNextPlay)
                .Append(':').Append(card.IsSlyThisTurn)
                .Append(':').Append(card.ShouldRetainThisTurn).Append(',');
        }
    }
}
