using System.IO.Compression;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace CombatSolver;

internal static class CombatBugReportExporter
{
    private const string ExportFolderName = "CombatSolver-BugReports";
    private const long MaximumLogBytes = 16L * 1024 * 1024;
    private const long MaximumCombatLogBytes = 32L * 1024 * 1024;
    private const long MaximumCapturedSaveBytes = 4L * 1024 * 1024;
    private const int MaximumCheckpoints = 32;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed record CapturedFile(string SourceRelativePath, byte[] Bytes);
    private sealed record ForensicCheckpoint(
        string Label,
        string StateText,
        string MetadataJson,
        string ReplayStateJson,
        byte[] NativeCombatState,
        byte[] InMemoryRunSave);
    private sealed record ForensicLogRange(string Path, string EntryName, long Start, long End);
    private sealed record ForensicArchiveCheckpoint(
        string Name,
        string MetadataJson,
        string ReplayStateJson,
        byte[] NativeCombatState,
        byte[] InMemoryRunSave);

    private sealed class ForensicSession
    {
        public required string SessionId { get; init; }
        public required string EncounterId { get; init; }
        public required string EncounterType { get; init; }
        public required string Seed { get; init; }
        public required DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset? EndedAt { get; set; }
        public string? EndReason { get; set; }
        public CapturedFile? InMemoryRunSave { get; set; }
        public CapturedFile? PreCombatRunSave { get; set; }
        public CapturedFile? PreCombatProgressSave { get; set; }
        public int PreCombatDiskCaptureAttempts { get; set; }
        public List<ForensicCheckpoint> Checkpoints { get; } = [];
        public Dictionary<string, long> LogStartOffsets { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> LogEndOffsets { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string LastRoute { get; set; } = "当前没有已完成的求解路线。";
        public string ReplanAudit { get; set; } = string.Empty;
        public string ControlMode { get; set; } = "solver_only";
        public int? LastSolverDeployedTurn { get; set; }
    }

    private sealed record ForensicArchiveSession(
        string SessionId,
        string EncounterId,
        string SessionJson,
        IReadOnlyList<ForensicArchiveCheckpoint> Checkpoints,
        CapturedFile? InMemoryRunSave,
        CapturedFile? PreCombatRunSave,
        CapturedFile? PreCombatProgressSave,
        IReadOnlyList<ForensicLogRange> Logs,
        string LastRoute,
        string ReplanAudit);

    private sealed record ForensicArchiveBundle(
        string ManifestJson,
        ForensicArchiveSession? Current,
        ForensicArchiveSession? Recent);

    private static ForensicSession? _currentSession;
    private static ForensicSession? _lastSession;

    public static void BeginCombat(ICombatState? rawState)
    {
        if (!NGame.IsMainThread())
            throw new InvalidOperationException("战斗取证只能从游戏主线程采集。");
        if (rawState is not CombatState state)
            return;

        if (_currentSession != null)
            CompleteCombat("combat_replaced", null, string.Empty);
        _currentSession = new ForensicSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            EncounterId = state.Encounter?.Id.Entry ?? "unknown",
            EncounterType = state.Encounter?.RoomType.ToString() ?? "unknown",
            Seed = state.RunState.Rng.StringSeed,
            StartedAt = DateTimeOffset.Now,
        };
        CaptureInMemoryRunSave(_currentSession);
        CaptureLogStarts(_currentSession);
        TryCapturePreCombatFiles(_currentSession, state);
        RecordCheckpointCore(state, "combat_start", null, string.Empty);
    }

    public static void RecordCheckpoint(
        CombatState state,
        string label,
        SolverResult? result,
        string replanAudit)
    {
        if (!NGame.IsMainThread())
            throw new InvalidOperationException("战斗取证只能从游戏主线程采集。");
        EnsureSession(state);
        RecordCheckpointCore(state, label, result, replanAudit);
    }

    public static void CompleteCombat(string reason, SolverResult? result, string replanAudit)
    {
        if (!NGame.IsMainThread())
            throw new InvalidOperationException("战斗取证只能从游戏主线程采集。");
        ForensicSession? session = _currentSession;
        if (session == null)
            return;

        CombatState? live = CombatManager.Instance.DebugOnlyGetState();
        if (live != null && live.RunState.Rng.StringSeed == session.Seed)
            RecordCheckpointCore(live, "combat_end", result, replanAudit);
        UpdateSessionResult(session, result, replanAudit);
        session.EndedAt = DateTimeOffset.Now;
        session.EndReason = reason;
        CaptureLogEnds(session);
        _lastSession = session;
        _currentSession = null;
    }

