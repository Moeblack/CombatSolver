using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private sealed class ProtocolHost
    {
        private bool _requestLoopStarted;
        private int _acceptedRequestCount;
        private int _injectPlayerHpLossTurn;
        private int _injectPlayerHpLossAmount;
        private int _injectedPlayerHpLoss;
        private int _clearPlayerBlockBeforeEndTurn;
        private int _clearedPlayerBlock;

        public bool IsActive { get; private set; }
        public bool AutomaticTurnSearchEnabled { get; private set; } = true;
        public bool VerifyIncrementalSearch { get; private set; }
        public bool ForceShortSearchOnly { get; private set; }
        public bool MeasureSearchPhases { get; private set; }
        public int? ShortSearchBudgetOverrideMilliseconds { get; private set; }
        public int? DeepSearchBudgetOverrideMilliseconds { get; private set; }

        public void TryStart(NGame? host)
        {
            if (_requestLoopStarted || host == null)
                return;

            _requestLoopStarted = true;
            TaskHelper.RunSafely(RunRequestLoopAsync(host));
            Entry.Logger.Info("[CombatSolver/Unattended] REQUEST_LOOP_STARTED reuse_process=true");
        }

        public void EnableAutomaticTurnSearch()
            => AutomaticTurnSearchEnabled = true;

        public async Task ApplyScheduledStateDriftAsync(CombatState state, int turn)
        {
            if (!IsActive
                || turn != _injectPlayerHpLossTurn
                || _injectPlayerHpLossAmount <= 0
                || Interlocked.Exchange(ref _injectedPlayerHpLoss, 1) != 0)
            {
                return;
            }

            Player player = LocalContext.GetMe(state)
                ?? throw new InvalidOperationException("状态漂移测试找不到本地玩家。");
            int before = player.Creature.CurrentHp;
            int after = Math.Max(1, before - _injectPlayerHpLossAmount);
            await CreatureCmd.SetCurrentHp(player.Creature, after);
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
            Entry.Logger.Info(
                $"[CombatSolver/Unattended] INJECT_STATE_DRIFT turn={turn} field=hp before={before} after={after}");
        }

        public async Task ApplyScheduledPreEndTurnDriftAsync(CombatState state, int turn)
        {
            if (!IsActive
                || turn != _clearPlayerBlockBeforeEndTurn
                || Interlocked.Exchange(ref _clearedPlayerBlock, 1) != 0)
            {
                return;
            }

            Player player = LocalContext.GetMe(state)
                ?? throw new InvalidOperationException("结束回合漂移测试找不到本地玩家。");
            int before = player.Creature.Block;
            await SetBlockAsync(player.Creature, 0);
            Entry.Logger.Info(
                $"[CombatSolver/Unattended] INJECT_PRE_END_TURN_DRIFT turn={turn} field=block before={before} after=0");
        }

        private async Task RunRequestLoopAsync(NGame host)
        {
            string runningPath = UnattendedTestFiles.GlobalPath(UnattendedTestFiles.RunningUri);
            string requestPath = UnattendedTestFiles.GlobalPath(UnattendedTestFiles.RequestUri);
            while (true)
            {
                if (!File.Exists(requestPath))
                {
                    for (int frame = 0; frame < 10; frame++)
                        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
                    continue;
                }

                string json = File.ReadAllText(requestPath);
                UnattendedTestRequest request = JsonSerializer.Deserialize<UnattendedTestRequest>(
                    json,
                    UnattendedTestFiles.JsonOptions)
                    ?? throw new InvalidOperationException("无人测试请求为空。");
                if (request.SchemaVersion != 1)
                    throw new InvalidOperationException($"不支持的无人测试协议版本 {request.SchemaVersion}。");

                File.Move(requestPath, runningPath, true);
                Activate(request);
                int requestSequence = ++_acceptedRequestCount;
                Entry.Logger.Info(
                    $"[CombatSolver/Unattended] REQUEST_ACCEPTED run_id={request.RunId} " +
                    $"scenario={request.ScenarioId} process_sequence={requestSequence} reused_process={requestSequence > 1}");
                try
                {
                    await new UnattendedTestRunner(host, request, this).RunAsync();
                }
                finally
                {
                    Reset();
                }
                if (!request.ExitOnComplete)
                {
                    for (int frame = 0; frame < 180; frame++)
                        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private void Activate(UnattendedTestRequest request)
        {
            IsActive = true;
            _injectPlayerHpLossTurn = request.InjectPlayerHpLossBeforeAutoSearchTurn ?? 0;
            _injectPlayerHpLossAmount = request.InjectPlayerHpLossAmount;
            _injectedPlayerHpLoss = 0;
            _clearPlayerBlockBeforeEndTurn = request.ClearPlayerBlockBeforeEndTurnForTest ?? 0;
            _clearedPlayerBlock = 0;
            AutomaticTurnSearchEnabled = false;
            VerifyIncrementalSearch = request.VerifyIncrementalSearch;
            ForceShortSearchOnly = request.ForceShortSearchOnly;
            MeasureSearchPhases = request.MeasureSearchPhases;
            ShortSearchBudgetOverrideMilliseconds = request.ShortSearchBudgetOverrideMilliseconds;
            DeepSearchBudgetOverrideMilliseconds = request.DeepSearchBudgetOverrideMilliseconds;
        }

        private void Reset()
        {
            IsActive = false;
            AutomaticTurnSearchEnabled = true;
            VerifyIncrementalSearch = false;
            ForceShortSearchOnly = false;
            MeasureSearchPhases = false;
            _injectPlayerHpLossTurn = 0;
            _injectPlayerHpLossAmount = 0;
            _injectedPlayerHpLoss = 0;
            _clearPlayerBlockBeforeEndTurn = 0;
            _clearedPlayerBlock = 0;
            ShortSearchBudgetOverrideMilliseconds = null;
            DeepSearchBudgetOverrideMilliseconds = null;
        }
    }
}
