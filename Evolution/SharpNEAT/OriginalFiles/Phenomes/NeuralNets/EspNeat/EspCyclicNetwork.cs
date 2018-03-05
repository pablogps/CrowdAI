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
using SharpNeat.Network;

// Disable missing comment warnings for non-private variables.
#pragma warning disable 1591

namespace SharpNeat.Phenomes.NeuralNets
{
    /// <summary>
    /// A phenome is a processed version of a neural network for faster input to
    /// output conversion. This class was derived from FastCyclicNetwork which 
    /// works with classic NEAT. Here we upgrade that class to the more complex
    /// modular architecture of EspNEAT. See the original class for more comments
    /// on the algorithm logic and other strategies to boost performance. Note
    /// that this is by far the single most demanding point of all the code from
    /// the performance point of view, because all units will use the Activate
    /// method every frame during evaluation.
    /// 
    /// We do not have an array with the activation function for each neuron.
    /// They will all use the same (execpt perhaps for the regulatory neurons or
    /// global output neurons, which use their own, but which will be identified
    /// by ther index).
    /// 
    /// Note: It is much faster (about x8 times) to use an array of arrays than
    /// a 2D array (double arrayArray[i][j] vs double array2D[i,j]). Also it is much
    /// faster to have the first index (i) in the outer loop.
    /// 
    /// Neuron sorting is different from the one used in genomes. Here we use:
    ///          -Input
    ///          -Output
    ///          -Regulatory
	///          -Hidden and local output (with targets other than output neurons)
	///          -Local input (with fixed input from bias and input neurons)
	///          -Local input (with local output as sources)
	///          -Local output (with output neurons as only target)
    /// 
    /// Connections will also be classified as follows:
    ///          -Input (and bias) to local input
    ///          -Input and bias to regulatory
    ///          -Non-protected
    ///          -Local output to regulatory
    ///          -Local output to output
    /// 
    /// Algorithm explanation:
    /// 
    /// 1) Loop through the array with input-to-local_input connections. Copies
    /// the bias/input state to the local input neurons. 
    /// 
    /// 2) Loop through the non-protected connections array. Takes the
    /// post-activation activity value from the source of the connection, applies
    /// the weight and adds the result to the pre-activation activity of the 
    /// target.
    /// 2.2) Loop through the input and bias to regulatory connections.
    /// Apply maximum weight.
    /// 
    /// 3) Loop through the matrix with the local_output-to-regulatory connections.
    /// Applies maximum weight (these are protected connections, the weight is
    /// always known and stored in a constant variable) and multiplies by the
    /// regulatory post-activation value. The first dimension of the matrix is
    /// the module and the second its local_output-to-regulatory connections, so
    /// we only need to read the regulatory value one time in each module.
    ///
    /// NOTICE! This connections NEVER use module 0, so moduleIndex = 2
    /// represents the connections in moduleId = 3.
    /// 
    /// 4) Loop through the neurons (through the activation arrays really) and 
    /// use the activation function to translate the pre-activation activity into
    /// post-activation. Reset the pre-activation to 0.
    /// 4.1) Hidden and local_output-to-regulatory neurons (as explained).
    /// 4.2) Local_output-to-output neurons only need to reset the pre-activation
    /// unless we are in the last iteration, where we also compute the
    /// post-activation (which is only used at the end of the activation).
    /// 4.3) Loop through a matrix for regulatory neurons. Array[i][j]: j is the 
    /// index to the regulatory neuron in the pre/post-activation lists. i groups
    /// regulatory neurons in pandemonium groups. In i = 0 we have those that are
    /// not in a pandemonium (for these, do as explained). For other values of 
    /// i we have to check all regulatory neurons (j) (take the chance to reset
    /// both their pre and post-activation values) and find the one with highest
    /// pre-activation value. Assign maximum post-activation to this one (so for
    /// thi one we will first set the post-value to 0 and then to max).
    /// 
    /// 5) Loop through local_output-to-output connections. Post-activation values
    /// from the sources are taken to the targets, applying maximum connection 
    /// weight and modulating with the regulatory neuron value. These connections
    /// are also in a matrix so the regulatory value only needs to be read once
    /// per module. 
    ///  
    /// NOTICE! This connections NEVER use module 0, so moduleIndex = 2
    /// represents the connections in moduleId = 3.
    /// 
    /// 6) Loop through the output neurons, and use their activation function
    /// to get the final result.
    /// 
    /// Note: Global input (with bias) and output neurons are keept the first
    /// in the activation arrays, so it is fast to access their values.
    /// 
    /// </summary>

