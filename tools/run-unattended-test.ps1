#requires -Version 7.0

param(
    [string]$ScenarioId = "SMOKE-001",
    [string]$CharacterId = "IRONCLAD",
    [string]$Seed = "COMBATSOLVER",
    [string]$EncounterId = "FUZZY_WURM_CRAWLER_WEAK",
    [string]$RunSnapshotPath = "",
    [string]$ProgressSnapshotPath = "",
    [ValidateRange(0, 10)]
    [int]$Ascension = 0,
    [int]$ActIndexForTest = 0,
    [switch]$MarkEncounterAsSecondBossForTest,
    [int]$EnemyCurrentHp = 1,
    [string]$InitialEnemyCurrentHpsJson = "",
    [int]$InitialPlayerHp = -1,
    [int]$InitialPlayerMaxHp = -1,
    [int]$InitialPlayerBlock = -1,
    [int]$InitialPlayerEnergy = -1,
    [int]$InitialPlayerStars = -1,
    [int]$InitialRoundNumber = -1,
    [int]$InitialPlayerTurnNumber = -1,
    [string]$InitialEnemyStateLogsJson = "",
    [switch]$ReloadRunRngAfterStateInjection,
    [string]$CardId = "STRIKE_IRONCLAD",
    [string]$PowerId = "",
    [string]$PowersJson = "",
    [string]$PowersPath = "",
    [int]$PowerAmount = 1,
    [ValidateSet("Player", "Enemy")]
    [string]$PowerTarget = "Enemy",
    [string]$MonsterMoveId = "",
    [string]$MonsterId = "",
    [int]$ExpectedPlayerHpLoss = -1,
    [int]$ExpectedEnemyBlockGain = -1,
    [string]$ExpectedPlayerPowersJson = "{}",
    [string]$ExpectedEnemyPowersJson = "{}",
    [string]$MonsterMoveChecksJson = "",
    [string]$MonsterMoveChecksPath = "",
    [string]$OrbsJson = "",
    [string]$OrbChecksJson = "",
    [string]$OrbChecksPath = "",
    [string]$RelicsJson = "",
    [string]$RelicsPath = "",
    [string]$CombatRelicsJson = "",
    [string]$CombatRelicsPath = "",
    [string]$CardsJson = "",
    [string]$CardsPath = "",
    [string]$RunCardsJson = "",
    [string]$RunCardsPath = "",
    [string]$PotionCheckJson = "",
    [string]$PotionCheckPath = "",
    [string]$PotionChecksJson = "",
    [string]$PotionChecksPath = "",
    [string]$PotionId = "",
    [string]$PotionsJson = "",
    [string]$PotionsPath = "",
    [string[]]$ModifierId = @(),
    [string[]]$AdditionalMonsterId = @(),
    [string]$InitialEnemyMoveIdsJson = "",
    [int]$ExpectedFinishedTurn = 0,
    [int]$ExpectedFinishedTurnAtMost = 0,
    [int]$ExpectedFinishedPlayerHpAtLeast = -1,
    [switch]$ClearPlayerHand,
    [switch]$ClearPlayerPiles,
    [switch]$ClearAllPowers,
    [switch]$VerifyPredictionFailureBoundaries,
    [switch]$VerifySearchPolicySnapshot,
    [switch]$VerifyControllerSessionLifecycle,
    [switch]$VerifyForkBoundaries,
    [switch]$VerifyCombatRootSnapshot,
    [switch]$VerifyBaseLibCardModifierBoundary,
    [switch]$StopAfterCombatRootSnapshotAssertion,
    [switch]$VerifyIncrementalSearch,
    [switch]$ForceShortSearchOnly,
    [switch]$MeasureSearchPhases,
    [switch]$HoldAfterInitialSearch,
    [int]$ShortSearchBudgetOverrideMilliseconds = -1,
    [int]$DeepSearchBudgetOverrideMilliseconds = -1,
    [ValidateSet("", "Short", "Deep")]
    [string]$ExpectedInitialSearchPhase = "",
    [ValidateSet(-1, 0, 1)]
    [int]$ExpectedInitialDeepSearchTriggered = -1,
    [ValidateSet(-1, 0, 1)]
    [int]$ExpectedInitialDeepSearchImprovedResult = -1,
    [double]$ExpectedInitialTotalElapsedMillisecondsAtMost = -1,
    [long]$ExpectedInitialTotalAllocatedBytesAtMost = -1,
    [int]$ExpectedInitialGen2CollectionsAtMost = -1,
    [double]$ExpectedInitialTotalGcPauseMillisecondsAtMost = -1,
    [double]$ExpectedInitialMaxGcPauseMillisecondsAtMost = -1,
    [double]$ExpectedInitialMaxMainThreadFrameGapMillisecondsAtMost = -1,
    [int]$ExpectedInitialMainThreadFramesOver50MillisecondsAtMost = -1,
    [int]$ExpectedInitialMainThreadFramesOver100MillisecondsAtMost = -1,
    [int]$ExpectedInitialTransitionCacheHitsAtLeast = -1,
    [int]$ExpectedInitialRepeatableNoProgressBranchesPrunedAtLeast = -1,
    [int]$ExpectedInitialChoiceBranchesEvaluatedAtLeast = -1,
    [int]$ExpectedInitialExecutableActionCountAtLeast = -1,
    [int]$ExpectedInitialSoldHp = -1,
    [int]$ExpectedInitialSoldHpAtMost = -1,
    [int]$ExpectedInitialSoldHpBranchesPrunedAtLeast = -1,
    [int]$ExpectedInitialPotionCount = -1,
    [int]$ExpectedInitialPotionHpSavedAtLeast = -1,
    [int]$ExpectedInitialPotionBranchesRejectedAtLeast = -1,
    [ValidateSet("", "PreserveResources", "LetEscape")]
    [string]$ExpectedInitialTheftPolicy = "",
    [int]$ExpectedInitialOutstandingStolenResource = -1,
    [int]$ExpectedInitialSearchedTurnsAtLeast = -1,
    [int]$ExpectedInitialShufflesCrossedAtLeast = -1,
    [int]$ExpectedInitialUnmirroredCount = -1,
    [int]$ExpectedInitialHpLostAtMost = -1,
    [int]$ExpectedInitialProjectedBattleHpLost = -1,
    [int]$ExpectedInitialProjectedBattleHpLostAtMost = -1,
    [int]$ExpectedInitialLongTermResourceValueAtLeast = -1,
    [int]$ExpectedInitialFinalMaxHp = -1,
    [int]$ExpectedInitialMaxBlockAtLeast = -1,
    [int]$ExpectedInitialActualBlockAtLeast = -1,
    [string]$ExpectedInitialActionCardId = "",
    [string]$ExpectedInitialAbsentActionCardId = "",
    [string]$ExpectedInitialFirstActionCardId = "",
    [string]$ExpectedInitialFirstActionPotionId = "",
    [string]$ExpectedInitialActionTitle = "",
    [int]$ExpectedInitialActionReplayCount = -1,
    [ValidateSet(-1, 0, 1)]
    [int]$ExpectedInitialOnlyDeathRoutesFound = -1,
    [int]$ExpectedInitialCombatEndedTurn = 0,
    [int]$ExpectedInitialDeathTurn = 0,
    [ValidateSet(-1, 0, 1)]
    [int]$ExpectedInitialActEndingBoss = -1,
    [string]$ExpectedInitialPlannedChoiceCardId = "",
    [int]$ExpectedInitialTurnStartChoiceTurn = 0,
    [string]$ExpectedInitialTurnStartChoiceSourceId = "",
    [string]$ExpectedInitialTurnStartChoiceCardId = "",
    [string]$ExpectedInitialTurnStartChoiceStateContains = "",
    [string]$ExpectedInitialTurnStartChoiceStateExcludes = "",
    [int]$ExpectedInitialSetupChoiceCountAtLeast = -1,
    [string]$ExpectedInitialSetupChoiceSourceId = "",
    [string]$ExpectedInitialSetupChoiceTextStartsWith = "",
    [switch]$StopAfterInitialSetupAssertion,
    [switch]$StopAfterInitialSolverResultAssertion,
    [switch]$ExpectedFullAutoPausedAtDeathTurn,
    [switch]$ExpectedFullAutoPausedAfterWorseRecalculation,
    [switch]$ExpectedFullAutoPausedAtLiveRisk,
    [switch]$EnableStopOnWorseRecalculationForTest,
    [string]$ExpectedInitialRelicEffectId = "",
    [string]$ExpectedInitialRelicEffectSummary = "",
    [int]$ExpectedReusedTurn = 0,
    [int]$ExpectedReusedProjectedBattleHpLost = -1,
    [int]$ExpectedUnexpectedReplansAtMost = -1,
    [switch]$StopAfterExpectedReuse,
    [string]$ExpectedPlayedCardId = "",
    [string]$ExpectedUsedPotionId = "",
    [string]$ExpectedObservedPlayerPowerId = "",
    [string]$ExpectedNativeChoiceOwnerPrefix = "",
    [ValidateSet("", "ChooseCard", "SimpleGrid", "CombatPile", "Hand", "HandUpgrade")]
    [string]$ExpectedNativeChoiceSurface = "",
    [int]$ExpectedNativeChoiceVisibleAtLeast = -1,
    [int]$ExpectedNativeChoiceSearchStartedAtMost = -1,
    [switch]$StopAfterExpectedPlayerPower,
    [switch]$ExpectedPlayerDeath,
    [ValidateSet("", "FollowGame", "Normal", "Fast", "Instant")]
    [string]$DeploymentFastModeForTest = "",
    [ValidateSet("", "Low", "Medium", "High", "VeryHigh", "Custom")]
    [string]$PerformancePresetForTest = "",
    [int]$ShortMaxCardBranchesPerNodeForTest = -1,
    [int]$DeepMaxCardBranchesPerNodeForTest = -1,
    [ValidateSet("", "Disabled", "Smart", "RequireAtLeastOne")]
    [string]$PotionPolicyForTest = "",
    [ValidateSet("", "PreserveResources", "LetEscape")]
    [string]$TheftPolicyForTest = "",
    [double]$NoGcRegionBudgetGigabytesForTest = -1,
    [double]$DeploymentInterActionDelaySecondsForTest = -1,
    [switch]$AssertDeploymentSpeedRestored,
    [switch]$ExportBugReportAfterSetup,
    [switch]$ExportBugReportAfterCombat,
    [ValidateSet(-1, 0, 1)]
    [int]$EnableDetailedDiagnosticLogsForTest = -1,
    [switch]$ManualEndTurnAfterInitialSearch,
    [switch]$SingleStepAfterInitialSearch,
    [ValidateSet("", "ExecuteCurrentTurn", "FullAuto")]
    [string]$SingleStepResumeModeForTest = "",
    [int]$ExpectedTurnSetupToDeploymentDelayMillisecondsAtLeast = -1,
    [switch]$EnableFullAutoAfterManualEndTurn,
    [int]$ExpectedManualDivergencesAtLeast = -1,
    [int]$ExpectedUnexpectedReplansAtLeast = -1,
    [switch]$StopAfterExpectedUnexpectedReplan,
    [switch]$ExpectedUnexpectedReplanWarning,
    [switch]$ExportBugReportAfterUnexpectedReplan,
    [ValidateSet("", "solver_only", "manual_plus_solver")]
    [string]$ExpectedBugReportControlMode = "",
    [int]$ExpectedNoGcRegionRolloversAtLeast = -1,
    [int]$InjectPlayerHpLossBeforeAutoSearchTurn = 0,
    [int]$InjectPlayerHpLossAmount = 0,
    [int]$ClearPlayerBlockBeforeEndTurnForTest = 0,
    [int]$TimeoutSeconds = 150,
    [switch]$KeepGameOpen,
    [switch]$ExitOnComplete
)

