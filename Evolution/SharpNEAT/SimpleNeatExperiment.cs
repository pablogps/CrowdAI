/* ***************************************************************************
 * This file is part of the NashCoding tutorial on SharpNEAT 2.
 * 
 * Copyright 2010, Wesley Tansey (wes@nashcoding.com)
 * 
 * Some code in this file may have been copied directly from SharpNEAT,
 * for learning purposes only. Any code copied from SharpNEAT 2 is 
 * copyright of Colin Green (sharpneat@gmail.com).
 *
 * Both SharpNEAT and this tutorial are free software: you can redistribute
 * it and/or modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of the 
 * License, or (at your option) any later version.
 *
 * SharpNEAT is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with SharpNEAT.  If not, see <http://www.gnu.org/licenses/>.
 */
using System.Collections.Generic;
using SharpNeat.Domains;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using SharpNeat.Decoders;
using System.Threading.Tasks;
using SharpNeat.Core;
using SharpNeat.EvolutionAlgorithms.ComplexityRegulation;
using SharpNeat.Decoders.Neat;
using SharpNeat.Phenomes;
using SharpNeat.DistanceMetrics;
using SharpNeat.SpeciationStrategies;
using System.Xml;
using System.IO;
// To use ActiveUsersList:
using Evolution.UsersInfo;

namespace SharpNeat
{
    /// <summary>
    /// Helper class that hides most of the details of setting up an experiment.
    /// If you're just doing a simple console-based experiment, this is probably
    /// what you want to inherit from. However, if you need more flexibility
    /// (e.g., custom genome/phenome creation or performing complex population
    /// evaluations) then you probably want to implement your own INeatExperiment
    /// class.
    /// </summary>
    public abstract class SimpleNeatExperiment : INeatExperiment
    {
        NeatEvolutionAlgorithmParameters _eaParams;
        NeatGenomeParameters _neatGenomeParams;
        NetworkActivationScheme _activationScheme;
        string _name;
        int _populationSize;
        int _specieCount;
        string _complexityRegulationStr;
        int? _complexityThreshold;
        string _description;
        ParallelOptions _parallelOptions;

        string userName;

        #region Abstract properties that subclasses must implement
        public abstract IPhenomeEvaluator<IBlackBox> PhenomeEvaluator { get; }
        public abstract int InputCount { get; }
        public abstract int OutputCount { get; }
        #endregion        

        #region INeatExperiment Members

        public string Description
        {
            get { return _description; }
        }

        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Gets the default population size to use for the experiment.
        /// </summary>
        public int DefaultPopulationSize
        {
            get { return _populationSize; }
        }

        /// <summary>
        /// Gets the NeatEvolutionAlgorithmParameters to be used for the experiment.
        /// Parameters on this object can be modified. Calls to CreateEvolutionAlgorithm()
        /// make a copy of and use this object in whatever state it is in at the time of the call.
        /// </summary>
        public NeatEvolutionAlgorithmParameters NeatEvolutionAlgorithmParameters
        {
            get { return _eaParams; }
        }

        /// <summary>
        /// Gets the NeatGenomeParameters to be used for the experiment. Parameters
        /// on this object can be modified. Calls to CreateEvolutionAlgorithm()
        /// make a copy of and use this object in whatever state it is in at the time of the call.
        /// </summary>
        public NeatGenomeParameters NeatGenomeParameters
        {
            get { return _neatGenomeParams; }
        }

        #endregion

        #region INeatExperiment Methods

        /// <summary>
        /// Initialize the experiment with some optional XML configutation data.
        /// </summary>
        public void Initialize(string name, XmlElement xmlConfig)
        { Initialize(name, xmlConfig, "noUserNameFound"); }
        public void Initialize(string name, XmlElement xmlConfig, string givenUserName)
        {
            _name = name;
            _populationSize = XmlUtils.GetValueAsInt(xmlConfig, "PopulationSize");
            ActiveUsersList<NeatGenome>.PopulationSize = _populationSize;
            _specieCount = XmlUtils.GetValueAsInt(xmlConfig, "SpecieCount");
            _activationScheme = ExperimentUtils.CreateActivationScheme(xmlConfig, "Activation");
            _complexityRegulationStr = XmlUtils.TryGetValueAsString(xmlConfig, "ComplexityRegulationStrategy");
            _complexityThreshold = XmlUtils.TryGetValueAsInt(xmlConfig, "ComplexityThreshold");
            _description = XmlUtils.TryGetValueAsString(xmlConfig, "Description");
            ActiveUsersList<NeatGenome>.MaxNumberOfUsers =
                    XmlUtils.GetValueAsInt(xmlConfig, "MaxSimultaneousUsers");
            ActiveUsersList<NeatGenome>.PortsPerUser =
                    XmlUtils.GetValueAsInt(xmlConfig, "PortsPerUser");
            _parallelOptions = ExperimentUtils.ReadParallelOptions(xmlConfig);
            _eaParams = new NeatEvolutionAlgorithmParameters();
            _eaParams.SpecieCount = _specieCount;
            _neatGenomeParams = new NeatGenomeParameters();
            System.Diagnostics.Debug.Assert(CheckActivationScheme(_activationScheme));
            userName = givenUserName;
        }

