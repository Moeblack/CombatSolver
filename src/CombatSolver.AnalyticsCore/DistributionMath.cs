using System;
using System.Collections.Generic;
using System.Numerics;

namespace CombatSolver.Analytics;

internal static class DistributionMath
{
	public static MetricDistribution Summarize(
		IReadOnlyList<double> values,
		IReadOnlyList<double> intrinsicVariances,
		bool isExact)
	{
		if (values.Count == 0 || values.Count != intrinsicVariances.Count)
		{
			throw new ArgumentException("A distribution requires equally sized, non-empty value and variance collections.");
		}

		double[] valueArray = new double[values.Count];
		double[] varianceArray = new double[values.Count];
		for (int i = 0; i < values.Count; i++)
		{
			valueArray[i] = values[i];
			varianceArray[i] = intrinsicVariances[i];
		}

		(double sum, double squareSum, double intrinsicSum) = ComputeMoments(valueArray, varianceArray);
		double mean = sum / valueArray.Length;
		double variance = Math.Max(0.0, squareSum / valueArray.Length - mean * mean + intrinsicSum / valueArray.Length);
		Array.Sort(valueArray);
		double lowerDecile = Quantile(valueArray, 0.1);
		double cvar = LowerTailAverage(valueArray, 0.1);
		return new MetricDistribution(
			mean,
			variance,
			Math.Sqrt(variance),
			lowerDecile,
			cvar,
			valueArray[0],
			valueArray[^1],
			valueArray.Length,
			isExact);
	}

	private static (double Sum, double SquareSum, double IntrinsicSum) ComputeMoments(
		ReadOnlySpan<double> values,
		ReadOnlySpan<double> intrinsicVariances)
	{
		int width = Vector<double>.Count;
		Vector<double> vectorSum = Vector<double>.Zero;
		Vector<double> vectorSquareSum = Vector<double>.Zero;
		Vector<double> vectorIntrinsicSum = Vector<double>.Zero;
		int index = 0;
		for (; index <= values.Length - width; index += width)
		{
			Vector<double> valueVector = new Vector<double>(values.Slice(index, width));
			Vector<double> varianceVector = new Vector<double>(intrinsicVariances.Slice(index, width));
			vectorSum += valueVector;
			vectorSquareSum += valueVector * valueVector;
			vectorIntrinsicSum += varianceVector;
		}

		double sum = Vector.Sum(vectorSum);
		double squareSum = Vector.Sum(vectorSquareSum);
		double intrinsicSum = Vector.Sum(vectorIntrinsicSum);
		for (; index < values.Length; index++)
		{
			double value = values[index];
			sum += value;
			squareSum += value * value;
			intrinsicSum += intrinsicVariances[index];
		}
		return (sum, squareSum, intrinsicSum);
	}

	private static double Quantile(IReadOnlyList<double> sorted, double probability)
	{
		if (sorted.Count == 1)
		{
			return sorted[0];
		}
		double position = probability * (sorted.Count - 1);
		int lower = (int)Math.Floor(position);
		int upper = (int)Math.Ceiling(position);
		double fraction = position - lower;
		return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
	}

	private static double LowerTailAverage(IReadOnlyList<double> sorted, double tailMass)
	{
		double targetMass = sorted.Count * tailMass;
		int fullValues = (int)Math.Floor(targetMass);
		double partialMass = targetMass - fullValues;
		double sum = 0.0;
		for (int i = 0; i < fullValues; i++)
		{
			sum += sorted[i];
		}
		if (partialMass > 0.0)
		{
			sum += sorted[Math.Min(fullValues, sorted.Count - 1)] * partialMass;
		}
		return sum / targetMass;
	}
}
