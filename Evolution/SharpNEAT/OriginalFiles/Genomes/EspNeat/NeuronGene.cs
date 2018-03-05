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
using System.Collections.Generic;

namespace SharpNeat.Genomes.Neat
{
    /// <summary>
    /// A gene that represents a single neuron in NEAT.
    /// </summary>
    public class NeuronGene : INetworkNode
    {
        /// <summary>
        /// Although this ID is allocated from the global innovation ID pool, 
        /// neurons do not participate in compatibility measurements and so it 
        /// is not actually used as an innovation ID. 
        /// </summary>
        private readonly uint _innovationId;
        private readonly NodeType _neuronType;
        private readonly int _activationFnId;
        private readonly int _modId;
        private int _pandemonium;
        private readonly double[] _auxState;
        private readonly HashSet<uint> _srcNeurons;
        private readonly HashSet<uint> _tgtNeurons;

        #region Constructor
        /// <summary>
        /// Copy constructor.
        /// </summary>
        /// <param name="copyFrom">NeuronGene to copy from.</param>
        /// <param name="copyConnectivityData">Indicates whether or not top copy
        /// connectivity data for the neuron.</param>
        public NeuronGene(NeuronGene copyFrom, bool copyConnectivityData)
        {
            _innovationId = copyFrom._innovationId;
            _neuronType = copyFrom._neuronType;
            _activationFnId = copyFrom._activationFnId;
            _modId = copyFrom._modId;
            _pandemonium = copyFrom._pandemonium;
            if(null != copyFrom._auxState) {
                _auxState = (double[])copyFrom._auxState.Clone();
            }

            if(copyConnectivityData) {
                _srcNeurons = new HashSet<uint>(copyFrom._srcNeurons);
                _tgtNeurons = new HashSet<uint>(copyFrom._tgtNeurons);
            } else {
                _srcNeurons = new HashSet<uint>();
                _tgtNeurons = new HashSet<uint>();
			}
        }

        /// <summary>
        /// Construct new NeuronGene with the specified innovationId, neuron type, 
        /// activation function ID and module ID.
        /// </summary>
		public NeuronGene(uint innovationId, NodeType neuronType, 
                          int activationFnId, int modId)
        {
            _innovationId = innovationId;
            _neuronType = neuronType;
            _activationFnId = activationFnId;
            _modId = modId;
            _pandemonium = 0;
            _auxState = null;
            _srcNeurons = new HashSet<uint>();
			_tgtNeurons = new HashSet<uint>();
        }

        /// <summary>
        /// Constructor for regulatory neurons, which have to specify 
        /// their pandemonium value.
        /// </summary>
        public NeuronGene(uint innovationId, NodeType neuronType, 
                          int activationFnId, int modId, int pandemonium)
        {
            _innovationId = innovationId;
            _neuronType = neuronType;
            _activationFnId = activationFnId;
            _modId = modId;
            _pandemonium = pandemonium;
            _auxState = null;
            _srcNeurons = new HashSet<uint>();
            _tgtNeurons = new HashSet<uint>();
        }

        /// <summary>
        /// Construct new NeuronGene providing a complete description.
        /// Mainly used when reading an Xml file.
        /// </summary>
		public NeuronGene(uint innovationId, NodeType neuronType, 
                          int activationFnId, int modId, int pandemonium,
                          double[] auxState)
        {
            _innovationId = innovationId;
            _neuronType = neuronType;
            _activationFnId = activationFnId;
            _modId = modId;
            _pandemonium = pandemonium;
            _auxState = auxState;
            _srcNeurons = new HashSet<uint>();
            _tgtNeurons = new HashSet<uint>();
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the neuron's innovation ID.
        /// </summary>
        public uint InnovationId
        {
            get { return _innovationId; }
        }

        /// <summary>
        /// Gets the neuron's type.
        /// </summary>
        public NodeType NodeType
        {
            get { return _neuronType; }
        }

        /// <summary>
        /// Gets the neuron's activation function ID. 
        /// For NEAT this is not used and will always be zero.
        /// For CPPNs/HyperNEAT this ID corresponds to an entry in the 
        /// IActivationFunctionLibrary present in the current genome factory.
        /// </summary>
        public int ActivationFnId
        {
            get { return _activationFnId; }
        }

        /// <summary>
        /// Gets the neuron's module ID.
        /// </summary>
        public int ModuleId
        {
            get { return _modId; }
        }

        /// <summary>
        /// Gets the neuron's pandemonium index. Only for regulatory neurons
        /// (otherwise it takes 0). 0 means no pandemonium.
        /// </summary>
        public int Pandemonium
        {
            get { return _pandemonium; }
            set { _pandemonium = value; }
        }

        /// <summary>
        /// Optional auxilliary node state. Null if no aux state is present. 
        /// Note. Radial Basis Function center and epsilon values are stored here.
        /// </summary>
        public double[] AuxState
        {
            get { return _auxState; }
        }

        /// <summary>
        /// Gets a set of IDs for the source neurons that directly connect into
        /// this neuron.
        /// </summary>
        public HashSet<uint> SourceNeurons
        {
            get { return _srcNeurons; }
        }

        /// <summary>
        /// Gets a set of IDs for the target neurons this neuron directly 
        /// connects out to.
        /// </summary>
        public HashSet<uint> TargetNeurons
        {
            get { return _tgtNeurons; }
        }
        #endregion

        #region Public Methods

        public void PrintNeuronInfo()
        {
            System.Diagnostics.Debug.WriteLine("Type: " + _neuronType + ", ID: " + _innovationId +
                                               ", act. function ID: " + _activationFnId +
                                               ", Module ID: " + _modId + ", pandemonium: " + _pandemonium);
        }

        public void PrintSourcesAndTargets()
        {
            foreach (uint source in _srcNeurons)
            {
                System.Diagnostics.Debug.WriteLine("Source ID: " + source);
            }
            foreach (uint target in _tgtNeurons)
            {
                System.Diagnostics.Debug.WriteLine("Target ID: " + target);
            }
        }

        /// <summary>
        /// Creates a copy of the current gene. Virtual method that can be 
        /// overriden by sub-types.
        /// </summary>
        public virtual NeuronGene CreateCopy()
        {
            return new NeuronGene(this, true);
        }

        /// <summary>
        /// Creates a copy of the current gene. Virtual method that can be 
        /// overriden by sub-types.
        /// </summary>
        /// <param name="copyConnectivityData">Indicates whether or not top copy
        /// connectivity data for the neuron.</param>
        public virtual NeuronGene CreateCopy(bool copyConnectivityData)
        {
            return new NeuronGene(this, copyConnectivityData);
        }
        #endregion

        #region INetworkNode Members
        /// <summary>
        /// Gets the network node's ID.
        /// </summary>
        public uint Id
        {
            get { return _innovationId; }
        }
        #endregion
    }
}