$ErrorActionPreference = "Stop"
$gameExe = "D:\Steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe"
$gameRoot = Split-Path -Parent $gameExe
$gameModsRoot = Join-Path $gameRoot "mods"
$ritsuWorkshopRoot = "D:\Steam\steamapps\workshop\content\2868840\3747602295"
$ritsuVariantDll = Join-Path $ritsuWorkshopRoot "lib\0.111.0\STS2-RitsuLib.dll"
$ritsuManifestSource = Join-Path $ritsuWorkshopRoot "mod_manifest.json"
$headlessDependencyDir = Join-Path $gameModsRoot ".combatsolver-headless-ritsulib"
$headlessDependencyMarker = Join-Path $headlessDependencyDir ".combatsolver-headless-only"
$interactiveDataDir = Join-Path ([Environment]::GetFolderPath("ApplicationData")) "SlayTheSpire2"
$headlessRoot = Join-Path ([Environment]::GetFolderPath("LocalApplicationData")) "CombatSolver\headless-runtime"
$headlessRoaming = Join-Path $headlessRoot "Roaming"
$headlessLocal = Join-Path $headlessRoot "Local"
$dataDir = Join-Path $headlessRoaming "SlayTheSpire2"
$processMarkerPath = Join-Path $headlessRoot "process.json"
$holdReleasePath = Join-Path $headlessRoot "release-held-search"
$headlessLogPath = Join-Path $headlessRoot "godot-headless.log"
$requestPath = Join-Path $dataDir "combat_solver_test_request.json"
$resultPath = Join-Path $dataDir "combat_solver_test_result.json"

