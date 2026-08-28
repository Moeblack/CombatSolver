using System;
using System.Collections.Generic;
using System.Linq;

namespace CombatSolver.Analytics;

public static class DeckAdvisor
{
	public const string SkipPublicKey = "__skip__";

	public static DeckRecommendation Recommend(
		IReadOnlyList<DeckCardProfile> deck,
		IReadOnlyList<DeckCandidate> candidates,
		TurnAnalysisOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(deck);
		ArgumentNullException.ThrowIfNull(candidates);
		TurnAnalysisOptions settings = options ?? new TurnAnalysisOptions();
		DeckEvaluation baseline = DeckAnalyzer.Evaluate(deck, settings);
		List<(DeckCandidate Candidate, DeckEvaluation Evaluation, double Score)> scored = new List<(DeckCandidate, DeckEvaluation, double)>(candidates.Count);
		foreach (DeckCandidate candidate in candidates)
		{
			List<DeckCardProfile> changed = deck.ToList();
			if (candidate.Change == DeckChangeKind.Add)
			{
				changed.Add(candidate.Card);
			}
			else
			{
				int index = changed.FindIndex(card => CardMatches(card, candidate.Card));
				if (index < 0)
				{
					throw new ArgumentException($"Removal candidate '{candidate.PublicKey}' is not present in the deck.", nameof(candidates));
				}
				changed.RemoveAt(index);
			}
			DeckEvaluation evaluation = DeckAnalyzer.Evaluate(changed, settings);
			double score = ScoreDelta(baseline, evaluation);
			scored.Add((candidate, evaluation, score));
		}

		List<(DeckCandidate Candidate, DeckEvaluation Evaluation, double Score)> ordered = scored
			.OrderByDescending(entry => entry.Score)
			.ThenBy(entry => entry.Candidate.PublicKey, StringComparer.Ordinal)
			.ToList();
		bool skip = ordered.Count == 0 || ordered[0].Score <= 0.0;
		List<CardRecommendation> recommendations = new List<CardRecommendation>(ordered.Count);
		for (int i = 0; i < ordered.Count; i++)
		{
			var entry = ordered[i];
			recommendations.Add(new CardRecommendation(
				entry.Candidate.PublicKey,
				entry.Candidate.Change,
				entry.Score,
				i + 1,
				!skip && i == 0,
				entry.Evaluation,
				BuildSummary(baseline, entry.Evaluation, entry.Score)));
		}
		return new DeckRecommendation(baseline, recommendations, skip ? SkipPublicKey : ordered[0].Candidate.PublicKey, skip);
	}

	private static bool CardMatches(DeckCardProfile left, DeckCardProfile right)
	{
		return ReferenceEquals(left, right) || left == right || string.Equals(left.PublicKey, right.PublicKey, StringComparison.Ordinal);
	}

	private static double ScoreDelta(DeckEvaluation baseline, DeckEvaluation candidate)
	{
		double meanDelta = candidate.BalancedUtility.Mean - baseline.BalancedUtility.Mean;
		double tailDelta = candidate.BalancedUtility.LowerTailCvar - baseline.BalancedUtility.LowerTailCvar;
		double damageDelta = candidate.Damage.Mean - baseline.Damage.Mean;
		double blockDelta = candidate.Block.Mean - baseline.Block.Mean;
		double deviationDelta = candidate.BalancedUtility.StandardDeviation - baseline.BalancedUtility.StandardDeviation;
		double densityDelta = (candidate.ActionableCardDensity - baseline.ActionableCardDensity) * 4.0;
		double infiniteDelta = InfiniteValue(candidate.Infinite.Status) - InfiniteValue(baseline.Infinite.Status);
		return meanDelta * 0.45 + tailDelta * 0.35 + damageDelta * 0.1 + blockDelta * 0.1 -
			Math.Max(0.0, deviationDelta) * 0.12 + densityDelta + infiniteDelta;
	}

	private static double InfiniteValue(InfiniteStatus status)
	{
		return status switch
		{
			InfiniteStatus.CurrentDeck => 30.0,
			InfiniteStatus.PotentialAfterRemovingBlockers => 5.0,
			_ => 0.0
		};
	}

	private static string BuildSummary(DeckEvaluation baseline, DeckEvaluation candidate, double score)
	{
		double damageDelta = candidate.Damage.Mean - baseline.Damage.Mean;
		double blockDelta = candidate.Block.Mean - baseline.Block.Mean;
		double tailDelta = candidate.BalancedUtility.LowerTailCvar - baseline.BalancedUtility.LowerTailCvar;
		return $"score {score:+0.0;-0.0;0.0}; E[dmg] {damageDelta:+0.0;-0.0;0.0}; E[block] {blockDelta:+0.0;-0.0;0.0}; lower-tail {tailDelta:+0.0;-0.0;0.0}";
	}
}
