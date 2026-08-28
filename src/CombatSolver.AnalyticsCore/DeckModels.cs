using System;
using System.Collections.Generic;

namespace CombatSolver.Analytics;

public sealed record DeckCardProfile(
	string PublicKey,
	int EnergyCost,
	double Damage = 0.0,
	double Block = 0.0,
	int CardsDrawn = 0,
	int EnergyGained = 0,
	double StrategicValue = 0.0,
	double DamageVariance = 0.0,
	double BlockVariance = 0.0,
	bool LeavesDeckAfterPlay = false,
	bool IsUnplayable = false,
	bool HasRandomOutcome = false)
{
	public DeckCardProfile Validate()
	{
		if (string.IsNullOrWhiteSpace(PublicKey))
		{
			throw new ArgumentException("A card profile requires a stable public key.", nameof(PublicKey));
		}
		if (EnergyCost < 0 || CardsDrawn < 0 || EnergyGained < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(EnergyCost), "Costs, draws, and gains must be non-negative.");
		}
		if (!double.IsFinite(Damage) || !double.IsFinite(Block) || !double.IsFinite(StrategicValue) ||
			!double.IsFinite(DamageVariance) || !double.IsFinite(BlockVariance) ||
			DamageVariance < 0.0 || BlockVariance < 0.0)
		{
			throw new ArgumentOutOfRangeException(nameof(Damage), "Card values must be finite and variances non-negative.");
		}
		return this;
	}
}

public sealed record TurnAnalysisOptions(
	int DrawCount = 5,
	int StartingEnergy = 3,
	int MaxHandSize = 10,
	long ExactCombinationLimit = 50_000,
	int SampleCount = 4_096,
	double DamageWeight = 1.0,
	double BlockWeight = 1.0,
	double StrategicWeight = 1.0,
	double DrawTempoValue = 1.25)
{
	public TurnAnalysisOptions Validate()
	{
		if (DrawCount < 0 || StartingEnergy < 0 || MaxHandSize <= 0 || ExactCombinationLimit <= 0 || SampleCount <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(DrawCount), "Analysis limits must be positive (draw and energy may be zero).");
		}
		if (!double.IsFinite(DamageWeight) || !double.IsFinite(BlockWeight) ||
			!double.IsFinite(StrategicWeight) || !double.IsFinite(DrawTempoValue))
		{
			throw new ArgumentOutOfRangeException(nameof(DamageWeight), "Analysis weights must be finite.");
		}
		return this;
	}
}

public sealed record MetricDistribution(
	double Mean,
	double Variance,
	double StandardDeviation,
	double LowerDecile,
	double LowerTailCvar,
	double Minimum,
	double Maximum,
	int ScenarioCount,
	bool IsExact);

public enum InfiniteStatus
{
	None,
	PotentialAfterRemovingBlockers,
	CurrentDeck
}

public sealed record InfiniteAnalysis(
	InfiniteStatus Status,
	int CycleCardCount,
	int BlockerCount,
	int NetEnergyPerCycle,
	int NetDrawPerCycle,
	double OutputPerCycle,
	IReadOnlyList<string> BlockerKeys,
	string Reason);

public sealed record DeckEvaluation(
	MetricDistribution Damage,
	MetricDistribution Block,
	MetricDistribution BalancedUtility,
	int DeckSize,
	int EffectiveCycleSize,
	double ActionableCardDensity,
	double ExpectedCycleTurns,
	InfiniteAnalysis Infinite,
	bool UsedSampling);

public enum DeckChangeKind
{
	Add,
	Remove
}

public sealed record DeckCandidate(string PublicKey, DeckCardProfile Card, DeckChangeKind Change = DeckChangeKind.Add);

public sealed record CardRecommendation(
	string PublicKey,
	DeckChangeKind Change,
	double Score,
	int Rank,
	bool Recommended,
	DeckEvaluation Evaluation,
	string Summary);

public sealed record DeckRecommendation(
	DeckEvaluation Baseline,
	IReadOnlyList<CardRecommendation> Candidates,
	string BestPublicKey,
	bool RecommendSkip);

public sealed record MerchantItemScore(string PublicKey, int Cost, double Value);

public sealed record MerchantPlan(
	IReadOnlyList<string> SelectedPublicKeys,
	int Spend,
	int GoldRemaining,
	double TotalValue);
