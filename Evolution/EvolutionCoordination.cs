using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpNeat;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using SharpNeat.IecEsp;
using System.Threading;
using Evolution.UsersInfo;

/// <summary>
/// This class has become absurd by including "userName" in almost every method.
/// A potential solution is to make this not a static class and create some "user" object.
/// Creating a global "user" is not good because different sessions will have different user names!
/// </summary>
namespace Evolution
{
    public static class EvolutionCoordination
    {
        static EvolutionCoordination() {}

        public static void Main()
        {}

        public static void OnStartEvolution(object sender, EvolutionEventsArgs eventArguments)
        {
            StartOrRestartEvolution(eventArguments.userName);
        }

        static void StartOrRestartEvolution(string userName)
        {
            // If the user name is not registered, then it is a new process and we need to 
            // create the evolution algorithm.
            if (!ActiveUsersList<NeatGenome>.DoesUserHaveEvolAlg(userName))
            {
                System.Diagnostics.Debug.WriteLine("Starting evolution process...");
                TryLaunchEvolution(LaunchEvolution, userName);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Re-starting evolution process...");
                TryLaunchEvolution(ReLaunchEvolution, userName);
            }
        }

        static void TryLaunchEvolution(Action<string> launchDelegate, string userName)
        {
            if (!ActiveUsersList<NeatGenome>.ContainsUser(userName) ||
                !ActiveUsersList<NeatGenome>.IsUserRunning(userName))
            {
                LaunchActionInThread(launchDelegate, userName);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Evolution is already running: skipping start.");
            }
        }

        private static void LaunchActionInThread(Action<string> myMethod, string userName)
        {
            Thread threadDelegate = new Thread(() => myMethod(userName));
            // A background thread does not need to finish for the main program to end.
            threadDelegate.IsBackground = true;
            threadDelegate.Priority = ThreadPriority.BelowNormal;
            threadDelegate.Start();
        }

        public static void CheckEvolution(string userName)
        {
            System.Diagnostics.Debug.WriteLine("Legacy method, not curretnly implemented");
        }

        public static void OnBranch(object sender, EvolutionEventsArgs eventArguments)
        {
            // If evolution has not been created or is not running this will not have effect!
            TryStopEvolution(eventArguments.userName);  
            // TODO: Probably unnecessary wait.
            Thread.Sleep(500);
            StartOrRestartEvolution(eventArguments.userName);
        }

        public static void OnResetEvolution(object sender, EvolutionEventsArgs eventArguments)
        {
            TryStopEvolution(eventArguments.userName);
            PopulationReadWrite.DeleteLocalEvolutionFiles(eventArguments.userName);
            // TODO: Probably unnecessary wait.
            Thread.Sleep(500);
            StartOrRestartEvolution(eventArguments.userName);
        }

        public static void OnStopEvolution(object sender, EvolutionEventsArgs eventArguments)
        {
            TryStopEvolution(eventArguments.userName);
        }

        private static void TryStopEvolution(string userName)
        {
            // We should not be able to ask to stop a process that has not started!
            // (And thus we ALWAYS expect this check to be true)
            if (ActiveUsersList<NeatGenome>.ContainsUser(userName))
            {
                if (ActiveUsersList<NeatGenome>.IsUserRunning(userName))
                {
                    System.Diagnostics.Debug.WriteLine("Stopping evolution process...");
                    ActiveUsersList<NeatGenome>.SetRunningStatus(userName, false);
                    ActiveUsersList<NeatGenome>.EvolutionAlgorithmForUser(userName).StopEvolution();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Evolution is already stopped: skipping stop.");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Requested to stop " + userName + " but it was not found in the dictionary!");
            }
        }

        static void ReLaunchEvolution(string userName)
        {
            ReInitialize(userName);
            System.Diagnostics.Debug.WriteLine("ReInitialize done");
            SetEvolutionStatusRunning(userName);
            StartAndRunEvolution(userName);
            System.Diagnostics.Debug.WriteLine("StartAndRunEvolution done");
        }

        static void LaunchEvolution(string userName)
        {
            Initialize(userName);
            SetEvolutionStatusRunning(userName);
            StartAndRunEvolution(userName);
        }

        static void SetEvolutionStatusRunning(string userName)
        {
            ActiveUsersList<NeatGenome>.SetRunningStatus(userName, true);
            System.Diagnostics.Debug.WriteLine("User: " + userName + ". Start evolution: " + DateTime.Now.ToString() + "\n");
        }

        static void StartAndRunEvolution(string userName)
        {
            StartEvolution(userName);
            while (ActiveUsersList<NeatGenome>.IsUserRunning(userName)) { }
            System.Diagnostics.Debug.WriteLine("User: " + userName + ". End evolution: " + DateTime.Now.ToString());
        }

        static void Initialize(string userName)
        {
            MyExperiment currentExperiment = CreateNewExperiment(userName);
            InitializeEvolutionAlgorithm(currentExperiment, userName);
            SaveDataIfRunning(userName);
            AddFirstModuleIfNeeded(userName);
        }

        static void ReInitialize(string userName)
        {
            MyExperiment currentExperiment = CreateNewExperiment(userName);
            var myEvolutionAlgorithm = ActiveUsersList<NeatGenome>.EvolutionAlgorithmForUser(userName);
            string populationPath = PopulationReadWrite.PopulationPathForUser(userName);
            currentExperiment.ReInitializeEvolution(myEvolutionAlgorithm, populationPath);
            SaveDataIfRunning(userName);
            AddFirstModuleIfNeeded(userName);
            System.Diagnostics.Debug.WriteLine("added module!");
        }

