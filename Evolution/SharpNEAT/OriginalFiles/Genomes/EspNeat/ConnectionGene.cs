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

namespace SharpNeat.Genomes.Neat
{
    /// <summary>
    /// A gene that represents a single connection between neurons in NEAT.
    /// </summary>
    public class ConnectionGene : INetworkConnection
    {
        private uint _innovationId;
        private uint _sourceNodeId;
        private uint _targetNodeId;
        private double _weight;
        private int _modId;
        private readonly bool _protected;

        /// <summary>
        /// Used by the connection mutation routine to flag mutated connections 
        /// so that they aren't mutated more than once.
        /// </summary>
        private bool _isMutated = false;

        #region Constructor

        /// <summary>
        /// Copy constructor.
        /// </summary>
        public ConnectionGene(ConnectionGene copyFrom)
        {
            _innovationId = copyFrom._innovationId;
            _sourceNodeId = copyFrom._sourceNodeId;
            _targetNodeId = copyFrom._targetNodeId;
            _weight = copyFrom._weight;
            _modId = copyFrom._modId;
            _protected = copyFrom._protected;
        }

        /// <summary>
        /// Construct a new ConnectionGene with the specified source and target 
        /// neurons, connection weight and module ID.
        /// </summary>
        public ConnectionGene(uint innovationId, uint sourceNodeId, 
                              uint targetNodeId, double weight, int modId)
        {
            _innovationId = innovationId;
            _sourceNodeId = sourceNodeId;
            _targetNodeId = targetNodeId;
            _weight = weight;
            _modId = modId;
            _protected = false;
        }

        /// <summary>
        /// Construct a new ConnectionGene with the specified source and target 
        /// neurons, connection weight, module ID and protected label.
        /// </summary>
        public ConnectionGene(uint innovationId, uint sourceNodeId, 
                              uint targetNodeId, double weight, int modId,
                              bool protect)
        {
            _innovationId = innovationId;
            _sourceNodeId = sourceNodeId;
            _targetNodeId = targetNodeId;
            _weight = weight;
            _modId = modId;
            _protected = protect;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the gene's innovation ID.
        /// </summary>
        public uint InnovationId
        {
            get { return _innovationId; }
            set { _innovationId = value; }
        }

        /// <summary>
        /// Gets or sets the gene's source neuron/node ID.
        /// </summary>
        public uint SourceNodeId
        {
            get { return _sourceNodeId; }
            set { _sourceNodeId = value; }
        }

        /// <summary>
        /// Gets or sets the gene's target neuron/node ID.
        /// </summary>
        public uint TargetNodeId
        {
            get { return _targetNodeId; }
            set { _targetNodeId = value; }
        }

        /// <summary>
        /// Gets or sets the gene's connection weight.
        /// </summary>
        public double Weight
        {
            get { return _weight; }
            set { _weight = value; }
        }


        /// <summary>
        /// Gets or sets the gene's module ID.
        /// </summary>
        public int ModuleId
        {
            get { return _modId; }
            set { _modId = value; }
        }

        /// <summary>
        /// Gets the connection's protected field.
        /// </summary>
        public bool Protected
        {
            get { return _protected; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this gene has been mutated. 
        /// This allows the mutation routine to avoid mutating genes on which it 
        /// has already operated. These flags are reset for all connection genes
        /// within a NeatGenome on exiting the mutation routine.
        /// </summary>
        public bool IsMutated
        {
            get { return _isMutated; }
            set { _isMutated = value; }
        }

        #endregion

        #region Public Methods

        public void PrintConnectionInfo()
        {
            System.Diagnostics.Debug.WriteLine("ID: " + _innovationId + ", from: " + _sourceNodeId +
						        	           ", to: " + _targetNodeId + ", weight: " + _weight +
                                               ", module: " + _modId + ", is protected: " + _protected);
        }

        /// <summary>
        /// Creates a copy of the current gene. Virtual method that can be 
        /// overriden by sub-types.
        /// </summary>
        public virtual ConnectionGene CreateCopy()
        {
            return new ConnectionGene(this);
        }

        #endregion
    }
}
