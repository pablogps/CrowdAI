// NeatEvolutionAlgorithm is over 1000 lines and it should be refactored.
// CreateOffspring seems like a reasonable candidate to move to a separate
// class. There are a few variable that can't be reached yet, but that could
// maybe be solved in the creator?

// Perhaps also bring here CreateOffspring_CrossSpecieMating
// which, by the way, currently uses FIVE parameters!

// Move also:
/*
public class SpecieStats
{
	// Real/continuous stats.
	public double _meanFitness;
	public double _targetSizeReal;

	// Integer stats.
	public int _targetSizeInt;
	public int _eliteSizeInt;
	public int _offspringCount;
	public int _offspringAsexualCount;
	public int _offspringSexualCount;

	// Selection data.
	public int _selectionSizeInt;
}
*/


/*
 * 
 * 
using System;
using System.Collections.Generic;
using SharpNeat.Core;
using System.Diagnostics;
using SharpNeat.DistanceMetrics;
using SharpNeat.EvolutionAlgorithms.ComplexityRegulation;
using SharpNeat.SpeciationStrategies;
using SharpNeat.Utility;

namespace SharpNeat.EvolutionAlgorithms
{
    public static class NeatEvolutionAlgorithmOffspringCreator<TGenome>
        where TGenome : class, IGenome<TGenome>
    {
        /// <summary>
        /// Create the required number of offspring genomes, using specieStatsArr 
        /// as the basis for selecting how many offspring are produced from each species.
        /// </summary>
        public static List<TGenome> CreateOffspring(SpecieStats[] specieStatsArr, int offspringCount)
        {
        	// Sexual reproduction can be from two genomes in the same specie
        	// or from two genomes in different species.            
        	// Build a RouletteWheelLayout for selecting species for cross-species
        	// reproduction. While we're in the loop we also pre-build a 
        	// RouletteWheelLayout for each specie; doing this before the main 
        	// loop means we have RouletteWheelLayouts available for all species
        	// when performing cross-specie matings.
        	int specieCount = specieStatsArr.Length;
        	double[] specieFitnessArr = new double[specieCount];
        	RouletteWheelLayout[] rwlArr = new RouletteWheelLayout[specieCount];

        	// Count of species with non-zero selection size.
        	// If this is exactly 1 then we skip inter-species mating. One is a
        	// special case because for 0 the species all get an even chance of 
        	// selection, and for >1 we can just select normally.
        	int nonZeroSpecieCount = 0;
        	for (int i = 0; i < specieCount; i++)
        	{
        		// Array of probabilities for specie selection. Note that some 
        		// of these probabilites can be zero, but at least one of them won't be.
        		SpecieStats inst = specieStatsArr[i];
        		specieFitnessArr[i] = inst._selectionSizeInt;
        		if (0 != inst._selectionSizeInt)
        		{
        			nonZeroSpecieCount++;
        		}

        		// For each specie we build a RouletteWheelLayout for genome
        		// selection within that specie. Fitter genomes have higher
        		// probability of selection.
        		List<TGenome> genomeList = _specieList[i].GenomeList;
        		double[] probabilities = new double[inst._selectionSizeInt];
        		for (int j = 0; j < inst._selectionSizeInt; j++)
        		{
        			probabilities[j] = genomeList[j].EvaluationInfo.Fitness;
        		}
        		rwlArr[i] = new RouletteWheelLayout(probabilities);
        	}

        	// Complete construction of RouletteWheelLayout for specie selection.
        	RouletteWheelLayout rwlSpecies = new RouletteWheelLayout(specieFitnessArr);


        	Console.WriteLine("\nReproduction statistics readdy");


        	// Produce offspring from each specie in turn and store them in offspringList.
        	List<TGenome> offspringList = new List<TGenome>(offspringCount);
        	for (int specieIdx = 0; specieIdx < specieCount; specieIdx++)
        	{
        		SpecieStats inst = specieStatsArr[specieIdx];
        		List<TGenome> genomeList = _specieList[specieIdx].GenomeList;

        		// Get RouletteWheelLayout for genome selection.
        		RouletteWheelLayout rwl = rwlArr[specieIdx];

        		// --- Produce the required number of offspring from asexual reproduction.



        		Console.WriteLine("\nAsexual reproduction");



        		for (int i = 0; i < inst._offspringAsexualCount; i++)
        		{
        			int genomeIdx = RouletteWheel.SingleThrow(rwl, _rng);
        			TGenome offspring = genomeList[genomeIdx].CreateOffspring(_currentGeneration);
        			offspringList.Add(offspring);
        		}
        		_stats._asexualOffspringCount += (ulong)inst._offspringAsexualCount;




        		Console.WriteLine("\nSexual reproduction");



        		// --- Produce the required number of offspring from sexual reproduction.
        		// Sexual reproduction can be from two genomes in the same specie
        		// or from two genomes in different species.
        		// Cross-specie mating.
        		// If nonZeroSpecieCount is exactly 1 then we skip inter-species 
        		// mating. One is a special case because for 0 the  species all 
        		// get an even chance of selection, and for >1 we can just select 
        		// species normally.
        		int crossSpecieMatings = nonZeroSpecieCount == 1 ? 0 :
        				(int)Utilities.ProbabilisticRound(_eaParams.InterspeciesMatingProportion *
        												  inst._offspringSexualCount, _rng);
        		_stats._sexualOffspringCount += (ulong)(inst._offspringSexualCount -
        												crossSpecieMatings);
        		_stats._interspeciesOffspringCount += (ulong)crossSpecieMatings;

        		// An index that keeps track of how many offspring have been 
        		// produced in total.
        		int matingsCount = 0;
        		for (; matingsCount < crossSpecieMatings; matingsCount++)
        		{
        			TGenome offspring =
        					CreateOffspring_CrossSpecieMating(rwl, rwlArr,
        													  rwlSpecies, specieIdx,
        													  genomeList);
        			offspringList.Add(offspring);
        		}



        		Console.WriteLine("\nSexual reproduction 2");


        		// For the remainder we use normal intra-specie mating.
        		// If there is only one genome in this specie we use asexual
        		// reproduction (offspring are still unique!)
        		if (1 == inst._selectionSizeInt)
        		{
        			Console.WriteLine("\nPath A");
        			for (; matingsCount < inst._offspringSexualCount; matingsCount++)
        			{
        				int genomeIdx = RouletteWheel.SingleThrow(rwl, _rng);
        				TGenome offspring =
        						genomeList[genomeIdx].CreateOffspring(_currentGeneration);
        				offspringList.Add(offspring);
        			}
        		}
        		else
        		{
        			Console.WriteLine("\nPath B");
        			// Remainder of matings are normal within-specie.
        			for (; matingsCount < inst._offspringSexualCount; matingsCount++)
        			{
        				// Select parents. SelectRouletteWheelItem() guarantees 
        				// parent2Idx!=parent1Idx
        				int parent1Idx = RouletteWheel.SingleThrow(rwl, _rng);
        				TGenome parent1 = genomeList[parent1Idx];

        				// Remove selected parent from set of possible outcomes.
        				// It is only removed from a temporal copy!
        				RouletteWheelLayout rwlTmp = rwl.RemoveOutcome(parent1Idx);
        				// Check the remaining combined probability for other
        				// parents is > 0 (otherwise: asexual reproduction)
        				float epsilon = 0.01f;
        				if (rwlTmp.ProbabilitiesTotal > epsilon)
        				{   // Get the two parents to mate.
        					int parent2Idx = RouletteWheel.SingleThrow(rwlTmp, _rng);
        					TGenome parent2 = genomeList[parent2Idx];
        					Console.WriteLine("\nPrepared for mating " + parent1.Id + " and " + parent2.Id);
        					Console.WriteLine("\nParent 1 check: ");
        					parent1.PrintGenomeStatistics();
        					Console.WriteLine("\nParent 2 check: ");
        					parent2.PrintGenomeStatistics();
        					TGenome offspring =
        							parent1.CreateOffspring(parent2, _currentGeneration);
        					offspringList.Add(offspring);
        				}
        				else
        				{   // No other parent has a non-zero selection 
        					// probability (they all have zero fitness). Fall 
        					// back to asexual reproduction of the single genome
        					// with a non-zero fitness.
        					Console.WriteLine("\nPrepared to reproduce " + parent1.Id);
        					TGenome offspring = parent1.CreateOffspring(_currentGeneration);
        					offspringList.Add(offspring);
        				}
        			}
        		}
        	}
        	_stats._totalOffspringCount += (ulong)offspringCount;
        	return offspringList;
        }






public class SpecieStats
{
	// Real/continuous stats.
	public double _meanFitness;
	public double _targetSizeReal;

	// Integer stats.
	public int _targetSizeInt;
	public int _eliteSizeInt;
	public int _offspringCount;
	public int _offspringAsexualCount;
	public int _offspringSexualCount;

	// Selection data.
	public int _selectionSizeInt;
        }






    }
}


*/
