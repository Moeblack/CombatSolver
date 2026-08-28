namespace CombatSolver;

internal sealed partial class SimulatedCombatState
{
    public TurnStartChoiceRequest? PendingTurnStartChoice { get; private set; }

    public void SetPendingTurnStartChoice(TurnStartChoiceRequest request)
    {
        if (PendingTurnStartChoice != null)
            throw new InvalidOperationException("模拟状态已经存在待处理的回合开始选牌。");
        PendingTurnStartChoice = request;
    }

    public void ClearPendingTurnStartChoice()
        => PendingTurnStartChoice = null;
}