        bool CheckActivationScheme(NetworkActivationScheme scheme)
        {
            if (!scheme.Esp)
            {
                System.Console.WriteLine("\nOnly EspCyclic activation scheme is compatible " +
                                         "with this version.\n");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Save a population of genomes to an XmlWriter.
        /// </summary>
        public void SavePopulation(XmlWriter xw, IList<NeatGenome> genomeList)
        {
            // Writing node IDs is not necessary for NEAT.
            NeatGenomeXmlIO.WriteComplete(xw, genomeList, false);
        }

        /// <summary>
        /// Create a genome2 factory for the experiment.
        /// Create a genome2 factory with our neat genome2 parameters object and
        /// the appropriate number of input and output neuron genes.
        /// </summary>
        public IGenomeFactory<NeatGenome> CreateGenomeFactory()
        {
            return new NeatGenomeFactory(InputCount, OutputCount, _neatGenomeParams);
        }

        public void ReInitializeEvolution(NeatEvolutionAlgorithm<NeatGenome> oldEvolutionAlgorithm, string populationPath)
        {
            List<NeatGenome> genomeList = null;
            genomeList = LoadPopulationFromFile(populationPath);
            if (genomeList == null)
            {
                genomeList = oldEvolutionAlgorithm.Factory.CreateGenomeList(_populationSize, 0);
            }
            oldEvolutionAlgorithm.ResetPopulation(genomeList);
        }

        /// <summary>
        /// Create and return a NeatEvolutionAlgorithm object ready for running
        /// the NEAT algorithm/search. Various sub-parts of the algorithm are also
        /// constructed and connected up. This overload requires no parameters
        /// and uses the default population size.
        /// </summary>
        public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm()
        {
            return CreateEvolutionAlgorithm(DefaultPopulationSize);
        }

        /// <summary>
        /// Create and return a NeatEvolutionAlgorithm object ready for running
        /// the NEAT algorithm/search. Various sub-parts of the algorithm are also
        /// constructed and connected up. This overload accepts a population size
        /// parameter that specifies how many genomes to create in an initial randomly
        /// generated population.
        /// </summary>
        public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm(int populationSize)
        {
            IGenomeFactory<NeatGenome> genomeFactory = CreateGenomeFactory();
			List<NeatGenome> genomeList = genomeFactory.CreateGenomeList(populationSize, 0);
            return CreateEvolutionAlgorithm(genomeFactory, genomeList);
        }

        /// <summary>
        /// Tries to load a genome population using a given file path. If this is
        /// unsuccessful then calls to the default method.
        /// </summary>
        public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm(string fileName)
        {
        	IGenomeFactory<NeatGenome> genomeFactory = CreateGenomeFactory();
            List<NeatGenome> genomeList = null;
        	genomeList = LoadPopulationFromFile(fileName);
            if (genomeList == null)
        	{
                return CreateEvolutionAlgorithm();
        	}
            NeatEvolutionAlgorithm<NeatGenome> evolutionAlgorithm = CreateEvolutionAlgorithm(genomeFactory, genomeList);
            evolutionAlgorithm.FindLastGeneration();
            return evolutionAlgorithm;
        }

		List<NeatGenome> LoadPopulationFromFile(string fileName)
		{
			List<NeatGenome> returnList = null;
			if (File.Exists(fileName))
			{
				returnList = ReadXMLfile(fileName);
			}
			return returnList;
		}

		List<NeatGenome> ReadXMLfile(string fileName)
		{
			using (XmlReader xr = XmlReader.Create(fileName))
			{
				// LoadPopulation is a method imposed by the interface.
				return LoadPopulation(xr);
			}
		}

        /// <summary>
        /// Loads a population of genomes from an XmlReader and returns the genomes in a new list.
        /// The genome factory for the genomes can be obtained from any one of the genomes.
        /// </summary>
        public List<NeatGenome> LoadPopulation(XmlReader xr)
        {
        	NeatGenomeFactory genomeFactory = (NeatGenomeFactory)CreateGenomeFactory();
        	return NeatGenomeXmlIO.ReadCompleteGenomeList(xr, false, genomeFactory);
        }

        /*
		List<NeatGenome> CreateNewGenome(IGenomeFactory<NeatGenome> genomeFactory)
		{
			Console.WriteLine("Saved genome not found, creating new files.");
			return genomeFactory.CreateGenomeList(_populationSize, 0);
		}
		*/

        /// <summary>
        /// Creates and returns a NeatEvolutionAlgorithm object ready for running
        /// the NEAT algorithm/search. Various sub-parts of the algorithm are also
        /// constructed and connected up. This overload accepts a pre-built genome2
        /// population and their associated/parent genome2 factory.
        /// </summary>
        public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm(
                IGenomeFactory<NeatGenome> genomeFactory, List<NeatGenome> genomeList)
        {

            // Creates distance metric. Mismatched genes have a fixed distance of 10;
            // for matched genes the distance is their weigth difference.
            IDistanceMetric distanceMetric = new ManhattanDistanceMetric(1.0, 0.0, 10.0);

            //ISpeciationStrategy<NeatGenome> speciationStrategy = new KMeansClusteringStrategy<NeatGenome>(distanceMetric);
            ISpeciationStrategy<NeatGenome> speciationStrategy = new ParallelKMeansClusteringStrategy<NeatGenome>(distanceMetric, _parallelOptions);

            IComplexityRegulationStrategy complexityRegulationStrategy =
                    ExperimentUtils.CreateComplexityRegulationStrategy(_complexityRegulationStr, _complexityThreshold);
            // Creates the evolution algorithm.
            NeatEvolutionAlgorithm<NeatGenome> evolAlgorithm = new NeatEvolutionAlgorithm<NeatGenome>(
                    _eaParams, speciationStrategy, complexityRegulationStrategy, userName);
			IGenomeDecoder<NeatGenome, IBlackBox> genomeDecoder = new NeatGenomeDecoder(_activationScheme); 
            // Creates a genome2 list evaluator. This packages up the genome2 decoder with the genome2 evaluator.
            IGenomeListEvaluator<NeatGenome> genomeListEvaluator =
                    new ParallelGenomeListEvaluator<NeatGenome, IBlackBox>(genomeDecoder, PhenomeEvaluator, _parallelOptions);
            //To use single-thread evaluator:
            //IGenomeListEvaluator<NeatGenome> genomeListEvaluator =
            //        new SerialGenomeListEvaluator<NeatGenome, IBlackBox>(genomeDecoder, PhenomeEvaluator, false);
            // Wraps the list evaluator in a 'selective' evaulator that will only
            // evaluate new genomes. That is, we skip re-evaluating any genomes
            // that were in the population in previous generations (elite genomes).
            // This is determiend by examining each genome's evaluation info object.
            /*
            int reevaluationPeriod = 1;
            genomeListEvaluator = new SelectiveGenomeListEvaluator<NeatGenome>(
                    genomeListEvaluator,
                    SelectiveGenomeListEvaluator<NeatGenome>.CreatePredicate_PeriodicReevaluation(reevaluationPeriod));
            */
            genomeListEvaluator = new SelectiveGenomeListEvaluator<NeatGenome>(
                    genomeListEvaluator,
                    SelectiveGenomeListEvaluator<NeatGenome>.CreatePredicate_OnceOnly());
            // Initializes the evolution algorithm.
            evolAlgorithm.Initialize(genomeListEvaluator, genomeFactory, genomeList);
            // Finished. Return the evolution algorithm
            return evolAlgorithm;
        }

        /// <summary>
        /// Creates a new genome decoder that can be used to convert a genome into a phenome.
        /// </summary>
        public IGenomeDecoder<NeatGenome, IBlackBox> CreateGenomeDecoder()
        {
            return new NeatGenomeDecoder(_activationScheme);
        }
        
        #endregion
    }
}
