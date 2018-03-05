/* ***************************************************************************
 * This file is part of SharpNEAT - Evolution of Neural Networks.
 * 
 * Copyright 2004-2016 Colin Green (sharpneat@gmail.com)
 *
 * SharpNEAT is free software; you can redistribute it and/or modify
 * it under the terms of The MIT License (MIT).
 *
 * You should have received a copy of the MIT License
 * along with SharpNEAT; if not, see https://opensource.org/licenses/MIT.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
// To use ActiveUsersList:
using Evolution.UsersInfo;
using SharpNeat.Genomes.Neat;

namespace SharpNeat.Core
{
    /// <summary>
    /// A concrete implementation of IGenomeListEvaluator that evaluates genomes independently of each 
    /// other and in parallel (on multiple execution threads).
    /// 
    /// Genome decoding is performed by a provided IGenomeDecoder.
    /// Phenome evaluation is performed by a provided IPhenomeEvaluator.
    /// </summary>
    public class ParallelGenomeListEvaluator<TGenome, TPhenome> : IGenomeListEvaluator<TGenome>
        where TGenome : class, IGenome<TGenome>
        where TPhenome : class
    {
        readonly IGenomeDecoder<TGenome, TPhenome> _genomeDecoder;

        // TODO: If we create N evaluators (one per thread) then the evaluators can have persistent state, which may allow faster execution (i.e. re-use of allocated memory).
        readonly IPhenomeEvaluator<TPhenome> _phenomeEvaluator;
        readonly ParallelOptions _parallelOptions;
        readonly bool _enablePhenomeCaching;
        readonly EvaluationMethod _evalMethod;

        delegate void EvaluationMethod(IList<TGenome> genomeList, string userName);

        #region Constructors

        /// <summary>
        /// Construct with the provided IGenomeDecoder and IPhenomeEvaluator. 
        /// Phenome caching is enabled by default.
        /// The number of parallel threads defaults to Environment.ProcessorCount.
        /// </summary>
        public ParallelGenomeListEvaluator(IGenomeDecoder<TGenome, TPhenome> genomeDecoder,
                                           IPhenomeEvaluator<TPhenome> phenomeEvaluator)
            : this(genomeDecoder, phenomeEvaluator, new ParallelOptions(), true)
        {
        }

        /// <summary>
        /// Construct with the provided IGenomeDecoder, IPhenomeEvaluator and ParalleOptions.
        /// Phenome caching is enabled by default.
        /// The number of parallel threads defaults to Environment.ProcessorCount.
        /// </summary>
        public ParallelGenomeListEvaluator(IGenomeDecoder<TGenome, TPhenome> genomeDecoder,
                                           IPhenomeEvaluator<TPhenome> phenomeEvaluator,
                                           ParallelOptions options)
            //: this(genomeDecoder, phenomeEvaluator, options, true)
            : this(genomeDecoder, phenomeEvaluator, options, false)
        {
        }

        /// <summary>
        /// Construct with the provided IGenomeDecoder, IPhenomeEvaluator, ParalleOptions and enablePhenomeCaching flag.
        /// </summary>
        public ParallelGenomeListEvaluator(IGenomeDecoder<TGenome, TPhenome> genomeDecoder,
                                           IPhenomeEvaluator<TPhenome> phenomeEvaluator,
                                           ParallelOptions options,
                                           bool enablePhenomeCaching)
        {
            _genomeDecoder = genomeDecoder;
            _phenomeEvaluator = phenomeEvaluator;
            _parallelOptions = options;
            _enablePhenomeCaching = enablePhenomeCaching;

            // Determine the appropriate evaluation method.
            if (_enablePhenomeCaching)
            {
                _evalMethod = Evaluate_Caching;
            }
            else
            {
                _evalMethod = Evaluate_NonCaching;
            }
        }

        #endregion

        #region IGenomeListEvaluator<TGenome> Members

        /// <summary>
        /// Gets the total number of individual genome evaluations that have been performed by this evaluator.
        /// </summary>
        public ulong EvaluationCount
        {
            get { return _phenomeEvaluator.EvaluationCount; }
        }

        /// <summary>
        /// Gets a value indicating whether some goal fitness has been achieved and that
        /// the evolutionary algorithm/search should stop. This property's value can remain false
        /// to allow the algorithm to run indefinitely.
        /// </summary>
        public bool StopConditionSatisfied
        {
            get { return _phenomeEvaluator.StopConditionSatisfied; }
        }

        /// <summary>
        /// Reset the internal state of the evaluation scheme if any exists.
        /// </summary>
        public void Reset()
        {
            _phenomeEvaluator.Reset();
        }

        /// <summary>
        /// Evaluates a list of genomes. Here we decode each genome in using the contained IGenomeDecoder
        /// and evaluate the resulting TPhenome using the contained IPhenomeEvaluator.
        /// </summary>
        public void Evaluate(IList<TGenome> genomeList, string userName)
        {
            // This method modifies a static dictionary, so it can't be used in the parallel loop
            Malmo.ProgramMalmo.UpdateUserToPortDictionary(userName);
            if (ActiveUsersList<NeatGenome>.PortsPerUser < ActiveUsersList<NeatGenome>.PopulationSize)
            {
                ProcessListInSmallParts(genomeList, userName);
            }
            else
            {
                _evalMethod(genomeList, userName);
            }
        }

        #endregion

        #region Private Methods

        private void ProcessListInSmallParts(IList<TGenome> genomeList, string userName)
        {
            int currentIndex = 0;
            int topIndex = ActiveUsersList<NeatGenome>.PortsPerUser;
            bool exit = false;
            while (!exit)
            {
                List<TGenome> partialList = new List<TGenome>();
                // No initialization in the loop, so that the value of currentIndex
                // is not reset in the next iteration of the while loop
                for (; currentIndex < topIndex; ++currentIndex)
                {
                    if (currentIndex < genomeList.Count)
                    {
                        partialList.Add(genomeList[currentIndex]);
                    }
                    else
                    {
                        exit = true;
                    }
                }
                topIndex += ActiveUsersList<NeatGenome>.PortsPerUser;
                _evalMethod(partialList, userName);
            }
        }

        /// <summary>
        /// Main genome evaluation loop with no phenome caching (decode on each loop).
        /// </summary>
        private void Evaluate_NonCaching(IList<TGenome> genomeList, string userName)
        {
            Dictionary<TGenome, TPhenome> genomeToPhenome = new Dictionary<TGenome, TPhenome>();
            foreach (TGenome genome in genomeList)
            {
                TPhenome phenome = _genomeDecoder.Decode(genome);
                genomeToPhenome.Add(genome, phenome);
            }
            Dictionary<string, int> genomeToPort = CreateGenomeToPort(genomeList);
            EvaluateList(genomeToPhenome, genomeToPort, userName);
        }

        /// <summary>
        /// Main genome evaluation loop with phenome caching (decode only if no cached phenome is present
        /// from a previous decode).
        /// </summary>
        private void Evaluate_Caching(IList<TGenome> genomeList, string userName)
        {
            Dictionary<TGenome, TPhenome> genomeToPhenome = new Dictionary<TGenome, TPhenome>();
            foreach (TGenome genome in genomeList)
            {
                TPhenome phenome = (TPhenome)genome.CachedPhenome;
                if (null == phenome)
                {   // Decode the phenome and store a ref against the genome.
                    phenome = _genomeDecoder.Decode(genome);
                    genome.CachedPhenome = phenome;
                }
                genomeToPhenome.Add(genome, phenome);
            }
            Dictionary<string, int> genomeToPort = CreateGenomeToPort(genomeList);
            EvaluateList(genomeToPhenome, genomeToPort, userName);
        }

        private Dictionary<string, int> CreateGenomeToPort(IList<TGenome> genomeList)
        {
            Dictionary<string, int> dictionary = new Dictionary<string, int>();
            for (int i = 0; i < genomeList.Count; ++i)
            {
                //System.Diagnostics.Debug.WriteLine("Adding " + genomeList[i].CandidateName + ", " + i);
                dictionary.Add(genomeList[i].CandidateName, i);
            }
            return dictionary;
        }

        /// <summary>
        /// The phenomes have all been found before, because they require accessing the decoder, and this 
        /// causes trouble with parallelization. genomeToPort is introduced because genomeList.IndexOf(genome)
        /// was returning out of range exceptions
        /// </summary>
        private void EvaluateList(Dictionary<TGenome, TPhenome> genomeToPhenome, Dictionary<string, int> genomeToPort, string userName)
        {            
            List<TGenome> genomeList = new List<TGenome>(genomeToPhenome.Keys);
            Parallel.ForEach(genomeList, _parallelOptions, delegate (TGenome genome)
            {
                try
                {
                    // random wait?
                    TPhenome phenome = genomeToPhenome[genome];
                    int genomeIndex = genomeToPort[genome.CandidateName];
                    ParallelEvaluationParameters parameters = new ParallelEvaluationParameters(genome.CandidateName, userName, genomeIndex);
                    EvaluateOne(genome, phenome, parameters);
                }
                catch (System.Exception ex)
                {
                    string error = "Exception occurred in parallel evaluation: skipping evaluation! " + ex.Message;
                    System.Diagnostics.Debug.WriteLine(error);
                }
            });
        }

        private void EvaluateOne(TGenome genome, TPhenome phenome, ParallelEvaluationParameters parameters)
        {
            if (null == phenome)
            {   // Non-viable genome.
                System.Diagnostics.Debug.WriteLine("Non-viable phenome found");
                genome.EvaluationInfo.SetFitness(0.0);
                genome.EvaluationInfo.AuxFitnessArr = null;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Evaluate one calls pseudoevaluator.");
                FitnessInfo fitnessInfo = _phenomeEvaluator.Evaluate(phenome, parameters);
                // We don't want to update fitness in this way in the website-based project.
                // See evolution algorithm.
                //genome.EvaluationInfo.SetFitness(fitnessInfo._fitness);
                //genome.EvaluationInfo.AuxFitnessArr = fitnessInfo._auxFitnessArr;
            }
        }

		#endregion
	}
}