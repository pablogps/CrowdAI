using SharpNeat.Network;

namespace SharpNeat.Phenomes.NeuralNets
{
    /// <summary>
    /// These are the variables and structures needed by the phenome. They are
    /// too many to pass as parameters, so we encapsulate them in this struct.
    /// If reading from here is at any time considered a performance issue
    /// they can be copied from here to local variables in the constructor.
    /// </summary>
    public struct PhenomeVariables {
        // We need to know the number of different neuron types and different
        // connections (from which neuron type to which neuron type). This will
        // let us know what type of neuron corresponds to a given index value
        // in the activation and connection lists.
        public int timestepsPerActivation;
        public int neuronCount;
        public int inputBiasCount;
        public int outputCount;
        public int regulatoryCount;
        public int localInFromBiasInCount;
        public int localInFromLocalOutCount;
        public int nonProtectedCount;
		public int localOutToOutCount;
        // Local output neurons with output neurons as targets AND non-protected
        // connections (recursive connections) will NOT be listed in
        // localOutToOutCount.
        // Note that if recursive connections are allowed, it is to be expected
        // that most local output neurons will be here. 
        // TODO: consider not making any difference after all and have hidden neurons
        // mixed with all local output neurons.
        public int localOutToOnlyOut;

        // These two are not really used, because in normal NEAT all neurons
        // have the same activation function (with perhaps the exception of
        // regulatory neurons and global output neurons).
        public IActivationFunction[] neuronActivationFnArray;
        public double[][] neuronAuxArgsArray;
        // If the function array is not used, we need three different types
        // of activation functions:
        public IActivationFunction normalNeuronActivFn;
        public IActivationFunction regulatoryActivFn;
        public IActivationFunction outputNeuronActivFn;
        // Local input just copy the input or bias post-activation value, so
        // we do not really need a function for that.

        // Arrays are MUCH faster than lists! (Usually the extra functionallity
        // makes up for this, but not when performance is the priority).

        // fastConnectionArray includes:
        // In and bias to local input connections
        // In and bias to regulatory neurons connections
        // Non protected connections
        public FastConnection[] fastConnectionArray;
        // Local output to output/regulatory given in a 2D-arrays.
        // These connections need to be weighted by the corresponding regulatory
        // neuron in their module, which is why they are separated by modules
        // (so the regulatory neuron is read only once per module).
        public FastConnection[][] localOutToRegOrLInConnect;
        public FastConnection[][] localOutToOutConnect;
        // The fastest way to know how many local out to X connections 
        // there are in each module.
        public int[] lOutToRegOrLInModuleCount;
        public int[] localOutToOutModuleCount;
        // Regulatory neurons are separated by their pandemonium. In [0][] we
        // have those that do not belong to a pandemonium.
        public int[][] pandemonium;
        // Number of regulatory neurons in each pandemonium.
        public int[] pandemoniumCounts;
        // Number of pandemoniums (including "0").
        public int numberOfPandem;
    }   
}