    public static Task<string> ExportCurrentAsync(string? outputDirectory = null)
    {
        if (!NGame.IsMainThread())
            throw new InvalidOperationException("问题包只能从游戏主线程导出。");

        CombatState? state = CombatManager.Instance.IsInProgress
            ? CombatManager.Instance.DebugOnlyGetState()
            : null;
        SolverResult? result = SolverController.CurrentResultForBugReport;
        string replanAudit = SolverController.ReplanAuditForBugReport;
        if (state != null)
            RecordCheckpoint(state, "export_clicked", result, replanAudit);

        SolverSettingsSnapshot profiles = SolverSettings.Capture();
        string settingsJson = JsonSerializer.Serialize(SolverSettings.Current, JsonOptions);
        string combatJson = CaptureCombatState(state, profiles);
        string routeText = DescribeRoute(result);
        string exportContextJson = CaptureExportContext(state);
        string environmentJson = JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            capturedAt = DateTimeOffset.Now,
            modVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            gameExecutable = OS.GetExecutablePath(),
            userDataDirectory = OS.GetUserDataDir(),
            os = System.Environment.OSVersion.ToString(),
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            framework = RuntimeInformation.FrameworkDescription,
            loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .Select(assembly => new
                {
                    name = assembly.GetName().Name,
                    version = assembly.GetName().Version?.ToString(),
                    assembly.Location,
                })
                .OrderBy(assembly => assembly.name, StringComparer.Ordinal)
                .ToArray(),
        }, JsonOptions);
        ForensicArchiveBundle forensics = CaptureForensicBundle();