    // TODO: consider not making any difference after all and have hidden neurons
    // mixed with all local output neurons.

    public class EspCyclicNetwork : IBlackBox
    {
		// Here we get all the variables needed for the phenome. Some of them
        // will be stored in local variables for extra performance.
        private readonly PhenomeVariables _phenVars;

        // Connections:
        // Inside _connectionArray:
        // In and bias to local input connections
        // In and bias to regulatory neurons connections
        // Non protected connections
        protected readonly FastConnection[] _connectionArray;
        protected readonly FastConnection[][] _localOutToRegLInConnect;
        protected readonly FastConnection[][] _localOutToOutConnect;
        // Extra arrays: how many local out to X connections there are in 
        // each module.
        // NOTE! There can be non-protected connections FROM local output neurons
        // as well. These are in _connectionArray with the rest of that kind.
        public readonly int[] _localOutToRegLInModuleCount;
        public readonly int[] _localOutToOutModuleCount;

        // Neuron pre- and post-activation signal arrays.
        protected readonly double[] _preActivationArray;
        protected readonly double[] _postActivationArray;

        // Wrappers over _postActivationArray that map between black box
        // inputs/outputs to the corresponding underlying network state variables.
        // This is only to increase performance reading input/output.
		private readonly SignalArray _inputSignalArrayWrapper;
		private readonly SignalArray _outputSignalArrayWrapper;

        // Pandemonium information (number of groups, number of neurons in 
        // each group, and the 2D-array with the neurons' index values). 
        // Number of pandemoniums (including [0][]).
        // private readonly int _numberOfPandem; // access through _phenVars
        // Number of regulatory neurons in each pandemonium.
        private readonly int[] _pandemoniumCounts;
        private readonly int[][] _pandemonium;

        // Convenient counts.
        private readonly int _inputCount;
        private readonly int _inToRegEndIndex;
        private readonly int _connectionArrayLength;
        private readonly int _firstRegIndex;
        private readonly int _inBiasOutRegEndIndex;
        // Counts hidden neurons and local output neurons that have any target
        // other than output neurons (regulatory, local input or non-protected
        // connections).
        private readonly int _hiddenLocalOutNoOutEndIndex;
        private readonly int _localInFromBiasInEndIndex;
        private readonly int _localOutToOutFirstIndex;
        // private readonly double _maxWeight;

        // Aparently reading activation functions from an array (as oposed to
        // having a single constant reference to a function and no aux parameters)
        // does not involve a significant performance overhead (<1%, as tested
        // in my machine).
        // Otherwise we could always call SteepenedSigmoid with no aux.
        // The Exp function there takes most time.
        // The use of arrays with a function for each neuron is left as
        // an option.
        /*
        protected readonly IActivationFunction[] _neuronActivationFnArray;
        protected readonly double[][] _neuronAuxArgsArray;
        */
        // Otherwise we will use the most basic set of activation functions:
        // for normal neurons, for regulatory neurons and for output neurons.
        // For regulatory neurons we may want to use step functions, while
        // for output we might want a function that ensures that for 0 input
        // we have 0 output.
        private readonly IActivationFunction _normalNeuronActivFn;
        private readonly IActivationFunction _regulatoryActivFn;
        private readonly IActivationFunction _outputNeuronActivFn;

        #region Constructor

