using System;
using System.Collections.Generic;
using System.Linq;

namespace CombatSolver.Analytics;

public static class DeckAnalyzer
{
	private enum Objective
	{
		Damage,
		Block,
		Balanced
	}

	private readonly record struct PlanOutcome(
		double Damage,
		double Block,
		double Utility,
		double DamageVariance,
		double BlockVariance,
		double UtilityVariance);

	public static DeckEvaluation Evaluate(IReadOnlyList<DeckCardProfile> deck, TurnAnalysisOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(deck);
		TurnAnalysisOptions settings = (options ?? new TurnAnalysisOptions()).Validate();
		DeckCardProfile[] stableDeck = deck.Select(card => card.Validate())
			.OrderBy(card => card.PublicKey, StringComparer.Ordinal)
			.ThenBy(card => card.EnergyCost)
			.ThenBy(card => card.Damage)
			.ThenBy(card => card.Block)
			.ToArray();

		int drawCount = Math.Min(settings.DrawCount, stableDeck.Length);
		long combinationCount = CombinationCount(stableDeck.Length, drawCount);
		bool exact = combinationCount <= settings.ExactCombinationLimit;
		int scenarioCount = exact ? checked((int)combinationCount) : Math.Min(settings.SampleCount, checked((int)Math.Min(combinationCount, int.MaxValue)));
		if (scenarioCount == 0)
		{
			scenarioCount = 1;
		}

		List<double> damage = new List<double>(scenarioCount);
		List<double> damageVariance = new List<double>(scenarioCount);
		List<double> block = new List<double>(scenarioCount);
		List<double> blockVariance = new List<double>(scenarioCount);
		List<double> utility = new List<double>(scenarioCount);
		List<double> utilityVariance = new List<double>(scenarioCount);
		if (stableDeck.Length == 0 || drawCount == 0)
		{
			AddEmptyScenario(damage, damageVariance, block, blockVariance, utility, utilityVariance);
		}
		else
		{
			for (int scenario = 0; scenario < scenarioCount; scenario++)
			{
				long rank = exact
					? scenario
					: (long)(((decimal)(2L * scenario + 1L) * combinationCount) / (2L * scenarioCount));
				int[] indexes = UnrankCombination(stableDeck.Length, drawCount, rank);
				DeckCardProfile[] hand = new DeckCardProfile[indexes.Length];
				for (int i = 0; i < indexes.Length; i++)
				{
					hand[i] = stableDeck[indexes[i]];
				}

				PlanOutcome damagePlan = OptimizeHand(hand, settings, Objective.Damage);
				PlanOutcome blockPlan = OptimizeHand(hand, settings, Objective.Block);
				PlanOutcome balancedPlan = OptimizeHand(hand, settings, Objective.Balanced);
				damage.Add(damagePlan.Damage);
				damageVariance.Add(damagePlan.DamageVariance);
				block.Add(blockPlan.Block);
				blockVariance.Add(blockPlan.BlockVariance);
				utility.Add(balancedPlan.Utility);
				utilityVariance.Add(balancedPlan.UtilityVariance);
			}
		}

		int effectiveCycleSize = stableDeck.Count(card => !card.LeavesDeckAfterPlay);
		int actionableCards = stableDeck.Count(card => !card.IsUnplayable &&
			(card.Damage > 0.0 || card.Block > 0.0 || card.CardsDrawn > 0 || card.EnergyGained > 0 || card.StrategicValue > 0.0));
		double density = stableDeck.Length == 0 ? 0.0 : (double)actionableCards / stableDeck.Length;
		double expectedCycleTurns = settings.DrawCount == 0 ? double.PositiveInfinity : (double)effectiveCycleSize / settings.DrawCount;
		return new DeckEvaluation(
			DistributionMath.Summarize(damage, damageVariance, exact),
			DistributionMath.Summarize(block, blockVariance, exact),
			DistributionMath.Summarize(utility, utilityVariance, exact),
			stableDeck.Length,
			effectiveCycleSize,
			density,
			expectedCycleTurns,
			AnalyzeInfinite(stableDeck, settings),
			!exact);
	}

	private static void AddEmptyScenario(
		ICollection<double> damage,
		ICollection<double> damageVariance,
		ICollection<double> block,
		ICollection<double> blockVariance,
		ICollection<double> utility,
		ICollection<double> utilityVariance)
	{
		damage.Add(0.0);
		damageVariance.Add(0.0);
		block.Add(0.0);
		blockVariance.Add(0.0);
		utility.Add(0.0);
		utilityVariance.Add(0.0);
	}

