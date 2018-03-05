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

using System.Collections.Generic;
using SharpNeat.Utility;

namespace SharpNeat.Network
{
    /// <summary>
    /// Default implementation of an IActivationFunctionLibrary. 
    /// Also provides static factory methods to create libraries with commonly used activation functions.
    /// </summary>
    public class DefaultActivationFunctionLibrary : IActivationFunctionLibrary
    {
        readonly IList<ActivationFunctionInfo> _functionList;
        readonly Dictionary<int,IActivationFunction> _functionDict;
        readonly RouletteWheelLayout _rwl;

        #region Constructor

        /// <summary>
        /// Constructs an activation function library with the provided list of activation functions.
        /// </summary>
        public DefaultActivationFunctionLibrary(IList<ActivationFunctionInfo> fnList)
        {
            // Build a RouletteWheelLayout based on the selection probability on each item.
            int count = fnList.Count;
            double[] probabilities = new double[count];
            for(int i=0; i<count; i++) {
                probabilities[i] = fnList[i].SelectionProbability;
            }
            _rwl = new RouletteWheelLayout(probabilities);
            _functionList = fnList;

            // Build a dictionary of functions keyed on integer ID.
            _functionDict = CreateFunctionDictionary(_functionList);
        }

        #endregion

        #region IActivationFunctionLibrary Members

        /// <summary>
        /// Gets the function with the specified integer ID.
        /// </summary>
        public IActivationFunction GetFunction(int id)
        {
            return _functionDict[id];
        }

        /// <summary>
        /// Randomly select a function based on each function's selection probability.
        /// </summary>
        public ActivationFunctionInfo GetRandomFunction(FastRandom rng)
        {
            return _functionList[RouletteWheel.SingleThrow(_rwl, rng)];
        }

        /// <summary>
        /// Gets a list of all functions in the library.
        /// </summary>
        public IList<ActivationFunctionInfo> GetFunctionList()
        {
            return _functionList;
        }

        #endregion

        #region Private Methods

        private static Dictionary<int,IActivationFunction> CreateFunctionDictionary(IList<ActivationFunctionInfo> fnList)
        {
            Dictionary<int,IActivationFunction> dict = new Dictionary<int,IActivationFunction>(fnList.Count);
            foreach(ActivationFunctionInfo fnInfo in fnList) {
                dict.Add(fnInfo.Id, fnInfo.ActivationFunction);
            }
            return dict;
        }

        #endregion

        #region Public Static Factory Methods

        /// <summary>
        /// Create an IActivationFunctionLibrary for use with EspNEAT.
        /// EspNEAT uses three activation functions:
        /// One, standard, for normal neurons.
        /// One for regulatory neurons. Here we may want something like a step
        /// function.
        /// One for output neurons (connected to local output neurons). Here
        /// we need output = 0 for input = 0. Otherwise output neurons will
        /// produce spontaneous activity (also, local_output to output connections
        /// may not be negative in many Esp implementations!). 
        /// 
        /// ENHANCEMENT? Perhaps this structure (probably designed to work best
        /// with CPPNs) is not the best for EspNEAT. The danger here is to
        /// identify activation functions with an integer, which may lead to
        /// unintended mistakes. This is limited since this numbers will only
        /// be directly handled in CreateNeuronGene in NeatGenomeFactory and in
		/// ReadGenome in NeatGenomeXmlIO.
		/// 
        /// The advantage of leaving this like this is that it would be easier
        /// to adapt EspNEAT to an algorithm using different activation 
        /// functions for some or all neurons.
        /// </summary>
        public static IActivationFunctionLibrary CreateLibraryNeat(
                IActivationFunction normalNeuronActivFn,
                IActivationFunction regulatoryActivFn,
                IActivationFunction outputNeuronActivFn)
        {
            List<ActivationFunctionInfo> fnList = new List<ActivationFunctionInfo>(3);
            // Default activation function
            fnList.Add(new ActivationFunctionInfo(0, 1.0, normalNeuronActivFn));
            // Activation function for regulatory neurons
            fnList.Add(new ActivationFunctionInfo(1, 0.0, regulatoryActivFn));
            // Activation function for outpupt neurons
            fnList.Add(new ActivationFunctionInfo(2, 0.0, outputNeuronActivFn));
            return new DefaultActivationFunctionLibrary(fnList);
        }

        /// <summary>
        /// Create an IActivationFunctionLibrary for use with CPPNs.
        /// </summary>
        public static IActivationFunctionLibrary CreateLibraryCppn()
        {
            List<ActivationFunctionInfo> fnList = new List<ActivationFunctionInfo>(4);
            fnList.Add(new ActivationFunctionInfo(0, 0.25, Linear.__DefaultInstance));
            fnList.Add(new ActivationFunctionInfo(1, 0.25, BipolarSigmoid.__DefaultInstance));
            fnList.Add(new ActivationFunctionInfo(2, 0.25, Gaussian.__DefaultInstance));
            fnList.Add(new ActivationFunctionInfo(3, 0.25, Sine.__DefaultInstance));
            return new DefaultActivationFunctionLibrary(fnList);
        }

        /// <summary>
        /// Create an IActivationFunctionLibrary for use with Radial Basis Function NEAT.
        /// </summary>
        public static IActivationFunctionLibrary CreateLibraryRbf(IActivationFunction activationFn, double auxArgsMutationSigmaCenter, double auxArgsMutationSigmaRadius)
        {
            List<ActivationFunctionInfo> fnList = new List<ActivationFunctionInfo>(2);
            fnList.Add(new ActivationFunctionInfo(0, 0.8, activationFn));
            fnList.Add(new ActivationFunctionInfo(1, 0.2, new RbfGaussian(auxArgsMutationSigmaCenter, auxArgsMutationSigmaRadius)));
            return new DefaultActivationFunctionLibrary(fnList);
        }

        #endregion
    }
}