if (-not (Test-Path -LiteralPath $gameExe -PathType Leaf)) {
    throw "Game executable not found: $gameExe"
}
if (-not (Test-Path -LiteralPath $ritsuVariantDll -PathType Leaf) -or
    -not (Test-Path -LiteralPath $ritsuManifestSource -PathType Leaf)) {
    throw "Headless RitsuLib source not found under: $ritsuWorkshopRoot"
}
if ($KeepGameOpen.IsPresent -and $ExitOnComplete.IsPresent) {
    throw "KeepGameOpen and ExitOnComplete cannot be used together."
}
if ($HoldAfterInitialSearch.IsPresent -and -not $KeepGameOpen.IsPresent) {
    throw "HoldAfterInitialSearch requires KeepGameOpen so the profiler can attach to the held combat."
}
if ($StopAfterExpectedReuse.IsPresent -and $ExpectedReusedTurn -le 0) {
    throw "StopAfterExpectedReuse requires ExpectedReusedTurn."
}
if ($StopAfterExpectedPlayerPower.IsPresent -and [string]::IsNullOrWhiteSpace($ExpectedObservedPlayerPowerId)) {
    throw "StopAfterExpectedPlayerPower requires ExpectedObservedPlayerPowerId."
}
if ($HoldAfterInitialSearch.IsPresent -and (Test-Path -LiteralPath $holdReleasePath -PathType Leaf)) {
    Remove-Item -LiteralPath $holdReleasePath -Force
}

function Assert-HeadlessDependencyPath {
    $modsRootFull = [IO.Path]::GetFullPath($gameModsRoot).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $dependencyFull = [IO.Path]::GetFullPath($headlessDependencyDir)
    if (-not $dependencyFull.StartsWith($modsRootFull, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to manage a headless dependency outside the game mods directory: $dependencyFull"
    }
}

function Install-HeadlessDependency {
    Assert-HeadlessDependencyPath
    if (Test-Path -LiteralPath $headlessDependencyDir -PathType Container) {
        if (-not (Test-Path -LiteralPath $headlessDependencyMarker -PathType Leaf)) {
            throw "Headless dependency target already exists without the ownership marker: $headlessDependencyDir"
        }
        Remove-Item -LiteralPath $headlessDependencyDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $headlessDependencyDir -Force | Out-Null
    Copy-Item -LiteralPath $ritsuVariantDll -Destination (Join-Path $headlessDependencyDir "STS2-RitsuLib.dll")
    Copy-Item -LiteralPath $ritsuManifestSource -Destination (Join-Path $headlessDependencyDir "STS2-RitsuLib.json")
    Set-Content -LiteralPath $headlessDependencyMarker -Value "CombatSolver isolated headless dependency" -Encoding UTF8
}

function Remove-HeadlessDependency {
    Assert-HeadlessDependencyPath
    if (-not (Test-Path -LiteralPath $headlessDependencyDir -PathType Container)) {
        return
    }
    if (-not (Test-Path -LiteralPath $headlessDependencyMarker -PathType Leaf)) {
        throw "Refusing to remove a headless dependency without the ownership marker: $headlessDependencyDir"
    }
    Remove-Item -LiteralPath $headlessDependencyDir -Recurse -Force
}

function Stop-TestProcessAndRemoveDependency([Diagnostics.Process]$TestProcess) {
    if ($null -ne $TestProcess) {
        $TestProcess.Refresh()
        if (-not $TestProcess.HasExited) {
            Stop-Process -Id $TestProcess.Id
            $TestProcess.WaitForExit(10000) | Out-Null
        }
    }
    Remove-HeadlessDependency
}

New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
New-Item -ItemType Directory -Path $headlessLocal -Force | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $dataDir "default") -PathType Container)) {
    foreach ($directory in @("default", "ModConfig", "mod_configs")) {
        $source = Join-Path $interactiveDataDir $directory
        if (Test-Path -LiteralPath $source -PathType Container) {
            Copy-Item -LiteralPath $source -Destination $dataDir -Recurse -Force
        }
    }
    $sourceModConfig = Join-Path $interactiveDataDir "mods\config"
    if (Test-Path -LiteralPath $sourceModConfig -PathType Container) {
        $targetMods = Join-Path $dataDir "mods"
        New-Item -ItemType Directory -Path $targetMods -Force | Out-Null
        Copy-Item -LiteralPath $sourceModConfig -Destination $targetMods -Recurse -Force
    }
}
$settingsPath = Join-Path $dataDir "default\1\settings.save"
if (-not (Test-Path -LiteralPath $settingsPath -PathType Leaf)) {
    throw "Headless settings save not found after profile initialization: $settingsPath"
}
$headlessSettings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
$headlessSettings.mod_settings = [ordered]@{
    mods_enabled = $true
    mod_list = @()
}
$headlessSettings | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
$resolvedProgressSnapshotPath = if ([string]::IsNullOrWhiteSpace($ProgressSnapshotPath)) {
    $null
} else {
    (Resolve-Path -LiteralPath $ProgressSnapshotPath).Path
}
if ($null -ne $resolvedProgressSnapshotPath) {
    $headlessProgressPath = Join-Path $dataDir "default\1\modded\profile1\saves\progress.save"
    $headlessProgressDirectory = Split-Path -Parent $headlessProgressPath
    New-Item -ItemType Directory -Path $headlessProgressDirectory -Force | Out-Null
    Copy-Item -LiteralPath $resolvedProgressSnapshotPath -Destination $headlessProgressPath -Force
}
$resolvedRunSnapshotPath = if ([string]::IsNullOrWhiteSpace($RunSnapshotPath)) {
    $null
} else {
    (Resolve-Path -LiteralPath $RunSnapshotPath).Path
}
$runId = [Guid]::NewGuid().ToString("N")
$cardsExplicitlyConfigured = -not [string]::IsNullOrWhiteSpace($CardsPath) -or
    -not [string]::IsNullOrWhiteSpace($CardsJson)
