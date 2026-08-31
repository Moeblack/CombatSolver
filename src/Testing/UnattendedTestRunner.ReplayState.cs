using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private static async Task ApplyReplayStateAsync(
        CombatState combatState,
        Player player,
        string replayStatePath,
        string? runSnapshotPath)
    {
        if (!File.Exists(replayStatePath))
            throw new FileNotFoundException("找不到无人测试中途战斗状态。", replayStatePath);
        if (string.IsNullOrWhiteSpace(runSnapshotPath) || !File.Exists(runSnapshotPath))
        {
            throw new InvalidOperationException(
                "中途战斗状态导入需要同一检查点的 run-state 快照，以恢复完整跑局和 RNG。");
        }

        using JsonDocument document = JsonDocument.Parse(
            await File.ReadAllTextAsync(replayStatePath));
        JsonElement root = document.RootElement;
        int schemaVersion = root.GetProperty("schemaVersion").GetInt32();
        if (schemaVersion != 1)
            throw new InvalidOperationException($"不支持 replay-state schemaVersion={schemaVersion}。");
        string expectedEncounterId = RequiredString(root, "encounterId");
        if (combatState.Encounter == null
            || !ModelMatches(combatState.Encounter, expectedEncounterId))
        {
            throw new InvalidOperationException(
                $"replay-state 遭遇为 {expectedEncounterId}，当前夹具为 " +
                $"{combatState.Encounter?.Id.Entry ?? "-"}。");
        }
        string currentSide = RequiredString(root, "currentSide");
        if (!string.Equals(currentSide, combatState.CurrentSide.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"replay-state 阵营为 {currentSide}，当前夹具为 {combatState.CurrentSide}。");
        }

        JsonElement[] savedPlayers = root.GetProperty("players").EnumerateArray().ToArray();
        if (savedPlayers.Length != 1)
            throw new InvalidOperationException("replay-state 导入器只支持单人战斗。");
        JsonElement savedPlayer = savedPlayers[0];
        string expectedCharacterId = RequiredString(savedPlayer, "characterId");
        if (!ModelMatches(player.Character, expectedCharacterId))
        {
            throw new InvalidOperationException(
                $"replay-state 角色为 {expectedCharacterId}，当前夹具为 {player.Character.Id.Entry}。");
        }

        await RestoreReplayCreaturesAsync(combatState, root.GetProperty("creatures"));
        CombatState replayCombat = combatState;
        replayCombat.RoundNumber = root.GetProperty("roundNumber").GetInt32();
        PlayerCombatState playerState = player.PlayerCombatState
            ?? throw new InvalidOperationException("replay-state 导入时玩家没有战斗状态。");
        int turnNumber = savedPlayer.GetProperty("turnNumber").GetInt32();
        if (turnNumber < playerState.TurnNumber)
        {
            throw new InvalidOperationException(
                $"replay-state 回合 {turnNumber} 早于当前夹具回合 {playerState.TurnNumber}。");
        }
        while (playerState.TurnNumber < turnNumber)
            playerState.IncrementTurnNumber();
        string expectedPhase = RequiredString(savedPlayer, "phase");
        if (!string.Equals(expectedPhase, playerState.Phase.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"replay-state 玩家阶段为 {expectedPhase}，当前夹具为 {playerState.Phase}。");
        }
        SetEnergy(player, savedPlayer.GetProperty("energy").GetInt32());
        SetStars(player, savedPlayer.GetProperty("stars").GetInt32());
        player.Gold = savedPlayer.GetProperty("gold").GetInt32();

        ValidateReplayInventory(player, savedPlayer);
        await ClearPlayerPilesAsync(player);
        await RestoreReplayPilesAsync(combatState, player, savedPlayer.GetProperty("piles"));
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        ReloadRunSnapshotRng((RunState)combatState.RunState, player, runSnapshotPath);

        ContinuationStamp expected = new(RequiredString(root, "exactContinuationState"));
        ContinuationStamp actual = ContinuationStamp.CaptureLive(combatState);
        if (!string.Equals(expected.StateText, actual.StateText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "replay-state 严格导入不一致：" + expected.DescribeFirstDifference(actual));
        }
    }

    private static async Task RestoreReplayCreaturesAsync(
        CombatState combatState,
        JsonElement savedCreaturesElement)
    {
        JsonElement[] savedCreatures = savedCreaturesElement.EnumerateArray().ToArray();
        if (savedCreatures.Length != combatState.Creatures.Count)
        {
            throw new InvalidOperationException(
                $"replay-state 生物数 {savedCreatures.Length} 与当前夹具 " +
                $"{combatState.Creatures.Count} 不同。");
        }

        foreach (JsonElement saved in savedCreatures)
        {
            uint combatId = saved.GetProperty("combatId").GetUInt32();
            Creature creature = combatState.Creatures.SingleOrDefault(candidate =>
                    candidate.CombatId == combatId)
                ?? throw new InvalidOperationException($"当前夹具缺少 CombatId={combatId} 的生物。");
            string? monsterId = OptionalString(saved, "monsterId");
            if (monsterId == null)
            {
                ulong expectedPlayerNetId = saved.GetProperty("playerNetId").GetUInt64();
                if (creature.Player == null || creature.Player.NetId != expectedPlayerNetId)
                {
                    throw new InvalidOperationException($"CombatId={combatId} 不是 replay-state 中的玩家。");
                }
            }
            else if (creature.Monster == null || !ModelMatches(creature.Monster, monsterId))
            {
                throw new InvalidOperationException(
                    $"CombatId={combatId} 怪物为 {creature.Monster?.Id.Entry ?? "-"}，" +
                    $"replay-state 为 {monsterId}。");
            }

            await CreatureCmd.SetMaxHp(creature, saved.GetProperty("maxHp").GetInt32());
            await CreatureCmd.SetCurrentHp(creature, saved.GetProperty("currentHp").GetInt32());
            await SetBlockAsync(creature, saved.GetProperty("block").GetInt32());
            ValidateReplayPowers(creature, saved.GetProperty("powers"));
            if (creature.Monster != null)
                RestoreReplayMonsterMove(creature.Monster, saved);
        }
    }

    private static void ValidateReplayPowers(Creature creature, JsonElement savedPowersElement)
    {
        JsonElement[] savedPowers = savedPowersElement.EnumerateArray().ToArray();
        if (savedPowers.Length != creature.Powers.Count)
        {
            throw new InvalidOperationException(
                $"CombatId={creature.CombatId} Power 数为 {creature.Powers.Count}，" +
                $"replay-state 为 {savedPowers.Length}。");
        }
        for (int index = 0; index < savedPowers.Length; index++)
        {
            JsonElement saved = savedPowers[index];
            PowerModel power = creature.Powers[index];
            string id = RequiredString(saved, "id");
            int amount = saved.GetProperty("amount").GetInt32();
            int amountOnTurnStart = saved.GetProperty("amountOnTurnStart").GetInt32();
            if (!ModelMatches(power, id)
                || power.Amount != amount
                || power.AmountOnTurnStart != amountOnTurnStart)
            {
                throw new InvalidOperationException(
                    $"CombatId={creature.CombatId} Power[{index}] 为 " +
                    $"{power.Id.Entry}/{power.Amount}/{power.AmountOnTurnStart}，" +
                    $"replay-state 为 {id}/{amount}/{amountOnTurnStart}。");
            }
        }
    }

    private static void RestoreReplayMonsterMove(MonsterModel monster, JsonElement saved)
    {
        MonsterMoveStateMachine machine = monster.MoveStateMachine
            ?? throw new InvalidOperationException($"怪物 {monster.Id.Entry} 没有行动状态机。");
        machine.StateLog.Clear();
        foreach (JsonElement stateElement in saved.GetProperty("moveStateLog").EnumerateArray())
        {
            string stateId = stateElement.GetString()
                ?? throw new InvalidOperationException("replay-state 怪物行动历史包含空 ID。");
            if (!machine.States.TryGetValue(stateId, out MonsterState? state))
                throw new InvalidOperationException($"怪物 {monster.Id.Entry} 没有行动状态 {stateId}。");
            machine.StateLog.Add(state);
        }
        string? nextMoveId = OptionalString(saved, "nextMoveId");
        if (nextMoveId == null)
            return;
        if (!machine.States.TryGetValue(nextMoveId, out MonsterState? nextState)
            || nextState is not MoveState move)
        {
            throw new InvalidOperationException($"怪物 {monster.Id.Entry} 没有行动 {nextMoveId}。");
        }
        monster.SetMoveImmediate(move, true);
    }

    private static void ValidateReplayInventory(Player player, JsonElement savedPlayer)
    {
        JsonElement[] savedPotions = savedPlayer.GetProperty("potions").EnumerateArray().ToArray();
        if (savedPotions.Length != player.PotionSlots.Count)
            throw new InvalidOperationException("replay-state 药水槽数量与跑局快照不同。");
        for (int slot = 0; slot < savedPotions.Length; slot++)
        {
            string? expected = OptionalString(savedPotions[slot], "id");
            string? actual = player.GetPotionAtSlotIndex(slot)?.Id.Entry;
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"药水槽 {slot} 为 {actual ?? "-"}，replay-state 为 {expected ?? "-"}。");
            }
        }

        JsonElement[] savedRelics = savedPlayer.GetProperty("relics").EnumerateArray().ToArray();
        if (savedRelics.Length != player.Relics.Count)
            throw new InvalidOperationException("replay-state 遗物数量与跑局快照不同。");
        for (int index = 0; index < savedRelics.Length; index++)
        {
            string expected = RequiredString(savedRelics[index], "id");
            if (!ModelMatches(player.Relics[index], expected))
            {
                throw new InvalidOperationException(
                    $"遗物[{index}] 为 {player.Relics[index].Id.Entry}，replay-state 为 {expected}。");
            }
        }
    }

    private static async Task RestoreReplayPilesAsync(
        CombatState combatState,
        Player player,
        JsonElement savedPilesElement)
    {
        foreach (JsonElement savedPile in savedPilesElement.EnumerateArray())
        {
            string pile = RequiredString(savedPile, "pile");
            JsonElement[] savedCards = savedPile.GetProperty("cards").EnumerateArray().ToArray();
            if (string.Equals(pile, "Play", StringComparison.OrdinalIgnoreCase))
            {
                if (savedCards.Length != 0)
                    throw new InvalidOperationException("replay-state 导入暂不支持非空 Play 牌堆。");
                continue;
            }
            foreach (JsonElement savedCard in savedCards)
            {
                UnattendedCardInjection injection = BuildReplayCardInjection(savedCard, pile);
                CardModel restored = (await InjectCardAsync(combatState, player, injection)).Single();
                AttachReplayDeckVersion(restored, savedCard, player);
            }
        }
    }

    private static UnattendedCardInjection BuildReplayCardInjection(
        JsonElement savedCard,
        string pile)
    {
        Dictionary<string, int> dynamicVars = new(StringComparer.Ordinal);
        foreach (JsonProperty property in savedCard.GetProperty("dynamicVars").EnumerateObject())
        {
            decimal baseValue = property.Value.GetProperty("baseValue").GetDecimal();
            if (baseValue != decimal.Truncate(baseValue)
                || baseValue < int.MinValue
                || baseValue > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"卡牌 {RequiredString(savedCard, "id")} 动态变量 {property.Name} " +
                    $"无法以整数恢复：{baseValue}。");
            }
            dynamicVars[property.Name] = decimal.ToInt32(baseValue);
        }
        Dictionary<string, string> enumMembers = new(StringComparer.Ordinal);
        foreach (JsonProperty field in savedCard.GetProperty("fields").EnumerateObject())
        {
            if (field.Name.EndsWith("._tinkerTimeType", StringComparison.Ordinal)
                || field.Name.EndsWith("._tinkerTimeRider", StringComparison.Ordinal))
            {
                string member = field.Name[(field.Name.LastIndexOf('.') + 1)..];
                enumMembers[member] = field.Value.GetString()
                    ?? throw new InvalidOperationException($"卡牌枚举字段 {field.Name} 为空。");
            }
        }
        JsonElement enchantment = savedCard.GetProperty("enchantment");
        JsonElement affliction = savedCard.GetProperty("affliction");
        return new UnattendedCardInjection
        {
            CardId = RequiredString(savedCard, "id"),
            Pile = pile,
            Count = 1,
            UpgradeLevels = savedCard.GetProperty("currentUpgradeLevel").GetInt32(),
            EnchantmentId = enchantment.ValueKind == JsonValueKind.Null
                ? null
                : RequiredString(enchantment, "id"),
            EnchantmentAmount = enchantment.ValueKind == JsonValueKind.Null
                ? 1
                : enchantment.GetProperty("amount").GetInt32(),
            AfflictionId = affliction.ValueKind == JsonValueKind.Null
                ? null
                : RequiredString(affliction, "id"),
            AfflictionAmount = affliction.ValueKind == JsonValueKind.Null
                ? 1
                : affliction.GetProperty("amount").GetInt32(),
            DynamicVars = dynamicVars,
            EnumMembers = enumMembers,
        };
    }

    private static void AttachReplayDeckVersion(
        CardModel restored,
        JsonElement savedCard,
        Player player)
    {
        JsonElement serialized = savedCard.GetProperty("serialized");
        if (!serialized.TryGetProperty("floor_added_to_deck", out JsonElement floor)
            || floor.ValueKind == JsonValueKind.Null)
        {
            return;
        }
        int floorAdded = floor.GetInt32();
        CardModel[] matches = player.Deck.Cards.Where(candidate =>
                candidate.Id.Entry.Equals(restored.Id.Entry, StringComparison.Ordinal)
                && candidate.ToSerializable().FloorAddedToDeck == floorAdded
                && MatchesReplayDeckEnchantment(candidate, serialized))
            .ToArray();
        if (matches.Length == 0)
        {
            throw new InvalidOperationException(
                $"卡牌 {restored.Id.Entry}@{floorAdded} 找不到语义一致的跑局版本，" +
                "无法严格恢复 DeckVersion。");
        }
        restored.DeckVersion = matches[0];
    }

    private static bool MatchesReplayDeckEnchantment(CardModel candidate, JsonElement serialized)
    {
        if (!serialized.TryGetProperty("enchantment", out JsonElement savedEnchantment)
            || savedEnchantment.ValueKind == JsonValueKind.Null)
        {
            return candidate.Enchantment == null;
        }

        return candidate.Enchantment != null
               && ModelMatches(candidate.Enchantment, RequiredString(savedEnchantment, "id"))
               && candidate.Enchantment.Amount == savedEnchantment.GetProperty("amount").GetInt32();
    }

    private static string RequiredString(JsonElement element, string propertyName)
        => element.GetProperty(propertyName).GetString()
            ?? throw new InvalidOperationException($"replay-state 字段 {propertyName} 为空。");

    private static string? OptionalString(JsonElement element, string propertyName)
    {
        JsonElement value = element.GetProperty(propertyName);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }
}