        /// <summary>
        /// Constructs a FastCyclicNetwork.
        /// </summary>
        public EspCyclicNetwork(PhenomeVariables phenomeVariables)
        {
            // This structure contains all we need. However, we will make a
            // local copy of a few variables for extra performance.
            _phenVars = phenomeVariables;

            // Connectivity data
            _connectionArray = _phenVars.fastConnectionArray;
            _localOutToRegLInConnect = _phenVars.localOutToRegOrLInConnect;
            _localOutToOutConnect = _phenVars.localOutToOutConnect;
            _localOutToRegLInModuleCount = _phenVars.lOutToRegOrLInModuleCount;
            _localOutToOutModuleCount = _phenVars.localOutToOutModuleCount;

            // Create neuron pre- and post-activation signal arrays.
            _preActivationArray = new double[_phenVars.neuronCount];
            _postActivationArray = new double[_phenVars.neuronCount];
            // Sets activation values to 0.
            ResetState();

            // Wrap sub-ranges of the neuron signal arrays as input and output
            // arrays for IBlackBox.
            // Input neurons: offset is 1 to skip bias neuron.
            _inputSignalArrayWrapper = new SignalArray(_postActivationArray, 1,
                                                       _phenVars.inputBiasCount - 1);
            // Output neurons: offset to skip bias and input neurons.
            _outputSignalArrayWrapper = new SignalArray(_postActivationArray,
                                                        _phenVars.inputBiasCount,
                                                        _phenVars.outputCount); 

            // Pandemonium data
            // _numberOfPandem (use _phenVars.numberOfPandem)
            _pandemoniumCounts = _phenVars.pandemoniumCounts;
            _pandemonium = _phenVars.pandemonium;

            // Counts and indices
            _inputCount = _phenVars.inputBiasCount - 1;
            _inToRegEndIndex = _connectionArray.Length - _phenVars.nonProtectedCount;
            _connectionArrayLength = _connectionArray.Length;
            // We get the index for the first regulatory because we are counting
            // elements. The index for the las element is our result - 1 (because
            // of the index 0) so we already have the index for the next element!
            _firstRegIndex = _phenVars.inputBiasCount + _phenVars.outputCount;
            _inBiasOutRegEndIndex = _firstRegIndex + _phenVars.regulatoryCount;
            _localOutToOutFirstIndex = _phenVars.neuronCount -
                                       _phenVars.localOutToOnlyOut;
            _localInFromBiasInEndIndex = _localOutToOutFirstIndex -
                                         _phenVars.localInFromLocalOutCount;
            _hiddenLocalOutNoOutEndIndex = _localInFromBiasInEndIndex -
                                           _phenVars.localInFromBiasInCount;

            // The first connection is always the auxiliary connection, which
            // is protected and has maximum weight:
            //_maxWeight = _connectionArray[0]._weight;
            // BUT: For protected connections we may not consider the weight,
            // since it is constant.

            // Activation functions
            /*
            _neuronActivationFnArray = _phenVars.neuronActivationFnArray;
            _neuronAuxArgsArray = _phenVars.neuronAuxArgsArray;
            */
            _normalNeuronActivFn = _phenVars.normalNeuronActivFn;
            _regulatoryActivFn = _phenVars.regulatoryActivFn;
            _outputNeuronActivFn = _phenVars.outputNeuronActivFn;
                        
            // Initialise the bias neuron's fixed output value.
            _postActivationArray[0] = 1.0;  
                        
/*            UnityEngine.Debug.Log("TIMESTEPS TO 2");
            _phenVars.timestepsPerActivation = 2;*/
        }

        #endregion

        #region IBlackBox Members

        /// <summary>
        /// Gets the number of inputs.
        /// </summary>
        public int InputCount
        {
            get { return _inputCount; }
        }

        /// <summary>
        /// Gets the number of outputs.
        /// </summary>
        public int OutputCount
        {
            get { return _phenVars.outputCount; }
        }

        /// <summary>
        /// Gets an array for feeding input signals to the network.
        /// </summary>
        public ISignalArray InputSignalArray
        {
            get { return _inputSignalArrayWrapper; }
        }

        /// <summary>
        /// Gets an array of output signals from the network.
        /// </summary>
        public ISignalArray OutputSignalArray
        {
            get { return _outputSignalArrayWrapper; }
        }

        /// <summary>
        /// Gets a value indicating whether the internal state is valid.
        /// Always returns true for this class.
        /// </summary>
        public virtual bool IsStateValid
        {
            get { return true; }
        }