        /// <summary>
        /// My experiment controls what NEAT will do (how genomes will be 
        /// evaluated, their inputs and outputs, etc.)
        /// </summary>
        static MyExperiment CreateNewExperiment(string userName)
        {
            string configFilePath = PopulationReadWrite.GetEvolutionFolderPath() + "Malmo.config.xml";
            return new MyExperiment(configFilePath, userName);
        }

        static void InitializeEvolutionAlgorithm(MyExperiment currentExperiment, string userName)
        {
            // Passing a file path the new evolution algorithm will try to read an existing population.
            string posiblePopulationFile = PopulationReadWrite.PopulationPathForUser(userName);
            // This step will create the evolution algorithm and pass the userName (which currentExperiment already has)
            // to the algorithm constructor, which will add the pair (algorithm, user) to ActiveUsersList
            NeatEvolutionAlgorithm<NeatGenome> evolutionAlgorithm = currentExperiment.CreateEvolutionAlgorithm(posiblePopulationFile);
            evolutionAlgorithm.UpdateEvent += new EventHandler<EvolutionEventsArgs>(WhenUpdateEvent);
            evolutionAlgorithm.PausedEvent += new EventHandler<EvolutionEventsArgs>(WhenPauseEvent);
        }

        static public void DessubscribeEventsFromUser(string userName)
        {
            NeatEvolutionAlgorithm<NeatGenome> evolutionAlgorithm = null;
            try
            {
                evolutionAlgorithm = ActiveUsersList<NeatGenome>.EvolutionAlgorithmForUser(userName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Null algorithm at DessubscribeEventsFromUser in EvolutionCoordination: " + ex.Message);
            }
            if (evolutionAlgorithm != null)
            {
                evolutionAlgorithm.UpdateEvent -= new EventHandler<EvolutionEventsArgs>(WhenUpdateEvent);
                evolutionAlgorithm.PausedEvent -= new EventHandler<EvolutionEventsArgs>(WhenPauseEvent);
            }
        }

        /// <summary>
        /// Newly created genomes will only have an empty "carcass" with some 
        /// basic elements (input and output connections and a regulatoty neuron).
        /// We need to create a module with all the internal connections. The most
        /// basic module will include "local input" and "local output" neurons as
        /// interface between all inputs and outputs (in the future it will be
        /// possible to connect only to a subset of these).
        /// 
        /// This step is not necessary if an old (complete) population has been
        /// loaded.
        /// </summary>
        static void AddFirstModuleIfNeeded(string userName)
        {
            if (CheckNewModuleNeeded(userName))
            {
                NeatEvolutionAlgorithm<NeatGenome> evolutionAlgorithm = ActiveUsersList<NeatGenome>.EvolutionAlgorithmForUser(userName);
                // If a first module is needed, then all genomes are equal (yet).
                // We ensure the champion is a valid genome:
                evolutionAlgorithm.CurrentChampGenome = evolutionAlgorithm.GenomeList[0];
                evolutionAlgorithm.AddFirstModule();
            }
        }
        
        /// <summary>
        /// Checks the module ID of the last neuron in one (the first) genome of
        /// the population. If (and only if) this is "0" this means the genome
        /// is not complete, and so a new (first) module is needed.
        /// </summary>
        static bool CheckNewModuleNeeded(string userName)
        {
            NeatEvolutionAlgorithm<NeatGenome> evolutionAlgorithm = ActiveUsersList<NeatGenome>.EvolutionAlgorithmForUser(userName);
            int latestModuleID;
            int lastNeuronIndex = evolutionAlgorithm.GenomeList[0].NeuronGeneList.Count - 1;
            latestModuleID = evolutionAlgorithm.GenomeList[0].NeuronGeneList[lastNeuronIndex].ModuleId;
            return latestModuleID == 0;
        }

        static void WhenUpdateEvent(object sender, EvolutionEventsArgs arguments)
        {
            string userName = arguments.userName;
            NeatEvolutionAlgorithm<NeatGenome> evolutionAlgorithm = null;
            try
            {
                evolutionAlgorithm = ActiveUsersList<NeatGenome>.EvolutionAlgorithmForUser(userName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Null algorithm at WhenUpdateEvent in EvolutionCoordination: " + ex.Message);
            }
            if (evolutionAlgorithm != null)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("gen={0:N0} bestFitness={1:N6}",
                                                   evolutionAlgorithm.CurrentGeneration,
                                                   evolutionAlgorithm.Statistics._maxFitness));
                SaveDataIfRunning(userName);
            }
        }

        static void WhenPauseEvent(object sender, EvolutionEventsArgs arguments)
        {
            string userName = arguments.userName;
            // In this version we decide to stop the program at a pause event.
            // evolutionRunning = false here seems reiterative? (see TryStopEvolution)
            ActiveUsersList<NeatGenome>.SetRunningStatus(userName, false);
            SaveDataIfRunning(userName);
        }

        static void StartEvolution(string userName)
        {
            NeatEvolutionAlgorithm<NeatGenome> evolutionAlgorithm = ActiveUsersList<NeatGenome>.EvolutionAlgorithmForUser(userName);
            evolutionAlgorithm.MakeEvolutionReady();
            System.Diagnostics.Debug.WriteLine("\nEvolution ready, starting iterations");
            SaveDataIfRunning(userName);            
            if (ActiveUsersList<NeatGenome>.IsUserRunning(userName))
            {
                evolutionAlgorithm.StartContinue();
            }
        }

        static bool isZero(int number)
        {
            return number == 0;
        }

        /// <summary>
        /// This is specially useful since it is possible that startevolution will be called
        /// after stop evolution (because stopping is not instantaneous!)
        /// </summary>
        static void SaveDataIfRunning(string userName)
        {
            if (ActiveUsersList<NeatGenome>.IsUserRunning(userName))
            {
                PopulationReadWrite.SavePopulation(userName);
            }
        }
    }
}
