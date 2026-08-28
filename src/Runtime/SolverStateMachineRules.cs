using System.Collections.Generic;

namespace CombatSolver;

/// <summary>
/// Pure state-machine predicates used by the native-choice runtime.
/// Keeping these rules free of Godot/game references makes request ordering
/// and reconciliation behavior testable without starting a game session.
/// </summary>
internal static class SolverStateMachineRules
{
    public static bool SurfaceContainsRequestOptions<T>(
        IReadOnlyList<T> surfaceOptions,
        IReadOnlyList<T> requestOptions)
        where T : class
    {
        if (requestOptions.Count == 0)
            return false;

        foreach (T requested in requestOptions)
        {
            bool found = false;
            foreach (T presented in surfaceOptions)
            {
                if (ReferenceEquals(requested, presented))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
                return false;
        }
        return true;
    }

    public static bool IsAnonymousEmptyChoice(
        string sourceId,
        string contextId,
        int selectedCardCount)
        => selectedCardCount == 0
           && string.IsNullOrEmpty(sourceId)
           && string.IsNullOrEmpty(contextId);

    public static bool IsImplicitEmptyChoiceRequest(
        bool requiresSurface,
        int optionCount,
        int minSelect,
        int maxSelect)
        => !requiresSurface
           && optionCount == 0
           && minSelect == 0
           && maxSelect == 0;
}

internal sealed class AcceptedRequestQueue<T>
{
    private readonly Queue<T> _requests = new();

    public T? Current => _requests.Count == 0 ? default : _requests.Peek();

    public bool HasPending => _requests.Count != 0;

    public void Enqueue(T request)
        => _requests.Enqueue(request);

    public void MarkAccepted(T request)
    {
        if (_requests.Count == 0
            || !EqualityComparer<T>.Default.Equals(_requests.Peek(), request))
        {
            throw new InvalidOperationException("选择请求完成顺序与观察顺序不一致。");
        }
        _requests.Dequeue();
    }

    public void Clear()
        => _requests.Clear();
}