        /// <summary>
        /// Activate the network for a fixed number of iterations defined by
        /// the 'maxIterations' parameter at construction time. Activation
        /// reads input signals from InputSignalArray and writes output signals
        /// to OutputSignalArray.
        /// 
        /// Input values are taken from the UnitController script.
        /// 
        /// IMPROVEMENT? This function is too complex, so it should be broken
        /// into smaller pieces. Will the extra function calls be a problem?
        /// Test!
        /// </summary>
        public virtual void Activate()
        {
            /*
            System.Diagnostics.Debug.WriteLine("input " + _postActivationArray[0] + " " +
                _postActivationArray[1] + " " +
                _postActivationArray[2] + " " +
                _postActivationArray[3] + " " +
                _postActivationArray[4] + " " +
                _postActivationArray[5] + " " +
                _postActivationArray[6]
                );
            */

/*          foreach (FastConnection connect in _connectionArray)
            {
                UnityEngine.Debug.Log("Connection from " + connect._srcNeuronIdx +
                                      " to " + connect._tgtNeuronIdx);
            }*/

            // 1) Loops through connections from input and bias to local_input.
            // Copies the post-activation values directly.
            // This information is constant, so it is done outside of the 
            // timesteps loop.
            for (int j = 0; j < _phenVars.localInFromBiasInCount; ++j)
            {
                _postActivationArray[_connectionArray[j]._tgtNeuronIdx] = 
                    _postActivationArray[_connectionArray[j]._srcNeuronIdx];
            }
/*			UnityEngine.Debug.Log("CHECK after in to local in update CHECK CHECK CHECK");
            UnityEngine.Debug.Log("indices: " + 0 + " " + _phenVars.localInFromBiasInCount);
			for (int h = 0; h < _phenVars.neuronCount; ++h)
			{
				UnityEngine.Debug.Log("Index: Pre/post " + h + ": " + _preActivationArray[h] + 
					" " + _postActivationArray[h]);
			}*/

            // Main loop:
            // Activate the network for a fixed number of timesteps.
            for (int i = 0; i < _phenVars.timestepsPerActivation; ++i)
            {
                // 2.1) Loops through connections from input and bias to
                // regulatory neurons. Copies the post-activation value of the
                // source to the pre-activation of the target.
                for (int j = _phenVars.localInFromBiasInCount; j < _inToRegEndIndex; ++j)
				{
                    _preActivationArray[_connectionArray[j]._tgtNeuronIdx] += 
                        _postActivationArray[_connectionArray[j]._srcNeuronIdx] *
                        _connectionArray[j]._weight;   
                }
/*				UnityEngine.Debug.Log("CHECK after in to reg connection update CHECK CHECK CHECK");
				UnityEngine.Debug.Log("indices: " + _phenVars.localInFromBiasInCount + " " + _inToRegEndIndex);
				for (int h = 0; h < _phenVars.neuronCount; ++h)
				{
					UnityEngine.Debug.Log("Index: Pre/post " + h + ": " + _preActivationArray[h] + 
						" " + _postActivationArray[h]);
				}*/

                // 2.2) Loops through non-protected connections.
                // Copies the post-activation value of the source to the 
                // pre-activation of the target, applying the connection weight.
                for (int j = _inToRegEndIndex; j < _connectionArrayLength; ++j)
                {
                    _preActivationArray[_connectionArray[j]._tgtNeuronIdx] += 
                        _postActivationArray[_connectionArray[j]._srcNeuronIdx] *
                        _connectionArray[j]._weight;
                }
/*				UnityEngine.Debug.Log("CHECK after non protected connections update CHECK CHECK CHECK");
				UnityEngine.Debug.Log("indices: " + _inToRegEndIndex + " " + _connectionArrayLength);
				for (int h = 0; h < _phenVars.neuronCount; ++h)
				{
					UnityEngine.Debug.Log("Index: Pre/post " + h + ": " + _preActivationArray[h] + 
						" " + _postActivationArray[h]);
				}*/

                // 3) Loops through connections from local output neurons to
                // regulatory or local input neurons, which have been grouped
                // by modules (there are regulatoryCount of them).
                // Applies the post-activation value for the regulatory neuron
                // in the module.
                // NOTICE! Module index i corresponds to module i + 1 (there are
                // never any connections of this type for module 0).
                // Note: until the last iteration we may forget about connections
                // to output neurons.
                for (int j = 0; j < _phenVars.regulatoryCount; ++j)
                {
                    // Gets the regulatory neuron post-activation value.
                    double moduleActivity = _postActivationArray[_firstRegIndex + j];                        
                    for (int k = 0; k < _localOutToRegLInModuleCount[j]; ++k)
                    {
/*                        UnityEngine.Debug.Log("Local out to reg or local in");
                        UnityEngine.Debug.Log("from " + _localOutToRegLInConnect[j][k]._srcNeuronIdx +
                                              " to " + _localOutToRegLInConnect[j][k]._tgtNeuronIdx);*/
                        _preActivationArray[_localOutToRegLInConnect[j][k]._tgtNeuronIdx] += 
                                _postActivationArray[_localOutToRegLInConnect[j][k]._srcNeuronIdx] *
                                _localOutToRegLInConnect[j][k]._weight * moduleActivity;   
                    }
                } 
/*				UnityEngine.Debug.Log("CHECK after local out to local in or reg connections MATRIX update CHECK CHECK CHECK");
				for (int h = 0; h < _phenVars.neuronCount; ++h)
				{
					UnityEngine.Debug.Log("Index: Pre/post " + h + ": " + _preActivationArray[h] + 
						" " + _postActivationArray[h]);
				}*/

                // 4) Loops through neurons (pre- and post-activation arrays).
                // Note: Local input neurons do not change their post-activation
                // values.
                // Note: Local_output-to-output neurons only need to be updated
                // in the last iteration.

                // 4.1) Loops through hidden neurons and local output neurons
                // with not only output neurons as target:
                // TODO: Consider updating here all hidden and local output neurons.
                // There will be very few local output neurons to ONLY output
                // neurons to make up for all the complications!
                for (int j = _inBiasOutRegEndIndex; j < _hiddenLocalOutNoOutEndIndex; ++j)
                {
                    // Updates the post-activation value
                    _postActivationArray[j] = _normalNeuronActivFn.Calculate(
                            _preActivationArray[j], null);                    
                    // Resets the pre-actiavtion value
                    _preActivationArray[j] = 0.0;                    
                }
/*              UnityEngine.Debug.Log("CHECK after hidden and local out (no only to out) update CHECK CHECK CHECK");
				UnityEngine.Debug.Log("indices: " + _inBiasOutRegEndIndex  + " " + _hiddenLocalOutNoOutEndIndex);
				for (int h = 0; h < _phenVars.neuronCount; ++h)
				{
					UnityEngine.Debug.Log("Index: Pre/post " + h + ": " + _preActivationArray[h] + 
						" " + _postActivationArray[h]);
				}*/

                // 4.2) Loops through local input neurons with local output
                // neurons as sources. Local input neurons with bias and input
                // neurons as sources have fixed input, therefore fixed output.
                // Local input neurons are special in that they copy their
                // input.
                for (int j = _localInFromBiasInEndIndex; j < _localOutToOutFirstIndex; ++j)
                {
                    // Updates the post-activation value
                    _postActivationArray[j] = _preActivationArray[j];                    
                    // Resets the pre-actiavtion value
                    _preActivationArray[j] = 0.0;                    
                }
/*              UnityEngine.Debug.Log("CHECK after LIn with LO as sources update CHECK CHECK CHECK");
                UnityEngine.Debug.Log("indices: " + _localInFromBiasInEndIndex  + " " + _localOutToOutFirstIndex);
                for (int h = 0; h < _phenVars.neuronCount; ++h)
                {
                    UnityEngine.Debug.Log("Index: Pre/post " + h + ": " + _preActivationArray[h] + 
                        " " + _postActivationArray[h]);
                }*/

                // 4.3) Loops through local_output-to-output neurons:
                // We will need the correct post-activation values during the
                // last iteration, but this will be done AFTER this loop, so it
                // is Ok to update these only in the last iteration.
                if (i == _phenVars.timestepsPerActivation - 1)
                {
                    // Updates and resets
                    for (int j = _localOutToOutFirstIndex; j < _phenVars.neuronCount; ++j)
                    {
                        // UnityEngine.Debug.Log("CHECK after local out to out intermediate update INDEX " + j);
                        _postActivationArray[j] = _normalNeuronActivFn.Calculate(
                                _preActivationArray[j], null);   
                        _preActivationArray[j] = 0.0;                          
                    }                    
                }
                else
                {
                    // Only resets their pre-activation values.
                    for (int j = _localOutToOutFirstIndex; j < _phenVars.neuronCount; ++j)
                    {  
                        _preActivationArray[j] = 0.0;                          
                    }                     
                }
/*				UnityEngine.Debug.Log("CHECK after local out to out intermediate update CHECK CHECK CHECK");
				UnityEngine.Debug.Log("indices: " + _localOutToOutFirstIndex + " " + _phenVars.neuronCount);
				for (int h = 0; h < _phenVars.neuronCount; ++h)
				{
					UnityEngine.Debug.Log("Index: Pre/post " + h + ": " + _preActivationArray[h] + 
						" " + _postActivationArray[h]);
				}*/

                // 4.4) Regulatory neurons. For pandemonium group 0 we update
                // them as usual. For other groups we find the regulatory neuron
                // with highest pre-activation activity and set the pos-activity
                // of that one to 1, the rest to 0.
				// First we go through group 0
				for (int k = 0; k < _phenVars.pandemoniumCounts[0]; ++k)
                {
                    // UnityEngine.Debug.Log("Regulatory in 0, index " + _pandemonium[0][k]);
					// Updates the post-activation value
					_postActivationArray[_pandemonium[0][k]] =  
                            _regulatoryActivFn.Calculate(
                                    _preActivationArray[_pandemonium[0][k]], null);                    
					// Resets the pre-actiavtion value
                    _preActivationArray[_pandemonium[0][k]] = 0.0;   					
				}
				// Rest of groups
				for (int j = 1; j < _phenVars.numberOfPandem; ++j)
                {
					// We need to identify the neuron with maximum pre-activation
                    // activity.
                    int maxIndex = -1;
					// We require at least a small activation threshold
					double maxPreActivity = 0.05;
					for (int k = 0; k < _phenVars.pandemoniumCounts[j]; ++k)
					{
                        // UnityEngine.Debug.Log("Regulatory in " + j + ", index " + _pandemonium[j][k]);
                        if (_preActivationArray[_pandemonium[j][k]] > maxPreActivity)
                        {
                            maxIndex = _pandemonium[j][k];
                            maxPreActivity = _preActivationArray[maxIndex];
                        }
                        // We take the chance to reset the pre-activation.
                        // We also set the post-activation to 0 (then we
                        // update this for the chosen regulatory neuron.
                        _preActivationArray[_pandemonium[j][k]] = 0.0;
                        _postActivationArray[_pandemonium[j][k]] = 0.0;
					}
					// If there is at least one regulatory neuron above threshold:
					if (maxIndex != -1)
					{
						_postActivationArray[maxIndex] = 1.0;	
					}
				}
/*				UnityEngine.Debug.Log("CHECK after pandemonium MATRIX update CHECK CHECK CHECK");
				for (int h = 0; h < _phenVars.neuronCount; ++h)
				{
					UnityEngine.Debug.Log("Index: Pre/post " + h + ": " + _preActivationArray[h] + 
						" " + _postActivationArray[h]);
				}*/
            }

            // 5) Loops through local_output-to-output connections, which are
            // grouped by module (there are regulatoryCount of them).
            // Applies the module regulation.
            for (int j = 0; j < _phenVars.regulatoryCount; ++j)
            {
                // Gets the regulatory neuron post-activation value.
                double moduleActivity = _postActivationArray[_firstRegIndex + j];                        
                for (int k = 0; k < _localOutToOutModuleCount[j]; ++k)
                {
/*                    UnityEngine.Debug.Log("Local out to out. from " + _localOutToOutConnect[j][k]._srcNeuronIdx +
                                          " to " + _localOutToOutConnect[j][k]._tgtNeuronIdx);*/
                    _preActivationArray[_localOutToOutConnect[j][k]._tgtNeuronIdx] += 
                        _postActivationArray[_localOutToOutConnect[j][k]._srcNeuronIdx] *
                        _localOutToOutConnect[j][k]._weight * moduleActivity;   
                }
            } 
/*    		UnityEngine.Debug.Log("CHECK after local out to out MATRIX final update CHECK CHECK CHECK");
			for (int h = 0; h < _phenVars.neuronCount; ++h)
			{
				UnityEngine.Debug.Log("Index: Pre/post " + h + ": " + _preActivationArray[h] + 
					" " + _postActivationArray[h]);
			}*/

            // 6) Loops through output neurons (with their own activation
            // function). Gets the final post-activation values.
            for (int j = _phenVars.inputBiasCount; j < _firstRegIndex; ++j)
            {                
				_postActivationArray[j] = _outputNeuronActivFn.Calculate(
                        _preActivationArray[j], null);   
                _preActivationArray[j] = 0.0;                          
            }               
/*			UnityEngine.Debug.Log("CHECK after out final update CHECK CHECK CHECK");
			UnityEngine.Debug.Log("indices: " + _phenVars.inputBiasCount + " " + _firstRegIndex);
            for (int h = 0; h < _phenVars.neuronCount; ++h)
            {
                UnityEngine.Debug.Log("Index: Pre/post " + h + ": " + _preActivationArray[h] + 
                    " " + _postActivationArray[h]);
            }*/

            
            //System.Diagnostics.Debug.WriteLine("output " + _postActivationArray[7] + " " + 
            //    _postActivationArray[8]);

        }

        /// <summary>
        /// Reset the network's internal state.
        /// </summary>
        public void ResetState()
        {
            // Resets the activation arrays.
            // Ignores input signals as these gets overwritten on each iteration.
            //for (int i = _phenVars.inputBiasCount; i < _postActivationArray.Length; i++)
            for (int i = _phenVars.inputBiasCount; i < _phenVars.neuronCount; ++i)
            {
                _preActivationArray[i] = 0.0;
                _postActivationArray[i] = 0.0;
            }   
        }

        #endregion
    }
}