	private static PlanOutcome OptimizeHand(DeckCardProfile[] hand, TurnAnalysisOptions options, Objective objective)
	{
		if (hand.Length > 22)
		{
			return OptimizeLargeHand(hand, options, objective);
		}

		int stateCount = 1 << hand.Length;
		int[] bestEnergy = new int[stateCount];
		Array.Fill(bestEnergy, -1);
		bestEnergy[0] = options.StartingEnergy;
		PlanOutcome best = default;
		double bestScore = 0.0;
		for (int mask = 0; mask < stateCount; mask++)
		{
			int energy = bestEnergy[mask];
			if (energy < 0)
			{
				continue;
			}

			PlanOutcome outcome = Aggregate(hand, mask, options);
			double score = ObjectiveScore(outcome, objective);
			if (score > bestScore || (score == bestScore && outcome.Utility > best.Utility))
			{
				bestScore = score;
				best = outcome;
			}

			for (int cardIndex = 0; cardIndex < hand.Length; cardIndex++)
			{
				int bit = 1 << cardIndex;
				DeckCardProfile card = hand[cardIndex];
				if ((mask & bit) != 0 || card.IsUnplayable || card.EnergyCost > energy)
				{
					continue;
				}
				int nextMask = mask | bit;
				int nextEnergy = energy - card.EnergyCost + card.EnergyGained;
				if (nextEnergy > bestEnergy[nextMask])
				{
					bestEnergy[nextMask] = nextEnergy;
				}
			}
		}
		return best;
	}

	private static PlanOutcome OptimizeLargeHand(DeckCardProfile[] hand, TurnAnalysisOptions options, Objective objective)
	{
		IEnumerable<DeckCardProfile> ordered = hand.Where(card => !card.IsUnplayable)
			.OrderByDescending(card => ObjectiveScore(AggregateCard(card, options), objective))
			.ThenBy(card => card.PublicKey, StringComparer.Ordinal);
		int energy = options.StartingEnergy;
		double damage = 0.0;
		double block = 0.0;
		double utility = 0.0;
		double damageVariance = 0.0;
		double blockVariance = 0.0;
		foreach (DeckCardProfile card in ordered)
		{
			if (card.EnergyCost > energy)
			{
				continue;
			}
			energy += card.EnergyGained - card.EnergyCost;
			PlanOutcome value = AggregateCard(card, options);
			damage += value.Damage;
			block += value.Block;
			utility += value.Utility;
			damageVariance += value.DamageVariance;
			blockVariance += value.BlockVariance;
		}
		return new PlanOutcome(damage, block, utility, damageVariance, blockVariance,
			options.DamageWeight * options.DamageWeight * damageVariance + options.BlockWeight * options.BlockWeight * blockVariance);
	}

	private static PlanOutcome Aggregate(DeckCardProfile[] hand, int mask, TurnAnalysisOptions options)
	{
		double damage = 0.0;
		double block = 0.0;
		double strategic = 0.0;
		double damageVariance = 0.0;
		double blockVariance = 0.0;
		for (int i = 0; i < hand.Length; i++)
		{
			if ((mask & (1 << i)) == 0)
			{
				continue;
			}
			DeckCardProfile card = hand[i];
			damage += card.Damage;
			block += card.Block;
			strategic += card.StrategicValue + card.CardsDrawn * options.DrawTempoValue;
			damageVariance += card.DamageVariance;
			blockVariance += card.BlockVariance;
		}
		double utility = damage * options.DamageWeight + block * options.BlockWeight + strategic * options.StrategicWeight;
		double utilityVariance = options.DamageWeight * options.DamageWeight * damageVariance +
			options.BlockWeight * options.BlockWeight * blockVariance;
		return new PlanOutcome(damage, block, utility, damageVariance, blockVariance, utilityVariance);
	}

	private static PlanOutcome AggregateCard(DeckCardProfile card, TurnAnalysisOptions options)
	{
		double utility = card.Damage * options.DamageWeight + card.Block * options.BlockWeight +
			(card.StrategicValue + card.CardsDrawn * options.DrawTempoValue) * options.StrategicWeight;
		return new PlanOutcome(card.Damage, card.Block, utility, card.DamageVariance, card.BlockVariance,
			options.DamageWeight * options.DamageWeight * card.DamageVariance +
			options.BlockWeight * options.BlockWeight * card.BlockVariance);
	}

	private static double ObjectiveScore(PlanOutcome outcome, Objective objective)
	{
		return objective switch
		{
			Objective.Damage => outcome.Damage,
			Objective.Block => outcome.Block,
			_ => outcome.Utility
		};
	}

	private static InfiniteAnalysis AnalyzeInfinite(IReadOnlyList<DeckCardProfile> deck, TurnAnalysisOptions options)
	{
		List<DeckCardProfile> persistent = deck.Where(card => !card.LeavesDeckAfterPlay).ToList();
		if (TryProveCycle(persistent, options, out string reason))
		{
			return BuildInfinite(InfiniteStatus.CurrentDeck, persistent, Array.Empty<DeckCardProfile>(), reason);
		}

		List<DeckCardProfile> kept = persistent.ToList();
		List<DeckCardProfile> blockers = new List<DeckCardProfile>();
		IEnumerable<DeckCardProfile> removalOrder = persistent
			.OrderByDescending(card => card.IsUnplayable ? double.PositiveInfinity : LoopDrag(card))
			.ThenBy(card => card.PublicKey, StringComparer.Ordinal);
		foreach (DeckCardProfile card in removalOrder)
		{
			if (kept.Count <= 1)
			{
				break;
			}
			kept.Remove(card);
			blockers.Add(card);
			if (TryProveCycle(kept, options, out reason))
			{
				return BuildInfinite(InfiniteStatus.PotentialAfterRemovingBlockers, kept, blockers, reason);
			}
		}

		return new InfiniteAnalysis(
			InfiniteStatus.None,
			persistent.Count,
			0,
			persistent.Sum(card => card.EnergyGained - card.EnergyCost),
			persistent.Sum(card => card.CardsDrawn) - persistent.Count,
			persistent.Sum(card => card.Damage + card.Block + Math.Max(0.0, card.StrategicValue)),
			Array.Empty<string>(),
			"No closed draw-and-energy cycle with positive output was proven from public card profiles.");
	}

