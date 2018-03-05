using System;
using SharpNeat.Core;
using SharpNeat.Phenomes;
using Malmo;
using MalmoObservations;

namespace SharpNeat
{
	/// <summary>
	/// This is called a pseudo-evaluator because this class takes the place of evaluators in classic
    /// NEAT. The typical role of these is to take a phenome (translation of a genome) and return
    /// a fitness value. In this case the class will only create a Malmo simulation and save the
    /// corresponding video for the user to see. It is not a true evaluator since it won't return
    /// a corresponding fitness value (for the moment it will return "0" for all).
	/// </summary>
	public class OnlineMalmoPseudoEvaluator : IPhenomeEvaluator<IBlackBox>
	{
        private ulong evalCount;
		private bool stopConditionSatisfied = false;
        private double fitness = 0;

        public OnlineMalmoPseudoEvaluator()
        {}

		#region IPhenomeEvaluator<IBlackBox> Members

		/// <summary>
		/// Gets the total number of evaluations that have been performed.
		/// </summary>
		public ulong EvaluationCount
		{
			get { return evalCount; }
		}

		/// <summary>
		/// Gets a value indicating whether some goal fitness has been achieved and that
		/// the the evolutionary algorithm/search should stop. This property's value can remain false
		/// to allow the algorithm to run indefinitely.
		/// </summary>
		public bool StopConditionSatisfied
		{
			get { return stopConditionSatisfied; }
		}

        #endregion

        /// <summary>
        /// Implement here the task. In our case, this should be something like
        /// creating a serires of Minecraft simulations for each brain and get
        /// a fitness in each case.
        /// 
        /// If no name is given for the simulation (calling only Evaluate(box))
        /// then en empty name will be used.
        /// </summary>
        /*public FitnessInfo Evaluate(IBlackBox brain, string userName)
		{
            return Evaluate(brain, userName, "DefaultName");
		}*/
        public FitnessInfo Evaluate(IBlackBox brain, string simulationName, string userName)
        {
            ParallelEvaluationParameters parallelParameters = new ParallelEvaluationParameters(simulationName, userName, 0);
            return Evaluate(brain, parallelParameters);
        }
        public FitnessInfo Evaluate(IBlackBox brain, ParallelEvaluationParameters parallelParameters)
        {
            ProgramMalmo programMalmo = new ProgramMalmo();

            // The brain to Malmo controller will be subscribed to the update
            // events of ProgramMalmo, and it will controll the actions of the
            // Minecraft character. Here we only care about evaluating the results
            // of these actions.
            //BrainToMalmoController brainToMalmoController = new BrainToMalmoController(brain, programMalmo);
            //BuilderController brainToMalmoController = new BuilderController(brain, programMalmo);
            MazeControllerNoTurn brainToMalmoController = new MazeControllerNoTurn(brain, programMalmo);
            //MazeControllerWithDiagonals brainToMalmoController = new MazeControllerWithDiagonals(brain, programMalmo);
            ++evalCount;
            
            System.Diagnostics.Debug.WriteLine("Evaluate one. Simulation name: " + parallelParameters.simulationName +
                                               " with assigned port: " + parallelParameters.assignedPort.ToString());
            System.Diagnostics.Debug.WriteLine("\nEnter evaluation " + evalCount);
            programMalmo.RunMalmo(parallelParameters);
            // FitnessInfo takes an "alternative fitness", so here we simply pass
            // the same fitness value twice.
            System.Diagnostics.Debug.WriteLine("Exit evaluation " + evalCount + "\n");


            brainToMalmoController.CleanUp();
            brainToMalmoController = null;
            return new FitnessInfo(fitness, fitness);
        }

        /// <summary>
        /// This method was used to determine whether fitness was high enough to stop evolution.
        /// In the web interface this will probably correspond to a specific action (like
        /// saving a result).
        /// </summary>
        /*
        void CheckStopCondition()
        {
            if (condition)
            {
                stopConditionSatisfied = true;         
        
            }
        }
        */
        
        /// <summary>
        /// Resets the internal state of the evaluation scheme if any exists.
        /// </summary>
        public void Reset()
		{}

        /*
        void WhenObservationsEvent(object sender, ObservationEventArgs eventArguments)
        {}

        void WhenMissionEndEvent(object sender, ObservationEventArgs eventArguments)
        {}
        */
	}
}