$request = [ordered]@{
    schemaVersion = 1
    runId = $runId
    scenarioId = $ScenarioId
    characterId = $CharacterId
    encounterId = $EncounterId
    runSnapshotPath = $resolvedRunSnapshotPath
    ascension = $Ascension
    actIndexForTest = $ActIndexForTest
    markEncounterAsSecondBossForTest = $MarkEncounterAsSecondBossForTest.IsPresent
    seed = $Seed
    enemyCurrentHp = $EnemyCurrentHp
    initialPlayerHp = if ($InitialPlayerHp -gt 0) { $InitialPlayerHp } else { $null }
    initialPlayerMaxHp = if ($InitialPlayerMaxHp -gt 0) { $InitialPlayerMaxHp } else { $null }
    initialPlayerBlock = if ($InitialPlayerBlock -ge 0) { $InitialPlayerBlock } else { $null }
    initialPlayerEnergy = if ($InitialPlayerEnergy -ge 0) { $InitialPlayerEnergy } else { $null }
    initialPlayerStars = if ($InitialPlayerStars -ge 0) { $InitialPlayerStars } else { $null }
    initialRoundNumber = if ($InitialRoundNumber -ge 0) { $InitialRoundNumber } else { $null }
    initialPlayerTurnNumber = if ($InitialPlayerTurnNumber -ge 0) { $InitialPlayerTurnNumber } else { $null }
    initialEnemyStateLogs = if ([string]::IsNullOrWhiteSpace($InitialEnemyStateLogsJson)) { @() } else { @($InitialEnemyStateLogsJson | ConvertFrom-Json -NoEnumerate) }
    reloadRunRngAfterStateInjection = $ReloadRunRngAfterStateInjection.IsPresent
    cards = @()
    runCards = @()
    powers = @()
    orbs = @()
    relics = @()
    combatRelics = @()
    potions = @()
    orbChecks = @()
    potionCheck = $null
    potionChecks = @()
    monsterMoveCheck = $null
    monsterMoveChecks = @()
    modifierIds = @($ModifierId)
    additionalMonsterIds = @($AdditionalMonsterId)
    initialEnemyMoveIds = @()
    timeoutSeconds = $TimeoutSeconds
    expectedFinishedTurn = if ($ExpectedFinishedTurn -gt 0) { $ExpectedFinishedTurn } else { $null }
    expectedFinishedTurnAtMost = if ($ExpectedFinishedTurnAtMost -gt 0) { $ExpectedFinishedTurnAtMost } else { $null }
    expectedFinishedPlayerHpAtLeast = if ($ExpectedFinishedPlayerHpAtLeast -ge 0) { $ExpectedFinishedPlayerHpAtLeast } else { $null }
    clearPlayerHand = $ClearPlayerHand.IsPresent
    clearPlayerPiles = $ClearPlayerPiles.IsPresent
    clearAllPowers = $ClearAllPowers.IsPresent
    verifyPredictionFailureBoundaries = $VerifyPredictionFailureBoundaries.IsPresent
    verifySearchPolicySnapshot = $VerifySearchPolicySnapshot.IsPresent
    verifyControllerSessionLifecycle = $VerifyControllerSessionLifecycle.IsPresent
    verifyForkBoundaries = $VerifyForkBoundaries.IsPresent
    verifyCombatRootSnapshot = $VerifyCombatRootSnapshot.IsPresent
    verifyBaseLibCardModifierBoundary = $VerifyBaseLibCardModifierBoundary.IsPresent
    stopAfterCombatRootSnapshotAssertion = $StopAfterCombatRootSnapshotAssertion.IsPresent
    verifyIncrementalSearch = $VerifyIncrementalSearch.IsPresent
    forceShortSearchOnly = $ForceShortSearchOnly.IsPresent
    measureSearchPhases = $MeasureSearchPhases.IsPresent
    holdAfterInitialSearch = $HoldAfterInitialSearch.IsPresent
    shortSearchBudgetOverrideMilliseconds = if ($ShortSearchBudgetOverrideMilliseconds -gt 0) { $ShortSearchBudgetOverrideMilliseconds } else { $null }
    deepSearchBudgetOverrideMilliseconds = if ($DeepSearchBudgetOverrideMilliseconds -gt 0) { $DeepSearchBudgetOverrideMilliseconds } else { $null }
    expectedInitialSearchPhase = if ([string]::IsNullOrWhiteSpace($ExpectedInitialSearchPhase)) { $null } else { $ExpectedInitialSearchPhase }
    expectedInitialDeepSearchTriggered = if ($ExpectedInitialDeepSearchTriggered -ge 0) { [bool]$ExpectedInitialDeepSearchTriggered } else { $null }
    expectedInitialDeepSearchImprovedResult = if ($ExpectedInitialDeepSearchImprovedResult -ge 0) { [bool]$ExpectedInitialDeepSearchImprovedResult } else { $null }
    expectedInitialTotalElapsedMillisecondsAtMost = if ($ExpectedInitialTotalElapsedMillisecondsAtMost -ge 0) { $ExpectedInitialTotalElapsedMillisecondsAtMost } else { $null }
    expectedInitialTotalAllocatedBytesAtMost = if ($ExpectedInitialTotalAllocatedBytesAtMost -ge 0) { $ExpectedInitialTotalAllocatedBytesAtMost } else { $null }
    expectedInitialGen2CollectionsAtMost = if ($ExpectedInitialGen2CollectionsAtMost -ge 0) { $ExpectedInitialGen2CollectionsAtMost } else { $null }
    expectedInitialTotalGcPauseMillisecondsAtMost = if ($ExpectedInitialTotalGcPauseMillisecondsAtMost -ge 0) { $ExpectedInitialTotalGcPauseMillisecondsAtMost } else { $null }
    expectedInitialMaxGcPauseMillisecondsAtMost = if ($ExpectedInitialMaxGcPauseMillisecondsAtMost -ge 0) { $ExpectedInitialMaxGcPauseMillisecondsAtMost } else { $null }
    expectedInitialMaxMainThreadFrameGapMillisecondsAtMost = if ($ExpectedInitialMaxMainThreadFrameGapMillisecondsAtMost -ge 0) { $ExpectedInitialMaxMainThreadFrameGapMillisecondsAtMost } else { $null }
    expectedInitialMainThreadFramesOver50MillisecondsAtMost = if ($ExpectedInitialMainThreadFramesOver50MillisecondsAtMost -ge 0) { $ExpectedInitialMainThreadFramesOver50MillisecondsAtMost } else { $null }
    expectedInitialMainThreadFramesOver100MillisecondsAtMost = if ($ExpectedInitialMainThreadFramesOver100MillisecondsAtMost -ge 0) { $ExpectedInitialMainThreadFramesOver100MillisecondsAtMost } else { $null }
    expectedInitialTransitionCacheHitsAtLeast = if ($ExpectedInitialTransitionCacheHitsAtLeast -ge 0) { $ExpectedInitialTransitionCacheHitsAtLeast } else { $null }
    expectedInitialRepeatableNoProgressBranchesPrunedAtLeast = if ($ExpectedInitialRepeatableNoProgressBranchesPrunedAtLeast -ge 0) { $ExpectedInitialRepeatableNoProgressBranchesPrunedAtLeast } else { $null }
    expectedInitialChoiceBranchesEvaluatedAtLeast = if ($ExpectedInitialChoiceBranchesEvaluatedAtLeast -ge 0) { $ExpectedInitialChoiceBranchesEvaluatedAtLeast } else { $null }
    expectedInitialExecutableActionCountAtLeast = if ($ExpectedInitialExecutableActionCountAtLeast -ge 0) { $ExpectedInitialExecutableActionCountAtLeast } else { $null }
    expectedInitialSoldHp = if ($ExpectedInitialSoldHp -ge 0) { $ExpectedInitialSoldHp } else { $null }
    expectedInitialSoldHpAtMost = if ($ExpectedInitialSoldHpAtMost -ge 0) { $ExpectedInitialSoldHpAtMost } else { $null }
    expectedInitialSoldHpBranchesPrunedAtLeast = if ($ExpectedInitialSoldHpBranchesPrunedAtLeast -ge 0) { $ExpectedInitialSoldHpBranchesPrunedAtLeast } else { $null }
    expectedInitialPotionCount = if ($ExpectedInitialPotionCount -ge 0) { $ExpectedInitialPotionCount } else { $null }
    expectedInitialPotionHpSavedAtLeast = if ($ExpectedInitialPotionHpSavedAtLeast -ge 0) { $ExpectedInitialPotionHpSavedAtLeast } else { $null }
    expectedInitialPotionBranchesRejectedAtLeast = if ($ExpectedInitialPotionBranchesRejectedAtLeast -ge 0) { $ExpectedInitialPotionBranchesRejectedAtLeast } else { $null }
    expectedInitialTheftPolicy = if ([string]::IsNullOrWhiteSpace($ExpectedInitialTheftPolicy)) { $null } else { $ExpectedInitialTheftPolicy }
    expectedInitialOutstandingStolenResource = if ($ExpectedInitialOutstandingStolenResource -ge 0) { $ExpectedInitialOutstandingStolenResource } else { $null }
    expectedInitialSearchedTurnsAtLeast = if ($ExpectedInitialSearchedTurnsAtLeast -ge 0) { $ExpectedInitialSearchedTurnsAtLeast } else { $null }
    expectedInitialShufflesCrossedAtLeast = if ($ExpectedInitialShufflesCrossedAtLeast -ge 0) { $ExpectedInitialShufflesCrossedAtLeast } else { $null }
    expectedInitialUnmirroredCount = if ($ExpectedInitialUnmirroredCount -ge 0) { $ExpectedInitialUnmirroredCount } else { $null }
    expectedInitialHpLostAtMost = if ($ExpectedInitialHpLostAtMost -ge 0) { $ExpectedInitialHpLostAtMost } else { $null }
    expectedInitialProjectedBattleHpLost = if ($ExpectedInitialProjectedBattleHpLost -ge 0) { $ExpectedInitialProjectedBattleHpLost } else { $null }
    expectedInitialProjectedBattleHpLostAtMost = if ($ExpectedInitialProjectedBattleHpLostAtMost -ge 0) { $ExpectedInitialProjectedBattleHpLostAtMost } else { $null }
    expectedInitialLongTermResourceValueAtLeast = if ($ExpectedInitialLongTermResourceValueAtLeast -ge 0) { $ExpectedInitialLongTermResourceValueAtLeast } else { $null }
    expectedInitialFinalMaxHp = if ($ExpectedInitialFinalMaxHp -ge 0) { $ExpectedInitialFinalMaxHp } else { $null }
    expectedInitialMaxBlockAtLeast = if ($ExpectedInitialMaxBlockAtLeast -ge 0) { $ExpectedInitialMaxBlockAtLeast } else { $null }
    expectedInitialActualBlockAtLeast = if ($ExpectedInitialActualBlockAtLeast -ge 0) { $ExpectedInitialActualBlockAtLeast } else { $null }
    expectedInitialActionCardId = if ([string]::IsNullOrWhiteSpace($ExpectedInitialActionCardId)) { $null } else { $ExpectedInitialActionCardId }
    expectedInitialAbsentActionCardId = if ([string]::IsNullOrWhiteSpace($ExpectedInitialAbsentActionCardId)) { $null } else { $ExpectedInitialAbsentActionCardId }
    expectedInitialFirstActionCardId = if ([string]::IsNullOrWhiteSpace($ExpectedInitialFirstActionCardId)) { $null } else { $ExpectedInitialFirstActionCardId }
    expectedInitialFirstActionPotionId = if ([string]::IsNullOrWhiteSpace($ExpectedInitialFirstActionPotionId)) { $null } else { $ExpectedInitialFirstActionPotionId }
    expectedInitialActionTitle = if ([string]::IsNullOrWhiteSpace($ExpectedInitialActionTitle)) { $null } else { $ExpectedInitialActionTitle }
    expectedInitialActionReplayCount = if ($ExpectedInitialActionReplayCount -ge 0) { $ExpectedInitialActionReplayCount } else { $null }
    expectedInitialOnlyDeathRoutesFound = if ($ExpectedInitialOnlyDeathRoutesFound -ge 0) { [bool]$ExpectedInitialOnlyDeathRoutesFound } else { $null }
    expectedInitialCombatEndedTurn = if ($ExpectedInitialCombatEndedTurn -gt 0) { $ExpectedInitialCombatEndedTurn } else { $null }
    expectedInitialDeathTurn = if ($ExpectedInitialDeathTurn -gt 0) { $ExpectedInitialDeathTurn } else { $null }
    expectedInitialActEndingBoss = if ($ExpectedInitialActEndingBoss -ge 0) { [bool]$ExpectedInitialActEndingBoss } else { $null }
    expectedInitialPlannedChoiceCardId = if ([string]::IsNullOrWhiteSpace($ExpectedInitialPlannedChoiceCardId)) { $null } else { $ExpectedInitialPlannedChoiceCardId }
    expectedInitialTurnStartChoiceTurn = if ($ExpectedInitialTurnStartChoiceTurn -gt 0) { $ExpectedInitialTurnStartChoiceTurn } else { $null }
    expectedInitialTurnStartChoiceSourceId = if ([string]::IsNullOrWhiteSpace($ExpectedInitialTurnStartChoiceSourceId)) { $null } else { $ExpectedInitialTurnStartChoiceSourceId }
    expectedInitialTurnStartChoiceCardId = if ([string]::IsNullOrWhiteSpace($ExpectedInitialTurnStartChoiceCardId)) { $null } else { $ExpectedInitialTurnStartChoiceCardId }
    expectedInitialTurnStartChoiceStateContains = if ([string]::IsNullOrWhiteSpace($ExpectedInitialTurnStartChoiceStateContains)) { $null } else { $ExpectedInitialTurnStartChoiceStateContains }
    expectedInitialTurnStartChoiceStateExcludes = if ([string]::IsNullOrWhiteSpace($ExpectedInitialTurnStartChoiceStateExcludes)) { $null } else { $ExpectedInitialTurnStartChoiceStateExcludes }
    expectedInitialSetupChoiceCountAtLeast = if ($ExpectedInitialSetupChoiceCountAtLeast -ge 0) { $ExpectedInitialSetupChoiceCountAtLeast } else { $null }
    expectedInitialSetupChoiceSourceId = if ([string]::IsNullOrWhiteSpace($ExpectedInitialSetupChoiceSourceId)) { $null } else { $ExpectedInitialSetupChoiceSourceId }
    expectedInitialSetupChoiceTextStartsWith = if ([string]::IsNullOrWhiteSpace($ExpectedInitialSetupChoiceTextStartsWith)) { $null } else { $ExpectedInitialSetupChoiceTextStartsWith }
    stopAfterInitialSetupAssertion = $StopAfterInitialSetupAssertion.IsPresent
    stopAfterInitialSolverResultAssertion = $StopAfterInitialSolverResultAssertion.IsPresent
    expectedFullAutoPausedAtDeathTurn = $ExpectedFullAutoPausedAtDeathTurn.IsPresent
    expectedFullAutoPausedAfterWorseRecalculation = $ExpectedFullAutoPausedAfterWorseRecalculation.IsPresent
    expectedFullAutoPausedAtLiveRisk = $ExpectedFullAutoPausedAtLiveRisk.IsPresent
    enableStopOnWorseRecalculationForTest = $EnableStopOnWorseRecalculationForTest.IsPresent
    expectedInitialRelicEffectId = if ([string]::IsNullOrWhiteSpace($ExpectedInitialRelicEffectId)) { $null } else { $ExpectedInitialRelicEffectId }
    expectedInitialRelicEffectSummary = if ([string]::IsNullOrWhiteSpace($ExpectedInitialRelicEffectSummary)) { $null } else { $ExpectedInitialRelicEffectSummary }
    expectedReusedTurn = if ($ExpectedReusedTurn -gt 0) { $ExpectedReusedTurn } else { $null }
    expectedReusedProjectedBattleHpLost = if ($ExpectedReusedProjectedBattleHpLost -ge 0) { $ExpectedReusedProjectedBattleHpLost } else { $null }
    expectedUnexpectedReplansAtMost = if ($ExpectedUnexpectedReplansAtMost -ge 0) { $ExpectedUnexpectedReplansAtMost } else { $null }
    stopAfterExpectedReuse = $StopAfterExpectedReuse.IsPresent
    expectedPlayedCardId = if ([string]::IsNullOrWhiteSpace($ExpectedPlayedCardId)) { $null } else { $ExpectedPlayedCardId }
    expectedUsedPotionId = if ([string]::IsNullOrWhiteSpace($ExpectedUsedPotionId)) { $null } else { $ExpectedUsedPotionId }
    expectedObservedPlayerPowerId = if ([string]::IsNullOrWhiteSpace($ExpectedObservedPlayerPowerId)) { $null } else { $ExpectedObservedPlayerPowerId }
    expectedNativeChoiceOwnerPrefix = if ([string]::IsNullOrWhiteSpace($ExpectedNativeChoiceOwnerPrefix)) { $null } else { $ExpectedNativeChoiceOwnerPrefix }
    expectedNativeChoiceSurface = if ([string]::IsNullOrWhiteSpace($ExpectedNativeChoiceSurface)) { $null } else { $ExpectedNativeChoiceSurface }
    expectedNativeChoiceVisibleAtLeast = if ($ExpectedNativeChoiceVisibleAtLeast -ge 0) { $ExpectedNativeChoiceVisibleAtLeast } else { $null }
    expectedNativeChoiceSearchStartedAtMost = if ($ExpectedNativeChoiceSearchStartedAtMost -ge 0) { $ExpectedNativeChoiceSearchStartedAtMost } else { $null }
    stopAfterExpectedPlayerPower = $StopAfterExpectedPlayerPower.IsPresent
    expectedPlayerDeath = $ExpectedPlayerDeath.IsPresent
    deploymentFastModeForTest = if ([string]::IsNullOrWhiteSpace($DeploymentFastModeForTest)) { $null } else { $DeploymentFastModeForTest }
    performancePresetForTest = if ([string]::IsNullOrWhiteSpace($PerformancePresetForTest)) { $null } else { $PerformancePresetForTest }
    shortMaxCardBranchesPerNodeForTest = if ($ShortMaxCardBranchesPerNodeForTest -gt 0) { $ShortMaxCardBranchesPerNodeForTest } else { $null }
    deepMaxCardBranchesPerNodeForTest = if ($DeepMaxCardBranchesPerNodeForTest -gt 0) { $DeepMaxCardBranchesPerNodeForTest } else { $null }
    potionPolicyForTest = if ([string]::IsNullOrWhiteSpace($PotionPolicyForTest)) { $null } else { $PotionPolicyForTest }
    theftPolicyForTest = if ([string]::IsNullOrWhiteSpace($TheftPolicyForTest)) { $null } else { $TheftPolicyForTest }
    noGcRegionBudgetGigabytesForTest = if ($NoGcRegionBudgetGigabytesForTest -gt 0) { $NoGcRegionBudgetGigabytesForTest } else { $null }
    deploymentInterActionDelaySecondsForTest = if ($DeploymentInterActionDelaySecondsForTest -ge 0) { $DeploymentInterActionDelaySecondsForTest } else { $null }
    assertDeploymentSpeedRestored = $AssertDeploymentSpeedRestored.IsPresent
    exportBugReportAfterSetup = $ExportBugReportAfterSetup.IsPresent
    exportBugReportAfterCombat = $ExportBugReportAfterCombat.IsPresent
    enableDetailedDiagnosticLogsForTest = if ($EnableDetailedDiagnosticLogsForTest -ge 0) { [bool]$EnableDetailedDiagnosticLogsForTest } else { $null }
    manualEndTurnAfterInitialSearch = $ManualEndTurnAfterInitialSearch.IsPresent
    singleStepAfterInitialSearch = $SingleStepAfterInitialSearch.IsPresent
    singleStepResumeModeForTest = if ([string]::IsNullOrWhiteSpace($SingleStepResumeModeForTest)) { $null } else { $SingleStepResumeModeForTest }
    expectedTurnSetupToDeploymentDelayMillisecondsAtLeast = if ($ExpectedTurnSetupToDeploymentDelayMillisecondsAtLeast -ge 0) { $ExpectedTurnSetupToDeploymentDelayMillisecondsAtLeast } else { $null }
    enableFullAutoAfterManualEndTurn = $EnableFullAutoAfterManualEndTurn.IsPresent
    expectedManualDivergencesAtLeast = if ($ExpectedManualDivergencesAtLeast -ge 0) { $ExpectedManualDivergencesAtLeast } else { $null }
    expectedUnexpectedReplansAtLeast = if ($ExpectedUnexpectedReplansAtLeast -ge 0) { $ExpectedUnexpectedReplansAtLeast } else { $null }
    stopAfterExpectedUnexpectedReplan = $StopAfterExpectedUnexpectedReplan.IsPresent
    expectedUnexpectedReplanWarning = $ExpectedUnexpectedReplanWarning.IsPresent
    exportBugReportAfterUnexpectedReplan = $ExportBugReportAfterUnexpectedReplan.IsPresent
    expectedBugReportControlMode = if ([string]::IsNullOrWhiteSpace($ExpectedBugReportControlMode)) { $null } else { $ExpectedBugReportControlMode }
    expectedNoGcRegionRolloversAtLeast = if ($ExpectedNoGcRegionRolloversAtLeast -ge 0) { $ExpectedNoGcRegionRolloversAtLeast } else { $null }
    injectPlayerHpLossBeforeAutoSearchTurn = if ($InjectPlayerHpLossBeforeAutoSearchTurn -gt 0) { $InjectPlayerHpLossBeforeAutoSearchTurn } else { $null }
    injectPlayerHpLossAmount = $InjectPlayerHpLossAmount
    clearPlayerBlockBeforeEndTurnForTest = if ($ClearPlayerBlockBeforeEndTurnForTest -gt 0) { $ClearPlayerBlockBeforeEndTurnForTest } else { $null }
    exitOnComplete = $ExitOnComplete.IsPresent
}
if (-not [string]::IsNullOrWhiteSpace($InitialEnemyCurrentHpsJson)) {
    $request.initialEnemyCurrentHps = @($InitialEnemyCurrentHpsJson | ConvertFrom-Json)
}
if (-not [string]::IsNullOrWhiteSpace($InitialEnemyMoveIdsJson)) {
    $request.initialEnemyMoveIds = @($InitialEnemyMoveIdsJson | ConvertFrom-Json)
}
if (-not [string]::IsNullOrWhiteSpace($OrbsJson)) {
    $request.orbs = @($OrbsJson | ConvertFrom-Json)
}
if (-not [string]::IsNullOrWhiteSpace($PowersPath)) {
    $request.powers = @(Get-Content -LiteralPath $PowersPath -Raw | ConvertFrom-Json)
} elseif (-not [string]::IsNullOrWhiteSpace($PowersJson)) {
    $request.powers = @($PowersJson | ConvertFrom-Json)
}
if (-not [string]::IsNullOrWhiteSpace($RelicsPath)) {
    $request.relics = @(Get-Content -LiteralPath $RelicsPath -Raw | ConvertFrom-Json)
} elseif (-not [string]::IsNullOrWhiteSpace($RelicsJson)) {
    $request.relics = @($RelicsJson | ConvertFrom-Json)
}
if (-not [string]::IsNullOrWhiteSpace($CombatRelicsPath)) {
    $request.combatRelics = @(Get-Content -LiteralPath $CombatRelicsPath -Raw | ConvertFrom-Json)
} elseif (-not [string]::IsNullOrWhiteSpace($CombatRelicsJson)) {
    $request.combatRelics = @($CombatRelicsJson | ConvertFrom-Json)
}
if (-not [string]::IsNullOrWhiteSpace($CardsPath)) {
    $request.cards = @(Get-Content -LiteralPath $CardsPath -Raw | ConvertFrom-Json)
} elseif (-not [string]::IsNullOrWhiteSpace($CardsJson)) {
    $request.cards = @($CardsJson | ConvertFrom-Json)
}
if (-not [string]::IsNullOrWhiteSpace($RunCardsPath)) {
    $request.runCards = @(Get-Content -LiteralPath $RunCardsPath -Raw | ConvertFrom-Json)
} elseif (-not [string]::IsNullOrWhiteSpace($RunCardsJson)) {
    $request.runCards = @($RunCardsJson | ConvertFrom-Json)
}
if (-not [string]::IsNullOrWhiteSpace($PotionsPath)) {
    $request.potions = @(Get-Content -LiteralPath $PotionsPath -Raw | ConvertFrom-Json)
} elseif (-not [string]::IsNullOrWhiteSpace($PotionsJson)) {
    $request.potions = @($PotionsJson | ConvertFrom-Json)
} elseif (-not [string]::IsNullOrWhiteSpace($PotionId)) {
    $request.potions = @([ordered]@{ potionId = $PotionId })
}
if (-not [string]::IsNullOrWhiteSpace($PotionChecksPath)) {
    $request.potionChecks = @(Get-Content -LiteralPath $PotionChecksPath -Raw | ConvertFrom-Json)
} elseif (-not [string]::IsNullOrWhiteSpace($PotionChecksJson)) {
    $request.potionChecks = @($PotionChecksJson | ConvertFrom-Json)
} elseif (-not [string]::IsNullOrWhiteSpace($PotionCheckPath)) {
    $request.potionCheck = Get-Content -LiteralPath $PotionCheckPath -Raw | ConvertFrom-Json
} elseif (-not [string]::IsNullOrWhiteSpace($PotionCheckJson)) {
    $request.potionCheck = $PotionCheckJson | ConvertFrom-Json
} elseif (-not [string]::IsNullOrWhiteSpace($MonsterMoveChecksPath)) {
    $request.monsterMoveChecks = @(Get-Content -LiteralPath $MonsterMoveChecksPath -Raw | ConvertFrom-Json)
} elseif (-not [string]::IsNullOrWhiteSpace($MonsterMoveChecksJson)) {
    $request.monsterMoveChecks = @($MonsterMoveChecksJson | ConvertFrom-Json)
} elseif ([string]::IsNullOrWhiteSpace($MonsterMoveId)) {
    if ($request.cards.Count -eq 0 -and
        -not $cardsExplicitlyConfigured -and
        [string]::IsNullOrWhiteSpace($RunSnapshotPath)) {
        $request.cards = @(
            [ordered]@{
                cardId = $CardId
                pile = "Hand"
                count = 1
                upgradeLevels = 0
            }
        )
    }
} else {
    $expectedPlayerPowers = $ExpectedPlayerPowersJson | ConvertFrom-Json
    $expectedEnemyPowers = $ExpectedEnemyPowersJson | ConvertFrom-Json
    $request.monsterMoveCheck = [ordered]@{
        enemyIndex = 0
        monsterId = $MonsterId
        moveId = $MonsterMoveId
        expectedPlayerHpLoss = if ($ExpectedPlayerHpLoss -ge 0) { $ExpectedPlayerHpLoss } else { $null }
        expectedEnemyBlockGain = if ($ExpectedEnemyBlockGain -ge 0) { $ExpectedEnemyBlockGain } else { $null }
        expectedPlayerPowers = $expectedPlayerPowers
        expectedEnemyPowers = $expectedEnemyPowers
    }
}
if (-not [string]::IsNullOrWhiteSpace($OrbChecksPath)) {
    $request.orbChecks = @(Get-Content -LiteralPath $OrbChecksPath -Raw | ConvertFrom-Json)
} elseif (-not [string]::IsNullOrWhiteSpace($OrbChecksJson)) {
    $request.orbChecks = @($OrbChecksJson | ConvertFrom-Json)
}
if (-not [string]::IsNullOrWhiteSpace($PowerId)) {
    $request.powers = @(
        [ordered]@{
            powerId = $PowerId
            target = $PowerTarget
            targetIndex = 0
            amount = $PowerAmount
        }
    )
}

