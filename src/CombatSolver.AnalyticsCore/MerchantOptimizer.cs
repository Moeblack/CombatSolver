using System;
using System.Collections.Generic;
using System.Linq;

namespace CombatSolver.Analytics;

public static class MerchantOptimizer
{
	private sealed record State(double Value, IReadOnlyList<string> Keys);

	public static MerchantPlan Optimize(
		int currentGold,
		int reserveGold,
		IReadOnlyList<MerchantItemScore> items)
	{
		if (currentGold < 0 || reserveGold < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(currentGold));
		}
		ArgumentNullException.ThrowIfNull(items);
		int budget = Math.Max(0, currentGold - reserveGold);
		Dictionary<int, State> states = new Dictionary<int, State> { [0] = new State(0.0, Array.Empty<string>()) };
		foreach (MerchantItemScore item in items.OrderBy(item => item.PublicKey, StringComparer.Ordinal))
		{
			if (string.IsNullOrWhiteSpace(item.PublicKey) || item.Cost < 0 || !double.IsFinite(item.Value))
			{
				throw new ArgumentException("Merchant items require a key, non-negative cost, and finite value.", nameof(items));
			}
			KeyValuePair<int, State>[] snapshot = states.ToArray();
			foreach ((int spent, State state) in snapshot)
			{
				int nextSpent = spent + item.Cost;
				if (nextSpent > budget)
				{
					continue;
				}
				double nextValue = state.Value + item.Value;
				IReadOnlyList<string> nextKeys = state.Keys.Append(item.PublicKey).ToArray();
				if (!states.TryGetValue(nextSpent, out State? existing) || Better(nextValue, nextKeys, existing.Value, existing.Keys))
				{
					states[nextSpent] = new State(nextValue, nextKeys);
				}
			}
		}

		KeyValuePair<int, State> best = states
			.OrderByDescending(pair => pair.Value.Value)
			.ThenBy(pair => pair.Key)
			.ThenBy(pair => string.Join("\u001f", pair.Value.Keys), StringComparer.Ordinal)
			.First();
		if (best.Value.Value <= 0.0)
		{
			return new MerchantPlan(Array.Empty<string>(), 0, currentGold, 0.0);
		}
		return new MerchantPlan(best.Value.Keys, best.Key, currentGold - best.Key, best.Value.Value);
	}

	private static bool Better(double leftValue, IReadOnlyList<string> leftKeys, double rightValue, IReadOnlyList<string> rightKeys)
	{
		if (leftValue != rightValue)
		{
			return leftValue > rightValue;
		}
		if (leftKeys.Count != rightKeys.Count)
		{
			return leftKeys.Count < rightKeys.Count;
		}
		return string.CompareOrdinal(string.Join("\u001f", leftKeys), string.Join("\u001f", rightKeys)) < 0;
	}
}