	private static InfiniteAnalysis BuildInfinite(
		InfiniteStatus status,
		IReadOnlyList<DeckCardProfile> cycle,
		IReadOnlyList<DeckCardProfile> blockers,
		string reason)
	{
		return new InfiniteAnalysis(
			status,
			cycle.Count,
			blockers.Count,
			cycle.Sum(card => card.EnergyGained - card.EnergyCost),
			cycle.Sum(card => card.CardsDrawn) - cycle.Count,
			cycle.Sum(card => card.Damage + card.Block + Math.Max(0.0, card.StrategicValue)),
			blockers.Select(card => card.PublicKey).ToArray(),
			reason);
	}

	private static bool TryProveCycle(IReadOnlyList<DeckCardProfile> cards, TurnAnalysisOptions options, out string reason)
	{
		if (cards.Count == 0 || cards.Count > options.MaxHandSize)
		{
			reason = "The persistent cycle is empty or exceeds the modeled hand capacity.";
			return false;
		}
		if (cards.Any(card => card.IsUnplayable))
		{
			reason = "An unplayable persistent card blocks the cycle.";
			return false;
		}
		if (cards.Sum(card => card.CardsDrawn) < cards.Count)
		{
			reason = "The cycle does not redraw every persistent card.";
			return false;
		}
		if (cards.Sum(card => card.EnergyGained) < cards.Sum(card => card.EnergyCost))
		{
			reason = "The cycle loses energy.";
			return false;
		}
		if (cards.Sum(card => card.Damage + card.Block + Math.Max(0.0, card.StrategicValue)) <= 0.0)
		{
			reason = "The cycle has no modeled combat output.";
			return false;
		}
		if (!CanPlayWholeCycle(cards, options.StartingEnergy))
		{
			reason = "No affordable ordering can start the cycle.";
			return false;
		}
		reason = "Every persistent card can be replayed, total draw closes the cycle, energy is non-negative, and output is positive.";
		return true;
	}

	private static bool CanPlayWholeCycle(IReadOnlyList<DeckCardProfile> cards, int startingEnergy)
	{
		if (cards.Count > 22)
		{
			return false;
		}
		int stateCount = 1 << cards.Count;
		int[] bestEnergy = new int[stateCount];
		Array.Fill(bestEnergy, -1);
		bestEnergy[0] = startingEnergy;
		for (int mask = 0; mask < stateCount; mask++)
		{
			int energy = bestEnergy[mask];
			if (energy < 0)
			{
				continue;
			}
			for (int index = 0; index < cards.Count; index++)
			{
				int bit = 1 << index;
				DeckCardProfile card = cards[index];
				if ((mask & bit) != 0 || card.EnergyCost > energy)
				{
					continue;
				}
				int next = mask | bit;
				bestEnergy[next] = Math.Max(bestEnergy[next], energy - card.EnergyCost + card.EnergyGained);
			}
		}
		return bestEnergy[^1] >= startingEnergy;
	}

	private static double LoopDrag(DeckCardProfile card)
	{
		return (card.EnergyCost - card.EnergyGained) + (1 - card.CardsDrawn) * 1.5 -
			(card.Damage + card.Block + Math.Max(0.0, card.StrategicValue)) * 0.001;
	}

	internal static long CombinationCount(int n, int k)
	{
		if (k < 0 || k > n)
		{
			return 0;
		}
		k = Math.Min(k, n - k);
		decimal result = 1m;
		for (int i = 1; i <= k; i++)
		{
			result = result * (n - k + i) / i;
			if (result >= long.MaxValue)
			{
				return long.MaxValue;
			}
		}
		return (long)result;
	}

	internal static int[] UnrankCombination(int n, int k, long rank)
	{
		long total = CombinationCount(n, k);
		if (rank < 0 || rank >= total)
		{
			throw new ArgumentOutOfRangeException(nameof(rank));
		}
		int[] result = new int[k];
		int nextCandidate = 0;
		for (int position = 0; position < k; position++)
		{
			int remaining = k - position - 1;
			for (int candidate = nextCandidate; candidate <= n - remaining - 1; candidate++)
			{
				long suffixCount = CombinationCount(n - candidate - 1, remaining);
				if (rank < suffixCount)
				{
					result[position] = candidate;
					nextCandidate = candidate + 1;
					break;
				}
				rank -= suffixCount;
			}
		}
		return result;
	}
}
