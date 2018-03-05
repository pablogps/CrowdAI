/* ***************************************************************************
 * This file is part of SharpNEAT - Evolution of Neural Networks.
 * 
 * Copyright 2004-2006, 2009-2010 Colin Green (sharpneat@gmail.com)
 *
 * SharpNEAT is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * SharpNEAT is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with SharpNEAT.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using SharpNeat.Core;
using System.Threading;

// Disable missing comment warnings for non-private variables.
#pragma warning disable 1591

namespace SharpNeat.EvolutionAlgorithms
{
    /// <summary>
    /// Abstract class providing some common/baseline data and methods for 
    /// implementions of IEvolutionAlgorithm.
    /// </summary>
    /// <typeparam name="TGenome">The genome type that the algorithm will operate on.</typeparam>
    public abstract class AbstractGenerationalAlgorithm<TGenome> : IEvolutionAlgorithm<TGenome>
        where TGenome : class, IGenome<TGenome>
    {
        //private static readonly ILog __log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #region Instance Fields
        protected string userName;
        public string UserName
        {
            get { return userName; }
            set { userName = value; }
        }

        protected IGenomeListEvaluator<TGenome> _genomeListEvaluator;
        protected IGenomeFactory<TGenome> _genomeFactory;
        protected List<TGenome> _genomeList;
        protected int _populationSize;
        protected TGenome _currentBestGenome;

        // Algorithm state data.
        protected RunState _runState = RunState.NotReady;
        protected uint _currentGeneration;

        // Update event scheme / data.
        UpdateScheme _updateScheme;
        uint _prevUpdateGeneration;
        long _prevUpdateTimeTick;

        // Misc working variables.        
		Thread _algorithmThread;
        protected bool _pauseRequestFlag;
		bool _terminateFlag = false;
        // AutoResetEvent: "Notifies a waiting thread that an event has occurred."
        readonly AutoResetEvent _awaitPauseEvent = new AutoResetEvent(false);
        readonly AutoResetEvent _awaitRestartEvent = new AutoResetEvent(false);

        #endregion

        #region Events

        /// <summary>
        /// Notifies listeners that some state change has occured.
        /// Current listeners: optimizer.
        /// </summary>
        public event EventHandler<Evolution.EvolutionEventsArgs> UpdateEvent;
        /// <summary>
        /// Notifies listeners that the algorithm has paused.
        /// Current listeners: optimizer.
        /// </summary>
        public event EventHandler<Evolution.EvolutionEventsArgs> PausedEvent;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current generation.
        /// </summary>
        public uint CurrentGeneration
        {
            get { return _currentGeneration; }
            set { _currentGeneration = value; }
        }

        public IGenomeFactory<TGenome> Factory
        {
            get { return _genomeFactory; }
        }

        #endregion

        #region IEvolutionAlgorithm<TGenome> Members

        /// <summary>
        /// Gets or sets the algorithm's update scheme.
        /// </summary>
        public UpdateScheme UpdateScheme 
        {
            get { return _updateScheme; }
            set { _updateScheme = value; }
        }

        /// <summary>
        /// Gets the current execution/run state of the IEvolutionAlgorithm.
        /// </summary>
        public RunState RunState
        {
            get { return _runState; }
        }

        /// <summary>
        /// Gets the population's current champion genome.
        /// </summary>
        public TGenome CurrentChampGenome 
        {
            get { return _currentBestGenome; }
            set { _currentBestGenome = value; }
        }

        /// <summary>
        /// Gets a value indicating whether some goal fitness has been achieved 
        /// and that the algorithm has therefore stopped.
        /// </summary>
        public bool StopConditionSatisfied 
        { 
            get { return _genomeListEvaluator.StopConditionSatisfied; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the evolution algorithm with the provided IGenomeListEvaluator, 
        /// IGenomeFactory and an initial population of genomes.
        /// </summary>
        /// <param name="genomeListEvaluator">The genome evaluation scheme for 
        /// the evolution algorithm.</param>
        /// <param name="genomeFactory">The factory that was used to create the 
        /// genomeList and which is therefore referenced by the genomes.</param>
        /// <param name="genomeList">An initial genome population.</param>
        public virtual void Initialize(IGenomeListEvaluator<TGenome> genomeListEvaluator,
                                       IGenomeFactory<TGenome> genomeFactory,
                                       List<TGenome> genomeList)
        {
            _currentGeneration = 0;
            _genomeListEvaluator = genomeListEvaluator;
            _genomeFactory = genomeFactory;
            _genomeList = genomeList;
            // We set an arbitrary genome as champion to avoid possible null exceptions.
            _currentBestGenome = _genomeList[0];
            _populationSize = _genomeList.Count;
            _runState = RunState.Ready;
            _updateScheme = new UpdateScheme(new TimeSpan(0, 0, 1));
        }

        /// <summary>
        /// Initializes the evolution algorithm with the provided IGenomeListEvaluator
        /// and an IGenomeFactory that can be used to create an initial population of genomes.
        /// </summary>
        /// <param name="genomeListEvaluator">The genome evaluation scheme for 
        /// the evolution algorithm.</param>
        /// <param name="genomeFactory">The factory that was used to create the 
        /// genomeList and which is therefore referenced by the genomes.</param>
        /// <param name="populationSize">The number of genomes to create for the 
        /// initial population.</param>
        public virtual void Initialize(IGenomeListEvaluator<TGenome> genomeListEvaluator,
                                       IGenomeFactory<TGenome> genomeFactory,
                                       int populationSize)
        {
            _currentGeneration = 0;
            _genomeListEvaluator = genomeListEvaluator;
            _genomeFactory = genomeFactory;
            _genomeList = genomeFactory.CreateGenomeList(populationSize, _currentGeneration);
            // We set an arbitrary genome as champion to avoid possible null exceptions.
            _currentBestGenome = _genomeList[0];
            _populationSize = populationSize;
            _runState = RunState.Ready;
            _updateScheme = new UpdateScheme(new TimeSpan(0, 0, 1));
        }

        /// <summary>
        /// Starts the algorithm running. The algorithm will switch to the 
        /// Running state from either the Ready or Paused states.
        /// </summary>
        public void StartContinue()
        {
            // RunState must be Ready or Paused.
            if (RunState.Ready == _runState)
            { // Creates a new thread and starts it running.
         		_algorithmThread = new Thread(AlgorithmThreadMethod);
                // A background thread does NOT need to finish for the main program
                // to end. So choose whatever works best.
                _algorithmThread.IsBackground = true;
                _algorithmThread.Priority = ThreadPriority.BelowNormal;
                _runState = RunState.Running;
				OnUpdateEvent();
				_algorithmThread.Start();            
            }
            else if (RunState.Paused == _runState)
            { // Thread is paused. Resumes execution.
                _runState = RunState.Running;
                OnUpdateEvent();
                _awaitRestartEvent.Set();
            }
            else if (RunState.Running == _runState)
            { // Already running. Log a warning.
            	//__log.Warn("StartContinue() called but algorithm is already running.");
        		Console.WriteLine("StartContinue() called but algorithm is already running.");
            }
            else
            {
                throw new SharpNeatException(string.Format(
                        "StartContinue() call failed. Unexpected RunState [{0}]", _runState));
            }
        }

        /// <summary>
        /// Alias for RequestPause().
        /// </summary>
        public void Stop()
        {
            RequestPause();
        }

        /// <summary>
        /// Requests that the algorithm pauses but doesn't wait for the algorithm 
        /// thread to stop. The algorithm thread will pause when it is next 
        /// convenient to do so, and will notify listeners via an UpdateEvent.
        /// </summary>
        public void RequestPause()
        {
            if (RunState.Running == _runState) {
                _pauseRequestFlag = true;
            }
            else {
                //__log.Warn("RequestPause() called but algorithm is not running.");
        		Console.WriteLine("RequestPause() called but algorithm is not running.");
            }
        }

        /// <summary>
        /// Request that the algorithm pause and waits for the algorithm to do so. 
        /// The algorithm thread will pause when it is next convenient to do so 
        /// and notifies any UpdateEvent listeners prior to returning control to 
        /// the caller. Therefore it's generally a bad idea to call this method 
        /// from a GUI thread that also has code that may be called by the
        /// UpdateEvent - doing so will result in deadlocked threads.
        /// </summary>
        public void RequestPauseAndWait()
        {
            if (RunState.Running == _runState) 
            {   // Set a flag that tells the algorithm thread to enter the paused state and wait 
                // for a signal that tells us the thread has paused.
                _pauseRequestFlag = true;
                _awaitPauseEvent.WaitOne();
                // .WaitOne(): "Blocks the current thread until the current 
                // WaitHandle receives a signal."
            }
            else 
            {
                //__log.Warn("RequestPauseAndWait() called but algorithm is not running.");
        		Console.WriteLine("RequestPauseAndWait() called but algorithm is not running.");
            }
        }

		public void RequestTerminateAndWait()
		{
			if (RunState.Running == _runState)
			{
				// Signal worker thread to terminate.
				_terminateFlag = true;
				_pauseRequestFlag = true;
				_awaitPauseEvent.WaitOne();
			}
		}

		public void Dispose()
		{
			RequestTerminateAndWait();
		}

        #endregion

        #region Private/Protected Methods [Evolution Algorithm]

        /// <summary>
        /// This function starts the real evolution, although the details of 
        /// each generation are coded in PerformOneGneration, an abstract 
        /// funtcion implemented in the corresponding evolution algorithm 
        /// (for example, NeatEvolutionAlgorithm). 
        /// </summary>
		private void AlgorithmThreadMethod()
		{
			try
			{
				_prevUpdateGeneration = 0;
				_prevUpdateTimeTick = DateTime.Now.Ticks;

				for (;;)
                {
                    // This has been moved to inside the NeatEvolutionAlgorithm.
                    // Otherwise there were problems and inconsistencies when
                    // interactive evolution was aborted, or during the first 
                    // generation (because it starts by entering this loop, while the
                    // others start at some event). It is a bit confusing, but now
                    // everything seems in order.
                    //_currentGeneration++;
                    PerformOneGeneration();

					if (UpdateTest())
					{
						_prevUpdateGeneration = _currentGeneration;
						_prevUpdateTimeTick = DateTime.Now.Ticks;
						OnUpdateEvent();
					}

					// Check if a pause has been requested. 
					// Access to the flag is not thread synchronized, but it doesn't really matter if
					// we miss it being set and perform one other generation before pausing.
					if (_pauseRequestFlag || _genomeListEvaluator.StopConditionSatisfied)
					{
						// Signal to any waiting thread that we are pausing
						_awaitPauseEvent.Set();

						// Test for terminate signal.
						if (_terminateFlag)
						{
							return;
						}
						// Reset the flag. Update RunState and notify any listeners of the state change.
						_pauseRequestFlag = false;
						_runState = RunState.Paused;
						OnUpdateEvent();
						OnPausedEvent();
						// Wait indefinitely for a signal to wake up and continue.
						_awaitRestartEvent.WaitOne();
					}
				}
			}
			catch (ThreadAbortException)
			{   // Quietly exit thread.
			}
		}

        /// <summary>
        /// Returns true if it is time to raise an update event.
        /// </summary>
        private bool UpdateTest()
        {
            if (UpdateMode.Generational == _updateScheme.UpdateMode) {
                return (_currentGeneration - _prevUpdateGeneration) >= _updateScheme.Generations;
            }            
            return (DateTime.Now.Ticks - _prevUpdateTimeTick) >= _updateScheme.TimeSpan.Ticks;
        }

        private void OnUpdateEvent()
        {
            // Always check that there are listeners for the event
            if (null != UpdateEvent)
            {
                // Catch exceptions thrown by event listeners. This prevents 
                // listener exceptions from terminating the algorithm thread.
                try
                {
                    Evolution.EvolutionEventsArgs myArguments = new Evolution.EvolutionEventsArgs();
                    myArguments.userName = userName;
                    UpdateEvent(this, myArguments);
                }
                catch(Exception ex) {
                    //__log.Error("UpdateEvent listener threw exception", ex);
        			Console.WriteLine("UpdateEvent listener threw exception" + ex);
                }
            }
        }

        private void OnPausedEvent()
        {
            if (null != PausedEvent)
            {
                // Catch exceptions thrown by event listeners. This prevents 
                // listener exceptions from terminating the algorithm thread.
                try
                {
                    Evolution.EvolutionEventsArgs myArguments = new Evolution.EvolutionEventsArgs();
                    myArguments.userName = userName;
                    PausedEvent(this, myArguments);
                }
                catch(Exception ex) {
                    //__log.Error("PausedEvent listener threw exception", ex);
        			Console.WriteLine("PausedEvent listener threw exception" + ex);
                }
            }
        }

        /// <summary>
        /// Progress forward by one generation. Perform one generation/cycle of 
        /// the evolution algorithm.
        /// </summary>
        protected abstract void PerformOneGeneration();

        #endregion
    }
}