        string exportDirectory = outputDirectory ?? DefaultExportDirectory();
        Directory.CreateDirectory(exportDirectory);
        string encounter = SanitizeFileName(
            state?.Encounter?.Id.Entry
            ?? forensics.Recent?.EncounterId
            ?? forensics.Current?.EncounterId
            ?? "no-combat");
        string path = Path.Combine(
            exportDirectory,
            $"CombatSolver-{encounter}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.zip");
        string userDataDirectory = OS.GetUserDataDir();
        string executableDirectory = Path.GetDirectoryName(OS.GetExecutablePath())
            ?? throw new DirectoryNotFoundException("无法定位游戏目录。");

        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("游戏节点尚未创建。");
        Image? image = game.GetViewport()?.GetTexture()?.GetImage();
        byte[] screenshot = image == null || image.IsEmpty()
            ? []
            : image.SavePngToBuffer();
        return Task.Run(() => WriteArchive(
            path,
            userDataDirectory,
            executableDirectory,
            screenshot,
            combatJson,
            routeText,
            replanAudit,
            settingsJson,
            exportContextJson,
            environmentJson,
            forensics));
    }

    private static string WriteArchive(
        string path,
        string userDataDirectory,
        string executableDirectory,
        byte[] screenshot,
        string combatJson,
        string routeText,
        string replanAudit,
        string settingsJson,
        string exportContextJson,
        string environmentJson,
        ForensicArchiveBundle forensics)
    {
        using FileStream output = new(path, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.None);
        using ZipArchive archive = new(output, ZipArchiveMode.Create);
        string logsDirectory = Path.Combine(userDataDirectory, "logs");
        if (Directory.Exists(logsDirectory))
        {
            foreach (string log in Directory.EnumerateFiles(logsDirectory, "*.log", SearchOption.AllDirectories))
            {
                AddFileTail(
                    archive,
                    log,
                    "logs/" + NormalizeEntryPath(Path.GetRelativePath(logsDirectory, log)),
                    MaximumLogBytes);
            }
        }

        string[] saveNames = ["current_run.save", "progress.save", "prefs.save", "settings.save", "latest.mcr"];
        foreach (string file in Directory.EnumerateFiles(userDataDirectory, "*", SearchOption.AllDirectories)
                     .Where(file => saveNames.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)))
        {
            AddFile(
                archive,
                file,
                "saves/" + NormalizeEntryPath(Path.GetRelativePath(userDataDirectory, file)));
        }
        string releaseInfo = Path.Combine(executableDirectory, "release_info.json");
        if (File.Exists(releaseInfo))
            AddFile(archive, releaseInfo, "release_info.json");
        if (screenshot.Length > 0)
            AddBytes(archive, "screenshot.png", screenshot);

        AddText(archive, "combat-solver/combat-state.json", combatJson);
        AddText(archive, "combat-solver/current-route.txt", routeText);
        AddText(archive, "combat-solver/replan-audit.txt", replanAudit);
        AddText(archive, "combat-solver/settings.json", settingsJson);
        AddText(archive, "combat-solver/export-context.json", exportContextJson);
        AddText(archive, "combat-solver/environment.json", environmentJson);
        AddText(archive, "combat-solver/forensics/manifest.json", forensics.ManifestJson);
        WriteForensicSession(archive, "current", forensics.Current);
        WriteForensicSession(archive, "recent", forensics.Recent);
        AddText(
            archive,
            "combat-solver/README.txt",
            "此问题包由 CombatSolver 设置页导出。包含最近 16 MB 游戏日志、当前档案、截图、当前战斗状态、路线、重算审计、设置和运行环境。\n" +
            "forensics/current 保存当前战斗；forensics/recent 保存最近结束的一场。每场最多 32 个检查点。每个检查点都有 metadata、replay-state、native-state 和 run-state 四份同名材料。\n" +
            "replay-state 是可机器读取的完整中途战斗夹具，含有序牌堆、逐牌存档/动态状态、Power/遗物/怪物字段、行动历史、阵容和全部 RNG；native-state 是游戏原生 NetFullCombatState；run-state 是该检查点时刻的内存跑局存档。\n" +
            "forensics/*/pre-combat 始终包含内存跑局快照；磁盘 current_run.save/progress.save 已写出时会一并原样保存。即使在地图、奖励页或下一场战斗中导出，也优先用 recent 目录还原问题战斗。\n" +
            "session.json、检查点和 export-context.json 会标记 controlMode：solver_only 表示全程由求解器接管，manual_plus_solver 表示本场曾手操后再交给求解器；lastSolverDeployedTurn 记录最近一次完整自动执行的回合。\n" +
            "存档和日志可能包含 Steam 账号标识；导出本身只保存在本机桌面，不会自动上传。" +
            "设置页另有独立的“上传问题包”按钮，需要玩家在确认弹窗里再次点击确认才会把同样内容发送到开发者服务器。\n");
        Entry.Logger.Info($"[CombatSolver/Test] BUG_REPORT_EXPORTED path={path}");
        return path;
    }

    private static void WriteForensicSession(
        ZipArchive archive,
        string slot,
        ForensicArchiveSession? session)
    {
        if (session == null)
            return;
        string root = $"combat-solver/forensics/{slot}";
        AddText(archive, $"{root}/session.json", session.SessionJson);
        foreach (ForensicArchiveCheckpoint checkpoint in session.Checkpoints)
        {
            AddText(archive, $"{root}/checkpoints/{checkpoint.Name}", checkpoint.MetadataJson);
            AddText(
                archive,
                $"{root}/replay-state/{checkpoint.Name}",
                checkpoint.ReplayStateJson);
            string stem = Path.GetFileNameWithoutExtension(checkpoint.Name);
            AddBytes(
                archive,
                $"{root}/native-state/{stem}.bin",
                checkpoint.NativeCombatState);
            AddBytes(
                archive,
                $"{root}/run-state/{stem}.save",
                checkpoint.InMemoryRunSave);
        }
        if (session.InMemoryRunSave != null)
            AddBytes(archive, $"{root}/pre-combat/in-memory-current_run.save", session.InMemoryRunSave.Bytes);
        if (session.PreCombatRunSave != null)
            AddBytes(archive, $"{root}/pre-combat/current_run.save", session.PreCombatRunSave.Bytes);
        if (session.PreCombatProgressSave != null)
            AddBytes(archive, $"{root}/pre-combat/progress.save", session.PreCombatProgressSave.Bytes);
        AddText(archive, $"{root}/last-route.txt", session.LastRoute);
        AddText(archive, $"{root}/replan-audit.txt", session.ReplanAudit);
        foreach (ForensicLogRange log in session.Logs)
        {
            AddFileRange(
                archive,
                log.Path,
                $"{root}/logs/{log.EntryName}",
                log.Start,
                log.End,
                MaximumCombatLogBytes);
        }
    }

    private static void EnsureSession(CombatState state)
    {
        if (_currentSession != null
            && _currentSession.Seed == state.RunState.Rng.StringSeed
            && _currentSession.EncounterId == (state.Encounter?.Id.Entry ?? "unknown"))
        {
            return;
        }
        BeginCombat(state);
    }

    private static void RecordCheckpointCore(
        CombatState state,
        string label,
        SolverResult? result,
        string replanAudit)
    {
        ForensicSession session = _currentSession
            ?? throw new InvalidOperationException("记录战斗取证检查点时没有活动会话。");
        TryCapturePreCombatFiles(session, state);
        SolverSettingsSnapshot profiles = SolverSettings.Capture();
        Player? localPlayer = LocalContext.GetMe(state);
        string stateText = localPlayer?.PlayerCombatState == null
            ? string.Empty
            : ContinuationStamp.CaptureLive(state).StateText;
        string route = DescribeRoute(result);
        string json = JsonSerializer.Serialize(new
        {
            schemaVersion = 3,
            sessionId = session.SessionId,
            label,
            capturedAt = DateTimeOffset.Now,
            encounterId = state.Encounter?.Id.Entry,
            round = state.RoundNumber,
            side = state.CurrentSide.ToString(),
            playerTurn = localPlayer?.PlayerCombatState?.TurnNumber,
            playerPhase = localPlayer?.PlayerCombatState?.Phase.ToString(),
            ascension = state.RunState.AscensionLevel,
            actIndex = state.RunState.CurrentActIndex,
            actFloor = state.RunState.ActFloor,
            totalFloor = state.RunState.TotalFloor,
            exactContinuationState = stateText,
            readableDiagnostic = SolverDiagnostics.DescribeStart(
                state,
                profiles.ShortProfile,
                profiles.DeepProfile),
            runRng = state.RunState.Rng.ToSerializable(),
            players = state.Players.Select(player => new
            {
                netId = player.NetId,
                characterId = player.Character.Id.Entry,
                currentHp = player.Creature.CurrentHp,
                maxHp = player.Creature.MaxHp,
                rng = player.PlayerRng.ToSerializable(),
                odds = player.PlayerOdds.ToSerializable(),
            }).ToArray(),
            settings = SolverSettings.Current,
            result = result == null ? null : new
            {
                result.StartTurnNumber,
                result.SearchedTurns,
                result.BattleHpLostSoFar,
                result.ProjectedBattleHpLost,
                result.BattlePotionsUsedSoFar,
                plannedPotionCount = result.PotionCount,
                result.TheftPolicy,
                result.OutstandingStolenResource,
                result.CombatEndedTurn,
                result.DeathTurn,
                result.BoundaryReason,
                result.OnlyDeathRoutesFound,
            },
            route,
            replanAudit,
            controlMode = SolverController.ControlModeForBugReport,
            lastSolverDeployedTurn = SolverController.LastSolverDeployedTurnForBugReport,
        }, JsonOptions);
        ForensicCheckpoint checkpoint = new(
            label,
            stateText,
            json,
            CaptureReplayState(state, profiles, result),
            CaptureNativeCombatState(state),
            CaptureInMemoryRunSaveBytes());
        if (session.Checkpoints.Count >= MaximumCheckpoints)
            session.Checkpoints.RemoveAt(1);
        session.Checkpoints.Add(checkpoint);
        UpdateSessionResult(session, result, replanAudit);
    }

    private static void UpdateSessionResult(
        ForensicSession session,
        SolverResult? result,
        string replanAudit)
    {
        if (result != null)
            session.LastRoute = DescribeRoute(result);
        if (!string.IsNullOrWhiteSpace(replanAudit))
            session.ReplanAudit = replanAudit;
        session.ControlMode = SolverController.ControlModeForBugReport;
        session.LastSolverDeployedTurn = SolverController.LastSolverDeployedTurnForBugReport;
    }

    private static string DescribeRoute(SolverResult? result)
        => result == null
            ? "当前没有已完成的求解路线。"
            : SolverDiagnostics.DescribeResult(result) + System.Environment.NewLine + result.Format();

    private static ForensicArchiveBundle CaptureForensicBundle()
    {
        ForensicArchiveSession? current = CaptureForensicSession(_currentSession);
        ForensicArchiveSession? recent = CaptureForensicSession(_lastSession);
        string manifest = JsonSerializer.Serialize(new
        {
            schemaVersion = 3,
            capturedAt = DateTimeOffset.Now,
            currentSessionId = current?.SessionId,
            recentSessionId = recent?.SessionId,
            currentEncounterId = current?.EncounterId,
            recentEncounterId = recent?.EncounterId,
            checkpointLimitPerCombat = MaximumCheckpoints,
            checkpointArtifacts = new[] { "metadata", "replay-state", "native-state", "run-state" },
            replayStateSchemaVersion = 1,
            nativeStateFormat = "MegaCrit.Sts2.Core.Entities.Multiplayer.NetFullCombatState",
            currentCombatAvailable = current != null,
            recentCombatAvailable = recent != null,
        }, JsonOptions);
        return new ForensicArchiveBundle(manifest, current, recent);
    }

    private static ForensicArchiveSession? CaptureForensicSession(ForensicSession? session)
    {
        if (session == null)
            return null;
        IReadOnlyList<ForensicLogRange> logs = session.LogStartOffsets
            .Select(item =>
            {
                long end = session.LogEndOffsets.GetValueOrDefault(
                    item.Key,
                    File.Exists(item.Key) ? new FileInfo(item.Key).Length : item.Value);
                return new ForensicLogRange(
                    item.Key,
                    NormalizeEntryPath(Path.GetFileName(item.Key)),
                    item.Value,
                    Math.Max(item.Value, end));
            })
            .ToArray();
        string sessionJson = JsonSerializer.Serialize(new
        {
            schemaVersion = 3,
            session.SessionId,
            session.EncounterId,
            session.EncounterType,
            session.Seed,
            session.StartedAt,
            session.EndedAt,
            session.EndReason,
            session.ControlMode,
            session.LastSolverDeployedTurn,
            checkpointCount = session.Checkpoints.Count,
            checkpointArtifacts = new[] { "metadata", "replay-state", "native-state", "run-state" },
            inMemoryRunSaveCaptured = session.InMemoryRunSave != null,
            preCombatRunSaveSource = session.PreCombatRunSave?.SourceRelativePath,
            preCombatProgressSaveSource = session.PreCombatProgressSave?.SourceRelativePath,
            logRanges = logs.Select(log => new
            {
                log.EntryName,
                log.Start,
                log.End,
                bytes = log.End - log.Start,
            }),
        }, JsonOptions);
        IReadOnlyList<ForensicArchiveCheckpoint> checkpoints = session.Checkpoints
            .Select((checkpoint, index) => new ForensicArchiveCheckpoint(
                $"{index:D3}-{SanitizeFileName(checkpoint.Label)}.json",
                checkpoint.MetadataJson,
                checkpoint.ReplayStateJson,
                checkpoint.NativeCombatState,
                checkpoint.InMemoryRunSave))
            .ToArray();
        return new ForensicArchiveSession(
            session.SessionId,
            session.EncounterId,
            sessionJson,
            checkpoints,
            session.InMemoryRunSave,
            session.PreCombatRunSave,
            session.PreCombatProgressSave,
            logs,
            session.LastRoute,
            session.ReplanAudit);
    }

    private static string CaptureExportContext(CombatState? state)
    {
        IRunState? runState = state?.RunState;
        if (runState == null && RunManager.Instance.IsInProgress)
            runState = RunManager.Instance.DebugOnlyGetState();
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 3,
            capturedAt = DateTimeOffset.Now,
            combatActive = state != null,
            runActive = runState != null,
            runSeed = runState?.Rng.StringSeed,
            runRng = runState?.Rng.ToSerializable(),
            players = runState?.Players.Select(player => new
            {
                netId = player.NetId,
                characterId = player.Character.Id.Entry,
                currentHp = player.Creature.CurrentHp,
                maxHp = player.Creature.MaxHp,
                rng = player.PlayerRng.ToSerializable(),
                odds = player.PlayerOdds.ToSerializable(),
            }).ToArray(),
            currentForensicSessionId = _currentSession?.SessionId,
            recentForensicSessionId = _lastSession?.SessionId,
            currentControlMode = _currentSession?.ControlMode,
            recentControlMode = _lastSession?.ControlMode,
            currentLastSolverDeployedTurn = _currentSession?.LastSolverDeployedTurn,
            recentLastSolverDeployedTurn = _lastSession?.LastSolverDeployedTurn,
        }, JsonOptions);
    }

    private static string CaptureCombatState(
        CombatState? state,
        SolverSettingsSnapshot profiles)
    {
        if (state == null)
        {
            return JsonSerializer.Serialize(new
            {
                schemaVersion = 2,
                capturedAt = DateTimeOffset.Now,
                modVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                combatActive = false,
                recentForensicSessionId = _lastSession?.SessionId,
                recentEncounterId = _lastSession?.EncounterId,
            }, JsonOptions);
        }

        Player? player = LocalContext.GetMe(state);
        string diagnostic = SolverDiagnostics.DescribeStart(
            state,
            profiles.ShortProfile,
            profiles.DeepProfile);
        string exactState = player?.PlayerCombatState == null
            ? string.Empty
            : ContinuationStamp.CaptureLive(state).StateText;
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            capturedAt = DateTimeOffset.Now,
            modVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            combatActive = true,
            encounterId = state.Encounter?.Id.Entry,
            encounterType = state.Encounter?.RoomType.ToString(),
            round = state.RoundNumber,
            side = state.CurrentSide.ToString(),
            playerTurn = player?.PlayerCombatState?.TurnNumber,
            playerPhase = player?.PlayerCombatState?.Phase.ToString(),
            ascension = state.RunState.AscensionLevel,
            actIndex = state.RunState.CurrentActIndex,
            actFloor = state.RunState.ActFloor,
            totalFloor = state.RunState.TotalFloor,
            exactContinuationState = exactState,
            readableDiagnostic = diagnostic,
            runRng = state.RunState.Rng.ToSerializable(),
            playerRng = player?.PlayerRng.ToSerializable(),
            playerOdds = player?.PlayerOdds.ToSerializable(),
        }, JsonOptions);
    }

    private static string CaptureReplayState(
        CombatState state,
        SolverSettingsSnapshot profiles,
        SolverResult? result)
    {
        object[] players = state.Players.Select(player =>
        {
            PlayerCombatState? combat = player.PlayerCombatState;
            CardPile[] piles = combat == null
                ? []
                : [combat.Hand, combat.DrawPile, combat.DiscardPile, combat.ExhaustPile, combat.PlayPile];
            return (object)new
            {
                netId = player.NetId,
                characterId = player.Character.Id.Entry,
                player.Creature.CombatId,
                player.Creature.CurrentHp,
                player.Creature.MaxHp,
                player.Creature.Block,
                player.Gold,
                turnNumber = combat?.TurnNumber,
                phase = combat?.Phase.ToString(),
                energy = combat?.Energy,
                maxEnergy = combat?.MaxEnergy,
                stars = combat?.Stars,
                maxPotionCount = player.MaxPotionCount,
                rng = player.PlayerRng.ToSerializable(),
                odds = player.PlayerOdds.ToSerializable(),
                piles = piles.Select(pile => new
                {
                    pile = pile.Type.ToString(),
                    cards = pile.Cards.Select((card, index) => CaptureCard(card, index)).ToArray(),
                }).ToArray(),
                potions = Enumerable.Range(0, player.PotionSlots.Count).Select(slot =>
                {
                    PotionModel? potion = player.GetPotionAtSlotIndex(slot);
                    return new
                    {
                        slot,
                        id = potion?.Id.Entry,
                        runtimeType = potion?.GetType().FullName,
                        fields = potion == null ? null : CaptureObjectFields(potion),
                    };
                }).ToArray(),
                relics = player.Relics.Select(relic => new
                {
                    id = relic.Id.Entry,
                    runtimeType = relic.GetType().FullName,
                    relic.IsMelted,
                    fields = CaptureObjectFields(relic),
                }).ToArray(),
                orbs = combat == null ? null : new
                {
                    combat.OrbQueue.Capacity,
                    items = combat.OrbQueue.Orbs.Select((orb, index) => new
                    {
                        index,
                        id = orb.Id.Entry,
                        runtimeType = orb.GetType().FullName,
                        passive = orb.PassiveVal,
                        evoke = orb.EvokeVal,
                        fields = CaptureObjectFields(orb),
                    }).ToArray(),
                },
            };
        }).ToArray();

        object[] creatures = state.Creatures.Select((creature, index) => new
        {
            index,
            creature.CombatId,
            creature.SlotName,
            side = creature.Side.ToString(),
            monsterId = creature.Monster?.Id.Entry,
            playerNetId = creature.Player?.NetId,
            petOwnerNetId = creature.PetOwner?.NetId,
            creature.CurrentHp,
            creature.MaxHp,
            creature.Block,
            creature.IsAlive,
            creature.IsDead,
            creature.IsHittable,
            nextMoveId = creature.Monster?.NextMove?.Id,
            moveStateLog = creature.Monster?.MoveStateMachine?.StateLog
                .Select(move => move.Id)
                .ToArray() ?? [],
            monsterFields = creature.Monster == null ? null : CaptureObjectFields(creature.Monster),
            powers = creature.Powers.Select((power, powerIndex) => new
            {
                index = powerIndex,
                id = power.Id.Entry,
                runtimeType = power.GetType().FullName,
                power.Amount,
                power.AmountOnTurnStart,
                ownerCombatId = power.Owner?.CombatId,
                applierCombatId = power.Applier?.CombatId,
                targetCombatId = power.Target?.CombatId,
                dynamicVars = power.DynamicVars.OrderBy(item => item.Key, StringComparer.Ordinal)
                    .ToDictionary(
                        item => item.Key,
                        item => (object)new
                        {
                            runtimeType = item.Value.GetType().FullName,
                            item.Value.BaseValue,
                            item.Value.IntValue,
                        },
                        StringComparer.Ordinal),
                fields = CaptureObjectFields(power),
            }).ToArray(),
            creatureFields = CaptureObjectFields(creature),
        }).Cast<object>().ToArray();

        object[] history = CombatManager.Instance.History.Entries
            .Select((entry, index) => new
            {
                index,
                runtimeType = entry.GetType().FullName,
                fields = CaptureObjectFields(entry),
            })
            .Cast<object>()
            .ToArray();
        string exactState = LocalContext.GetMe(state)?.PlayerCombatState == null
            ? string.Empty
            : ContinuationStamp.CaptureLive(state).StateText;
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            capturedAt = DateTimeOffset.Now,
            restorableScope = "mid_combat_checkpoint",
            encounterId = state.Encounter?.Id.Entry,
            encounterType = state.Encounter?.RoomType.ToString(),
            state.RoundNumber,
            currentSide = state.CurrentSide.ToString(),
            state.RunState.AscensionLevel,
            state.RunState.CurrentActIndex,
            state.RunState.ActFloor,
            state.RunState.TotalFloor,
            exactContinuationState = exactState,
            runRng = state.RunState.Rng.ToSerializable(),
            settings = SolverSettings.Current,
            searchProfiles = new
            {
                profiles.ShortProfile,
                profiles.DeepProfile,
            },
            actualPotionsUsedThisCombat = CombatManager.Instance.History.Entries
                .Count(entry => entry.GetType().Name == "PotionUsedEntry"),
            resultSummary = result == null ? null : new
            {
                result.StartTurnNumber,
                result.SearchedTurns,
                result.BattleHpLostSoFar,
                result.ProjectedBattleHpLost,
                result.BattlePotionsUsedSoFar,
                plannedPotionCount = result.PotionCount,
                result.TheftPolicy,
                result.OutstandingStolenResource,
                result.CombatEndedTurn,
                result.DeathTurn,
                result.BoundaryReason,
            },
            players,
            creatures,
            history,
            route = DescribeRoute(result),
        }, JsonOptions);
    }

    private static object CaptureCard(CardModel card, int index)
    {
        JsonElement serialized = JsonSerializer.SerializeToElement(
            card.ToSerializable(),
            JsonSerializationUtility.GetTypeInfo<SerializableCard>());
        return new
        {
            index,
            id = card.Id.Entry,
            runtimeType = card.GetType().FullName,
            serialized,
            card.CurrentUpgradeLevel,
            energyCost = new
            {
                card.EnergyCost.Canonical,
                withModifiers = card.EnergyCost.GetWithModifiers(CostModifiers.All),
                card.EnergyCost.CostsX,
                fields = CaptureObjectFields(card.EnergyCost),
            },
            canonicalStarCost = card.CanonicalStarCost,
            starCostWithModifiers = card.GetStarCostWithModifiers(),
            keywords = card.Keywords.Select(keyword => keyword.ToString()).ToArray(),
            affliction = card.Affliction == null ? null : new
            {
                id = card.Affliction.Id.Entry,
                runtimeType = card.Affliction.GetType().FullName,
                card.Affliction.Amount,
                fields = CaptureObjectFields(card.Affliction),
            },
            enchantment = card.Enchantment == null ? null : new
            {
                id = card.Enchantment.Id.Entry,
                runtimeType = card.Enchantment.GetType().FullName,
                card.Enchantment.Amount,
                status = card.Enchantment.Status.ToString(),
                fields = CaptureObjectFields(card.Enchantment),
            },
            dynamicVars = card.DynamicVars.OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(
                    item => item.Key,
                    item => (object)new
                    {
                        runtimeType = item.Value.GetType().FullName,
                        item.Value.BaseValue,
                        item.Value.IntValue,
                    },
                    StringComparer.Ordinal),
            fields = CaptureObjectFields(card),
        };
    }

    private static SortedDictionary<string, object?> CaptureObjectFields(object source)
    {
        SortedDictionary<string, object?> fields = new(StringComparer.Ordinal);
        HashSet<object> visited = new(ReferenceEqualityComparer.Instance) { source };
        for (Type? type = source.GetType(); type != null && type != typeof(object); type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.IsStatic
                    || typeof(Delegate).IsAssignableFrom(field.FieldType)
                    || typeof(GodotObject).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }
                fields[$"{type.Name}.{field.Name}"] = SnapshotFieldValue(
                    field.GetValue(source),
                    depth: 0,
                    visited);
            }
        }
        return fields;
    }

    private static object? SnapshotFieldValue(object? value, int depth, HashSet<object> visited)
    {
        if (value == null)
            return null;
        Type type = value.GetType();
        if (type.IsPrimitive || value is decimal or string or DateTime or DateTimeOffset or Guid)
            return value;
        if (value is Enum or Type)
            return value.ToString();
        if (value is ModelId modelId)
            return modelId.ToString();
        if (value is Creature creature)
        {
            return new
            {
                creature.CombatId,
                monsterId = creature.Monster?.Id.Entry,
                playerNetId = creature.Player?.NetId,
            };
        }
        if (value is Player player)
            return new { player.NetId, characterId = player.Character.Id.Entry };
        if (value is AbstractModel model)
            return new { id = model.Id.Entry, runtimeType = model.GetType().FullName };
        if (value is IDictionary dictionary)
        {
            List<object?> entries = [];
            int count = 0;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (count++ >= 256)
                    break;
                entries.Add(new
                {
                    key = SnapshotFieldValue(entry.Key, depth + 1, visited),
                    value = SnapshotFieldValue(entry.Value, depth + 1, visited),
                });
            }
            return entries;
        }
        if (value is IEnumerable enumerable && value is not string)
        {
            List<object?> items = [];
            int count = 0;
            foreach (object? item in enumerable)
            {
                if (count++ >= 256)
                    break;
                items.Add(SnapshotFieldValue(item, depth + 1, visited));
            }
            return items;
        }
        if (depth >= 2 || !visited.Add(value))
            return value.ToString();
        SortedDictionary<string, object?> nested = new(StringComparer.Ordinal);
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.IsStatic
                || typeof(Delegate).IsAssignableFrom(field.FieldType)
                || typeof(GodotObject).IsAssignableFrom(field.FieldType))
            {
                continue;
            }
            nested[field.Name] = SnapshotFieldValue(field.GetValue(value), depth + 1, visited);
        }
        return nested;
    }

    private static byte[] CaptureNativeCombatState(CombatState state)
    {
        NetFullCombatState native = NetFullCombatState.FromRun(state.RunState, justFinishedAction: null);
        PacketWriter writer = new() { WarnOnGrow = false };
        native.Serialize(writer);
        writer.ZeroByteRemainder();
        return writer.Buffer.AsSpan(0, writer.BytePosition).ToArray();
    }

    private static void TryCapturePreCombatFiles(ForensicSession session, CombatState state)
    {
        if (session.PreCombatRunSave != null || session.PreCombatDiskCaptureAttempts >= 3)
            return;
        session.PreCombatDiskCaptureAttempts++;
        string userDataDirectory = OS.GetUserDataDir();
        string? best = null;
        DateTime bestWrite = DateTime.MinValue;
        foreach (string file in Directory.EnumerateFiles(
                     userDataDirectory,
                     "current_run.save",
                     SearchOption.AllDirectories))
        {
            try
            {
                byte[] bytes = ReadSharedFile(file, MaximumCapturedSaveBytes);
                using JsonDocument document = JsonDocument.Parse(bytes);
                if (!document.RootElement.TryGetProperty("rng", out JsonElement rng)
                    || !rng.TryGetProperty("seed", out JsonElement seed)
                    || seed.GetString() != state.RunState.Rng.StringSeed)
                {
                    continue;
                }
                DateTime write = File.GetLastWriteTimeUtc(file);
                if (write <= bestWrite)
                    continue;
                best = file;
                bestWrite = write;
            }
            catch (IOException)
            {
            }
            catch (JsonException)
            {
            }
        }
        if (best == null)
            return;

        session.PreCombatRunSave = new CapturedFile(
            NormalizeEntryPath(Path.GetRelativePath(userDataDirectory, best)),
            ReadSharedFile(best, MaximumCapturedSaveBytes));
        string progress = Path.Combine(Path.GetDirectoryName(best)!, "progress.save");
        if (File.Exists(progress))
        {
            session.PreCombatProgressSave = new CapturedFile(
                NormalizeEntryPath(Path.GetRelativePath(userDataDirectory, progress)),
                ReadSharedFile(progress, MaximumCapturedSaveBytes));
        }
    }

    private static void CaptureInMemoryRunSave(ForensicSession session)
    {
        if (!RunManager.Instance.IsInProgress)
            return;
        session.InMemoryRunSave = new CapturedFile("in-memory", CaptureInMemoryRunSaveBytes());
    }

    private static byte[] CaptureInMemoryRunSaveBytes()
    {
        SerializableRun save = RunManager.Instance.ToSave(null);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            save,
            JsonSerializationUtility.GetTypeInfo<SerializableRun>());
        if (bytes.LongLength > MaximumCapturedSaveBytes)
        {
            throw new InvalidDataException(
                $"内存跑局快照超过上限：{bytes.LongLength} bytes。");
        }
        return bytes;
    }

    private static byte[] ReadSharedFile(string path, long maximumBytes)
    {
        using FileStream input = new(
            path,
            FileMode.Open,
            System.IO.FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (input.Length > maximumBytes)
            throw new InvalidDataException($"取证文件超过上限：{path} ({input.Length} bytes)。");
        using MemoryStream output = new((int)input.Length);
        input.CopyTo(output);
        return output.ToArray();
    }

    private static void CaptureLogStarts(ForensicSession session)
    {
        string logsDirectory = Path.Combine(OS.GetUserDataDir(), "logs");
        if (!Directory.Exists(logsDirectory))
            return;
        foreach (string log in Directory.EnumerateFiles(logsDirectory, "*.log", SearchOption.AllDirectories))
            session.LogStartOffsets[log] = new FileInfo(log).Length;
    }

    private static void CaptureLogEnds(ForensicSession session)
    {
        foreach (string log in session.LogStartOffsets.Keys)
        {
            if (File.Exists(log))
                session.LogEndOffsets[log] = new FileInfo(log).Length;
        }
    }

    private static void AddText(ZipArchive archive, string name, string text)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using Stream output = entry.Open();
        using StreamWriter writer = new(output, new UTF8Encoding(false));
        writer.Write(text);
    }

    private static void AddFile(ZipArchive archive, string path, string entryName)
    {
        using FileStream input = new(
            path,
            FileMode.Open,
            System.IO.FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        AddStream(archive, entryName, input);
    }

    private static void AddFileTail(
        ZipArchive archive,
        string path,
        string entryName,
        long maximumBytes)
    {
        using FileStream input = new(
            path,
            FileMode.Open,
            System.IO.FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (input.Length > maximumBytes)
            input.Seek(-maximumBytes, SeekOrigin.End);
        AddStream(archive, entryName, input);
    }

    private static void AddFileRange(
        ZipArchive archive,
        string path,
        string entryName,
        long start,
        long end,
        long maximumBytes)
    {
        if (!File.Exists(path) || end <= start)
            return;
        using FileStream input = new(
            path,
            FileMode.Open,
            System.IO.FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        end = Math.Min(end, input.Length);
        start = Math.Clamp(start, 0, end);
        if (end - start > maximumBytes)
            start = end - maximumBytes;
        input.Seek(start, SeekOrigin.Begin);
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using Stream output = entry.Open();
        byte[] buffer = new byte[81920];
        long remaining = end - start;
        while (remaining > 0)
        {
            int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read <= 0)
                break;
            output.Write(buffer, 0, read);
            remaining -= read;
        }
    }

    private static void AddBytes(ZipArchive archive, string entryName, byte[] bytes)
    {
        using MemoryStream input = new(bytes, writable: false);
        AddStream(archive, entryName, input);
    }

    private static void AddStream(ZipArchive archive, string entryName, Stream input)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using Stream output = entry.Open();
        input.CopyTo(output);
    }

    private static string DefaultExportDirectory()
    {
        string desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktop))
            throw new DirectoryNotFoundException("无法定位桌面目录。");
        return Path.Combine(desktop, ExportFolderName);
    }

    private static string NormalizeEntryPath(string path)
        => path.Replace(Path.DirectorySeparatorChar, '/');

    private static string SanitizeFileName(string value)
    {
        HashSet<char> invalid = Path.GetInvalidFileNameChars().ToHashSet();
        string sanitized = new(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}