$requestTempPath = "$requestPath.$runId.tmp"
$request | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $requestTempPath -Encoding UTF8
Move-Item -LiteralPath $requestTempPath -Destination $requestPath -Force
$startedAt = Get-Date
$process = $null
if (Test-Path -LiteralPath $processMarkerPath -PathType Leaf) {
    $marker = Get-Content -LiteralPath $processMarkerPath -Raw | ConvertFrom-Json
    $candidate = Get-Process -Id $marker.pid -ErrorAction SilentlyContinue
    if ($null -ne $candidate -and $candidate.ProcessName -eq "SlayTheSpire2") {
        $process = $candidate
    }
}
$otherProcesses = @(Get-Process -Name "SlayTheSpire2" -ErrorAction SilentlyContinue |
    Where-Object { $null -eq $process -or $_.Id -ne $process.Id })
if ($otherProcesses.Count -gt 0) {
    $ids = $otherProcesses.Id -join ","
    throw "Refusing to start or reuse headless tests while an interactive SlayTheSpire2 process is running. pid=$ids"
}
$reusedProcess = $null -ne $process
if (-not $reusedProcess) {
    Install-HeadlessDependency
    $arguments = "--headless --disable-vsync --max-fps 0 --force-steam=off --log-file `"$headlessLogPath`""
    try {
        $process = Start-Process `
            -FilePath $gameExe `
            -ArgumentList $arguments `
            -Environment @{
                APPDATA = $headlessRoaming
                LOCALAPPDATA = $headlessLocal
                COMBATSOLVER_HEADLESS = "1"
            } `
            -WindowStyle Hidden `
            -PassThru
    } catch {
        Remove-HeadlessDependency
        throw
    }
    [ordered]@{
        pid = $process.Id
        startedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        appData = $headlessRoaming
        logPath = $headlessLogPath
    } | ConvertTo-Json | Set-Content -LiteralPath $processMarkerPath -Encoding UTF8
    $launchDeadline = $startedAt.AddSeconds(30)
    while (-not $process.HasExited -and (Get-Date) -lt $launchDeadline) {
        if (Test-Path -LiteralPath $headlessLogPath -PathType Leaf) {
            break
        }
        Start-Sleep -Milliseconds 250
    }
}
if ($null -eq $process -or $process.HasExited) {
    Remove-HeadlessDependency
    throw "Headless SlayTheSpire2 did not remain running. log=$headlessLogPath"
}
if ($reusedProcess) {
    Write-Host "UNATTENDED_REUSED run_id=$runId pid=$($process.Id)"
} else {
    Write-Host "UNATTENDED_STARTED run_id=$runId pid=$($process.Id)"
}

$deadline = $startedAt.AddSeconds($TimeoutSeconds + 45)
while ((Get-Date) -lt $deadline) {
    if (Test-Path -LiteralPath $resultPath) {
        $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        if ($result.runId -eq $runId) {
            $result | ConvertTo-Json -Depth 8
            if ($HoldAfterInitialSearch.IsPresent -and $result.status -eq "Passed") {
                Write-Host "UNATTENDED_HELD run_id=$runId pid=$($process.Id) release=$holdReleasePath"
                while (-not $process.HasExited -and -not (Test-Path -LiteralPath $holdReleasePath -PathType Leaf)) {
                    Start-Sleep -Milliseconds 500
                    $process.Refresh()
                }
                if (Test-Path -LiteralPath $holdReleasePath -PathType Leaf) {
                    Remove-Item -LiteralPath $holdReleasePath -Force
                }
                Stop-TestProcessAndRemoveDependency $process
                exit 0
            }
            if ($ExitOnComplete.IsPresent) {
                $process.WaitForExit(30000) | Out-Null
                Stop-TestProcessAndRemoveDependency $process
            }
            if ($result.status -ne "Passed") { exit 1 }
            exit 0
        }
    }
    if ($process.HasExited) {
        Remove-HeadlessDependency
        throw "Game exited without writing a result for this run. exit_code=$($process.ExitCode)"
    }
    Start-Sleep -Milliseconds 500
    $process.Refresh()
}

Stop-TestProcessAndRemoveDependency $process
throw "Unattended test exceeded the launcher timeout; its game process was stopped. run_id=$runId"
