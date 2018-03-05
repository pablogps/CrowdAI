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
using SharpNeat.Network;
using SharpNeat.Phenomes.NeuralNets;
using SharpNeat.Genomes.Neat;

namespace SharpNeat.Decoders
{
    /// <summary>
    /// Static factory for creating an ESP phenome. Note the network architecture
    /// in ESP is more complex than in plain NEAT, so we are using a NeatGenome
    /// to build the phenome instead of a INetworkDefinition. (We need access
    /// to the neuron type.)
    /// 
    /// First, the phenome will use a slightly different sorting than genomes.
    /// Neurons will be ordered as follows:
    ///          -Input
    ///          -Output
    ///          -Regulatory
    ///          -Hidden and local output (with targets other than output neurons)
    ///          -Local input (with fixed input from bias and input neurons)
    ///          -Local input (with local output as sources)
    ///          -Local output (with output neurons as only target)
    /// Hidden and local output may be mixed.
    /// Note that the first three categories are the same as in genomes, so we
    /// only need to go through part of the neuron list.
    /// 
    /// Phenomes will not work with neuron genes, but with a pre- and post-activation
    /// activity lists. These lists only contain a double, and we know the neuron
    /// to which they correspond only by their index. Once we have the number
    /// of elements in the different neuron categories shown above, the lists
    /// are ready to use.
    /// 
    /// More complex is the connection list. We will pass a copy of the connection
    /// list, with the simplified structure "FastConnection" instead of the more
    /// complete "connection genes". We are basically interested in the source
    /// and target neurons of each connection, but this information uses the 
    /// index for the NeuronGeneList in genome, so we need to change these to
    /// the new organization used here. For this, we will create a dictionary
    /// with the old index to the new index (again, note that this only affects
    /// hidden and local input/output neurons).
    /// 
    /// Connections will also be classified, which is new to the ESP version 
    /// of the decoder. Sorting of connections will be as follows:
    ///          -Input (and bias) to local input
    ///          -Input and bias to regulatory
    ///          -Non-protected
    ///          -Local output to regulatory or local input
    ///          -Local output to output
    /// Because sorting connections requires reading their sources and targets,
    /// we can use the same loop to update those index values to the new (phenome)
    /// order of the neurons, using the dictionary we have composed before.
    /// </summary>
    public class EspCyclicNetworkFactory
    {
        private static PhenomeVariables phenomeVariables;
        private static Dictionary<uint,int> idToOldIndex;
        private static Dictionary<int,int> oldToNewIndex;
        private static Dictionary<uint,int> idToNewIndex;
        private static SortedDictionary<int, int> pandemoniumsFound;
        private static NeatGenome genome;

        // Modules do not need to be in good order anymore, so moduleId is not
        // enough to determine the index position for the arrays in PhenomeVariables
        // (moduleId could be the second module in the system, or the first).
        // This information is already stored in UIvaraibles.moduleIdList, however,
        // since we are not passing an instance of UIvariables to the decoder
        // we need to construct the list of modules here as well. MakeDictionaries
        // already loops through all neurons, so we can build the module list
        // with little extra overhead (specially in interactive evolution, where
        // decoding is not a big issue to begin with).
        // We take the chance to make it a dictionary, as opposed to a list
        // in UIvariables.
        private static Dictionary<int, int> moduleIdToIndex;

        #region Public Static Methods

        /// <summary>
        /// Creates a EspCyclicNetwork from a NeatGenome.
        /// </summary>
        public static EspCyclicNetwork CreateEspCyclicNetwork(
                NeatGenome givenGenome, NetworkActivationScheme activationScheme)
        {
            phenomeVariables = new PhenomeVariables();
            genome = givenGenome;
            GetEasyVariables();
            phenomeVariables.timestepsPerActivation = activationScheme.TimestepsPerActivation;
            InternalDecode();
            /*foreach (KeyValuePair<int, int> entry in oldToNewIndex)
            {
                UnityEngine.Debug.Log("old index " + entry.Key + " new index " + entry.Value);
            }*/
            return new EspCyclicNetwork(phenomeVariables);
        }

        #endregion

        #region Private Static Methods

        private static void InternalDecode()
        {
            // Id to index (old)
            // Old index to new index (where the old index follows the neuron
            // gene sorting in genomes, and old index corresponds to the sorting
            // used in phenomes).
            MakeDictionaries();
/*          foreach (KeyValuePair<int, int> pair in oldToNewIndex)
            {
                UnityEngine.Debug.Log("old to new Index " + pair.Key + " to " + pair.Value);
            }*/

            // Creates an array of FastConnection(s) that represent the
            // connectivity of the network.
            CreateFastConnectionArrays();

            // Creates the 2D-array with the regulatory neurons (index) in each
            // pandemonium group (including "no pandemonium" with pandemonium
            // value = 0.
            CreatePandemoniumArray();

            GetActivationFunctions();
        }

        /// <summary>
        /// Creates an array of FastConnection(s) representing the connectivity
        /// of the provided NeatGenome.
        /// </summary>
        private static void CreateFastConnectionArrays()
        {
			ConnectionGeneList connectionList = genome.ConnectionGeneList;
            INodeList nodeList = genome.NodeList;
            phenomeVariables.nonProtectedCount = 0;

            // Creates local lists to store connections so we can place elements
            // in the correct order, and also to sort them by fragments.
            List<ConnectionGene> toLocalInputList = new List<ConnectionGene>();
            List<ConnectionGene> inToRegList = new List<ConnectionGene>();
			List<ConnectionGene> nonProtectedList = new List<ConnectionGene>();

            // PROTECTED connections from local output neurons will be treated in a
            // special way (because they are affected by regulatory neurons).
            // For this reason they come in 2D-arrays, organized by module.
            // The number of such connections in each module is also given in
            // a separate 1D-array.
			// Non-protected connections from local output neurons (recursive connections)
			// are treated normally, and are not affected by regulatory neurons.
            // Remember: module Ids are not (necessarily) in order, so we use
            // moduleIdToIndex
            phenomeVariables.localOutToRegOrLInConnect = new FastConnection[genome.Regulatory][];
            phenomeVariables.localOutToOutConnect = new FastConnection[genome.Regulatory][];
            phenomeVariables.lOutToRegOrLInModuleCount = new int[genome.Regulatory];
            phenomeVariables.localOutToOutModuleCount = new int[genome.Regulatory];
            // And we set all counts to 0 (because we will count with ++)
            Array.Clear(phenomeVariables.lOutToRegOrLInModuleCount, 0, genome.Regulatory);
            Array.Clear(phenomeVariables.localOutToOutModuleCount, 0, genome.Regulatory);
            // We count first how many to expect in each module, so we need
            // local lists.
			List<ConnectionGene> localOutToOutList = new List<ConnectionGene>();
            List<ConnectionGene> localOutToRegOrLocalInList = new List<ConnectionGene>();

            // Loop the connections and lookup the neuron IDs for each
            // connection's end points using idToOldIndex.
			foreach (ConnectionGene connection in connectionList)
            {   
                // We have to determine the type of connection:
                //          -Input (and bias) to local input
                //          -Non-protected
                //          -Local output to regulatory or local input
                //          -Local output to output

                // We are lookgin for "the type of the node with index
                // corresponding to the Id of the target of the connection".

                NodeType targetType = 
                        nodeList[idToOldIndex[connection.TargetNodeId]].NodeType;               
				if (!connection.Protected)
                {
                    // Count it and store it for later
                    nonProtectedList.Add(connection);
                    ++phenomeVariables.nonProtectedCount;
                }
                else if (targetType == NodeType.Local_Input)
                {
                    NodeType sourceType = 
                            nodeList[idToOldIndex[connection.SourceNodeId]].NodeType; 
                    if (sourceType == NodeType.Local_Output)
                    {
                        // This is local_out to local_in
                        localOutToRegOrLocalInList.Add(connection);
                        ++phenomeVariables.lOutToRegOrLInModuleCount[
                                moduleIdToIndex[connection.ModuleId]];  
                    }
                    else
                    {
                        // This is bias/input to local input
                        toLocalInputList.Add(connection);
                    }
                }
                else if (targetType == NodeType.Regulatory)
                {
                    NodeType sourceType = 
                        nodeList[idToOldIndex[connection.SourceNodeId]].NodeType;  
                    if (sourceType == NodeType.Local_Output)
                    {
                        // This is local_out-to-regulatory
                        localOutToRegOrLocalInList.Add(connection);
                        ++phenomeVariables.lOutToRegOrLInModuleCount[
                                moduleIdToIndex[connection.ModuleId]];                        
                    }
                    else
                    {
                        // This is input/bias to regulatory. These connections
                        // go in the main connection array.
                        inToRegList.Add(connection);
                    }
                }
                else if (targetType == NodeType.Output)
                {
                    // This can only be local_out-to-out
                    localOutToOutList.Add(connection);
                    ++phenomeVariables.localOutToOutModuleCount[
                            moduleIdToIndex[connection.ModuleId]];
                }
            }

            // Now we process the local lists to make the definitive arrays
            ProcessLocalLists(toLocalInputList, inToRegList, nonProtectedList,
                              localOutToOutList, localOutToRegOrLocalInList);
        }

        /// <summary>
        /// Takes preliminary connection lists and updates the arrays in 
        /// phenomeVariables.
        /// </summary>
        private static void ProcessLocalLists(List<ConnectionGene> toLocalInputList,
                                              List<ConnectionGene> inToRegList,
                                              List<ConnectionGene> nonProtectedList,
                                              List<ConnectionGene> localOutToOutList,
                                              List<ConnectionGene> localOutToRegOrLocalInList)
        {
            // Initializes the main connection array, with the correct size.
            phenomeVariables.fastConnectionArray = new FastConnection[
                    toLocalInputList.Count + inToRegList.Count + nonProtectedList.Count];

            // For toLocalInputList, inToRegList and nonProtectedList we only
            // need to add all elements:
            int current = 0;
            // First connections from input and bias to local input.
            SortList(toLocalInputList);
            foreach (ConnectionGene connection in toLocalInputList)
            {
                AddConnection(connection, out phenomeVariables.fastConnectionArray[current]);
                ++current;
            }
            // Input and bias to regulatory connections.
            SortList(inToRegList);
            foreach (ConnectionGene connection in inToRegList)
            {
                AddConnection(connection, out phenomeVariables.fastConnectionArray[current]);
                ++current;
            }
            // Non protected connections.
            SortList(nonProtectedList);
            foreach (ConnectionGene connection in nonProtectedList)
            {
                AddConnection(connection, out phenomeVariables.fastConnectionArray[current]);
                ++current;
            }

            // For local-out to out/regulatory we need to put each connection
            // with its module. Local-output connections are already sorted
            // within modules.
			phenomeVariables.localOutToOutCount = localOutToOutList.Count;
            int localToRegCurrent = 0;
            int localToOutCurrent = 0;
            for (int moduleIndex = 0; moduleIndex < genome.Regulatory; ++moduleIndex)
            {
                // Now we know the size to initialize the 2D-arrays:
                phenomeVariables.localOutToRegOrLInConnect[moduleIndex] =
                        new FastConnection[phenomeVariables.lOutToRegOrLInModuleCount[moduleIndex]];  
                phenomeVariables.localOutToOutConnect[moduleIndex] =
                        new FastConnection[phenomeVariables.localOutToOutModuleCount[moduleIndex]];
                
                // Adds the saved connections in their correct place.
                // First local out to regulatory or local in:
                for (int i = 0; i < phenomeVariables.lOutToRegOrLInModuleCount[moduleIndex]; ++i)
                {
                    // The connection is the next in localOutToRegOrLocalInList.
                    // The array element is localOutToRegOrLInConnect[module][i] 
                    AddConnection(localOutToRegOrLocalInList[localToRegCurrent],
                                  out phenomeVariables.localOutToRegOrLInConnect[moduleIndex][i]);
                    ++localToRegCurrent; 
                }
                // Then local-out to out:
                for (int j = 0; j < phenomeVariables.localOutToOutModuleCount[moduleIndex]; ++j)
                {
                    // The connection is the next in localOutToOutList.
                    // The array element is localOutToRegOrLInConnect[module][i] 
                    AddConnection(localOutToOutList[localToOutCurrent],
                            out phenomeVariables.localOutToOutConnect[moduleIndex][j]);
                    ++localToOutCurrent; 
                }
            }
        }

        /// <summary>
        /// Takes a connection and a counter and updates the corresponding 
        /// FastConnection element in a phenomeVariables array.
        /// </summary>
        private static void AddConnection(ConnectionGene connection,
                                          out FastConnection fastConnectionElement)
        {
            // We need the NEW index for sources and targets, not the
            // original index in genome.
            fastConnectionElement._srcNeuronIdx = 
                    idToNewIndex[connection.SourceNodeId];                   
            fastConnectionElement._tgtNeuronIdx =                         
                    idToNewIndex[connection.TargetNodeId];                    
            fastConnectionElement._weight = connection.Weight;  
        }

        /// <summary>
        /// Sorts the provided list.
        /// </summary>
        private static void SortList(List<ConnectionGene> connectionList)
        {
            connectionList.Sort(delegate(ConnectionGene x, ConnectionGene y)
                {// Use simple/fast diff method.
                    return (int)x.SourceNodeId - (int)y.SourceNodeId;
                });
        }

        /// <summary>
        /// Creates the 2D-array with the regulatory neurons that belong to
        /// each pandemonium. Regulatory neurons are given by their index
        /// in the phenome activation lists.
        /// </summary>
        private static void CreatePandemoniumArray()
        {
            CountPandemoniums();
            EnsurePandemGroupZeroInDictionary();
            phenomeVariables.numberOfPandem = pandemoniumsFound.Count;
            DictionaryToPhenomeVariables();
        }

        /// <summary>
        /// Counts the different pandemoniumn groups there are in the genome.
        /// Also counts the number of regulatory neurons in each pandemonium.
        /// This information is stored in a dictionray.
        /// </summary>
        private static void CountPandemoniums()
        {
            // Regulatory neurons are the last elements in the genome base.
            // We have easy access to the total number of regulatory neurons and
            // the index for the last, and we use these to make loops.
            int firstRegIndex = genome.NeuronGeneList.LastBase - genome.Regulatory + 1;
            int lastRegIndex = genome.NeuronGeneList.LastBase;

            // Actually some steps could be only done for one genome in the list!
            // This happens here, as regulatory neurons are the same for all.
            // For the moment, slow, easy and safe path:
            pandemoniumsFound = new SortedDictionary<int, int>();
            for (int i = firstRegIndex; i <= lastRegIndex; ++i)
            {
                int pandem = genome.NeuronGeneList[i].Pandemonium;
                // If the current pandemonium is in the list...
                if (pandemoniumsFound.ContainsKey(pandem))
                {
                    // We increase the count for that pandemonium group.
                    ++pandemoniumsFound[pandem];
                }
                else
                {
                    // We add a new pandemonium value, with count = 1;
                    pandemoniumsFound.Add(pandem, 1);
                }
            }            
        }

        /// <summary>
        /// The first pandemonium group ("0") is treated in a special way
        /// (these are modules with no group). We make sure there is always
        /// an entry for this in the dictionary.
        /// </summary>
        private static void EnsurePandemGroupZeroInDictionary()
        { 
            if (!pandemoniumsFound.ContainsKey(0))
            {
                // Adds pandemonium group 0, with 0 regulatory neurons.
                pandemoniumsFound.Add(0, 0);
            }  
        }

        /// <summary>
        /// Takes the information in the dictionary "pandemoniumsFound" and writes
        /// this into the phenomeVariables object (with faster? more simple elements).
        /// .pandemonium[i][] is the list of modules in pandemonium i. Values
        /// are the indices of the neurons in the phenome list instead of the
        /// module ID!
        /// .pandemoniumCounts[i] is the number of modules in pandemonium i
        /// </summary>
        private static void DictionaryToPhenomeVariables()
        {
            InitializePandemoniumAndMakeCountVariables();
            WriteValuesInPhenomeVariables();
        }

        private static void InitializePandemoniumAndMakeCountVariables()
        {
        	phenomeVariables.pandemoniumCounts = new int[pandemoniumsFound.Count];
        	phenomeVariables.pandemonium = new int[pandemoniumsFound.Count][];
        	// We go through the elements in the dictionary to initialize the
        	// second dimension of the array (number of regulatory neurons in this group)
        	int order = 0;
        	foreach (KeyValuePair<int, int> entry in pandemoniumsFound)
        	{
        		phenomeVariables.pandemoniumCounts[order] = entry.Value;
        		phenomeVariables.pandemonium[order] = new int[entry.Value];
                ++order;
            }
        }

        /// <summary>
        /// Writes the index of each regulatory neuron in phenomeVariables.pandemonium.
        /// phenomeVariables.pandemonium[i][] will be the indices for the
        /// regulatory neurons in pandemonium group i, where now groups are
        /// perfectly sorted (i.e., groups "0, 3, 4" are now "0, 1, 2").
        /// </summary>
        private static void WriteValuesInPhenomeVariables()
        {
            // Dictionary to help sort pandemonium groups (so IDs "0, 3, 4"
            // correspond to "0, 1, 2" in the phenome).
            Dictionary<int, int> pandemoniumGroupToOrder = BuildPandemoniumGroupToOrder();

            int numberOfGroups = pandemoniumGroupToOrder.Count;
            // regInModuleCurrent counts the number of modules already added
            // to each pandemonium group.
            int[] regInModuleCurrent = new int[numberOfGroups];
            Array.Clear(regInModuleCurrent, 0, numberOfGroups);

            int firstRegIndex = genome.NeuronGeneList.LastBase - genome.Regulatory + 1;
            int lastRegIndex = genome.NeuronGeneList.LastBase;
            for (int i = firstRegIndex; i <= lastRegIndex; ++i)
            {
                // pandOrder is the group to which belongs the regulatory neuron.
                // This is now in order (for example, for pandemonium values
                // 0, 3 and 4, here 0 would take group 0, 3 would take group 1
                // and 4 would take group 2).
                int pandOrder = pandemoniumGroupToOrder[genome.NeuronGeneList[i].Pandemonium];
                // The node index for regulatory neurons is the same in the processed
                // phenomes and in the genomes!
                phenomeVariables.pandemonium[pandOrder][regInModuleCurrent[pandOrder]] =
                		idToNewIndex[genome.NeuronGeneList[i].Id];
                // We increase the counter for this pandemonium group.
                ++regInModuleCurrent[pandOrder];
            }
        }

        /// <summary>
        /// pandemoniumsFound stores the modules in each pandemonium group, but
        /// these groups may take any int values, such as "0, 3, 4". In the 
        /// phenome we want them in better order, so that corresponds to 
        /// "0, 1, 2".
        /// </summary>
        private static Dictionary<int, int> BuildPandemoniumGroupToOrder()
        {
        	Dictionary<int, int> GroupToOrder = new Dictionary<int, int>();
        	// Note that pandemoniumsFound is a sorted dictionary!
        	int order = 0;
        	foreach (KeyValuePair<int, int> entry in pandemoniumsFound)
        	{
        		// entry.Key is the Group id.
        		GroupToOrder.Add(entry.Key, order);
        		++order;
        	}
        	return GroupToOrder;
        }

        /// <summary>
        /// Here we create an array with the activation function for each
        /// neuron in the genome. If we are not interested in using this, 
        /// we get the three basic functions needed: for standard neurons, 
        /// for regulatory neurons and for output neurons.
        /// </summary>
        private static void GetActivationFunctions()
        {
            // Constructs an array of neuron activation functions.
            INodeList nodeList = genome.NodeList;
            int nodeCount = nodeList.Count;
            IActivationFunctionLibrary activationFnLibrary = genome.ActivationFnLibrary;
            phenomeVariables.neuronActivationFnArray = new IActivationFunction[nodeCount];
            phenomeVariables.neuronAuxArgsArray = new double[nodeCount][];

            // Writes the function and auxiliary arguments in the phenome index,
            // so we use the oldToNewIndex dictionary.
            for (int i = 0; i < nodeCount; i++) {
                /*
                int cosa4;
                if (!oldToNewIndex.TryGetValue(i, out cosa4))
                {
                    UnityEngine.Debug.Log("ERROR4 " + i);
                }
                */
                phenomeVariables.neuronActivationFnArray[oldToNewIndex[i]] =
                    activationFnLibrary.GetFunction(nodeList[i].ActivationFnId);
                phenomeVariables.neuronAuxArgsArray[oldToNewIndex[i]] = nodeList[i].AuxState;
            }  

            // Here we get the three basic activation function types.
            for (int i = phenomeVariables.inputBiasCount; i < nodeCount; i++) {
                NodeType type = nodeList[i].NodeType;
                if (type == NodeType.Output)
				{
                    phenomeVariables.outputNeuronActivFn = 
                            activationFnLibrary.GetFunction(nodeList[i].ActivationFnId);
                }
                if (type == NodeType.Regulatory)
				{
                    phenomeVariables.regulatoryActivFn = 
                        activationFnLibrary.GetFunction(nodeList[i].ActivationFnId);
                }
                if (type == NodeType.Local_Output)
				{
                    phenomeVariables.normalNeuronActivFn = 
                        activationFnLibrary.GetFunction(nodeList[i].ActivationFnId);
                    break;
                }
            }
        }

        /// <summary>
        /// Copies some useful variables from the genome to the struct
        /// countVariables, which is passed to the phenome.
        /// The rest of the variables in countVariables need to be computed
        /// while in MakeDictionaries (except for timestepsPerActivation, 
        /// in the constructor, and nonProtectedCount, during 
        /// CreateFastConnectionArray).
        /// </summary>
        private static void GetEasyVariables()
        {
            phenomeVariables.neuronCount = genome.NeuronGeneList.Count;
            phenomeVariables.inputBiasCount = genome.Input + 1;
            phenomeVariables.outputCount = genome.Output;
            phenomeVariables.regulatoryCount = genome.Regulatory;
        }

        /// <summary>
        /// Makes a dictionary that gives the index of a neuron with a given Id.
        /// Then creates a dictionary with the new index for neurons.
        /// 
        /// The original index is for the genome neuron sorting:
        ///          -Input
        ///          -Output
        ///          -Regulatory
        ///          -Modules (active module, last)
        ///              -Local input (all)
        ///              -Local output (all)
        ///              -Hidden
        /// 
        /// The new sorting is used in phenomes:
        ///          -Input
        ///          -Output
        ///          -Regulatory
        ///          -Hidden and local output (with targets other than output neurons)
        ///          -Local input (with fixed input from bias and input neurons)
        ///          -Local input (with local output as sources)
        ///          -Local output (with output neurons as only target)
        /// </summary>
        private static void MakeDictionaries()
        {
            NeuronGeneList nodeList = genome.NeuronGeneList;
            int nodeCount = nodeList.Count;
            // Do not need to add 1 because the index starts at 0.
            uint biasAndInput = (uint)genome.Input;
            uint maxOut = biasAndInput + (uint)genome.Output;

            idToOldIndex = new Dictionary<uint,int>(nodeCount);
            oldToNewIndex = new Dictionary<int,int>(nodeCount);
            idToNewIndex = new Dictionary<uint,int>(nodeCount);

            int current = 0;
            List<int> localInIndexList = new List<int>();
            List<int> localInFromLocalOutIndexList = new List<int>();
            List<int> localOutToOutIndexList = new List<int>();

            // This counts neurons with only one connection from them, with
            // an output neuron as target. Local output neurons with output
            // neurons AND recursive non-protected connections are NOT considered
            // here!
            // Perhaps we should make no difference between different types
            // of local output neurons?
            phenomeVariables.localOutToOnlyOut = 0;

            // This dictionary will store the order in which modules appear in
            // the genome, remember they do not need to be in order!
            moduleIdToIndex= new Dictionary<int, int>();
            // Loops through regulatory neurons to list their moduleId and the
            // order in which they are found. NOTE we only need to give every
            // moduleId a unique index, but it is not really necessary that this
            // index be the order in which it is found in the genome. Because
            // the indices will be used for arryas, we only need them to start
            // at 0 and have no gaps.
            int currentModuleId = 0;
            int currentModuleIndex = 0;

            // + 1 because input does not include bias
            int offset = genome.Input + genome.Output + 1;
            for (int i = 0; i < genome.Regulatory; ++i)
            {
                if (nodeList[i + offset].ModuleId != currentModuleId)
                {
                    currentModuleId = nodeList[i + offset].ModuleId;
                    moduleIdToIndex.Add(currentModuleId, currentModuleIndex);
                    ++currentModuleIndex;
                }                
            }


            // Creates Id to Old index dictionary.
            // Adds the first elements to the Old-to-New-index dictionary. And
            // saves the elements that come next.
            for (int i = 0; i < nodeCount; i++) {
                // The first dictionary simply gives the index for the ID of
                // a neuron gene.
                idToOldIndex.Add(nodeList[i].Id, i);

                // If neurons are not local input or local output to output
                // we include them in order in the second dictionary (old index
                // to new index). 
                // Local input are stored in a list and added next.
                // Finally we add local output to output neurons at the end 
                // of the dictionary.
                // We take this chance to count the total number of local input
                // and local output (different kinds). Note we are
                // interested in all modules now, not only the active one.
                if (!(nodeList[i].NodeType == NodeType.Local_Input ||
                      nodeList[i].NodeType == NodeType.Local_Output))
                {
                    oldToNewIndex.Add(i, current);
                    idToNewIndex.Add(nodeList[i].Id, current);
                    ++current;
                }
                else if (nodeList[i].NodeType == NodeType.Local_Input)
                {
                    if (!HashSetContainsBiggerThan(nodeList[i].SourceNeurons,
                                                   biasAndInput))
                    {
                        // Local in with bias or input as source
                        localInIndexList.Add(i);                        
                    }
                    else
                    {
                        // Local in with local out as source
                        localInFromLocalOutIndexList.Add(i);
                    }
                }
                // We need to know if the target is a regulatory neuron
                // (or local input) or an output neuron. In the first case they
                // are added, mixed with other hidden neurons.
                else
                {
                    if (HashSetContainsBiggerThan(nodeList[i].TargetNeurons,
                        maxOut))
                    {
                        // The target has Id > max output --> this local output
                        // neuron has regulatory or local in targets, or some
                        // non-protected connections.
                        oldToNewIndex.Add(i, current);
                        idToNewIndex.Add(nodeList[i].Id, current);
                        ++current;
                    }
                    else
                    {
                        // The target has Id <= max output --> local out to out (ONLY)
                        localOutToOutIndexList.Add(i);
                        ++phenomeVariables.localOutToOnlyOut;
                    }
                }
            }
            // Adds the local input neurons (with bias and input as sources)
            for (int i = 0; i < localInIndexList.Count; ++i)
            {
                oldToNewIndex.Add(localInIndexList[i], current);
                idToNewIndex.Add(nodeList[localInIndexList[i]].Id, current);
                ++current;
            }

            // Adds the local input neurons (with local output neurons as sources)
            for (int i = 0; i < localInFromLocalOutIndexList.Count; ++i)
            {
                oldToNewIndex.Add(localInFromLocalOutIndexList[i], current);
                idToNewIndex.Add(nodeList[localInFromLocalOutIndexList[i]].Id, current);
                ++current;
            }

            // Adds the local output to output neurons
            for (int i = 0; i < localOutToOutIndexList.Count; ++i)
            {
                oldToNewIndex.Add(localOutToOutIndexList[i], current);
                idToNewIndex.Add(nodeList[localOutToOutIndexList[i]].Id, current);
                ++current;
            }
            // We updates useful counts!
            phenomeVariables.localInFromBiasInCount = localInIndexList.Count;
            phenomeVariables.localInFromLocalOutCount = localInFromLocalOutIndexList.Count;
        }

        /// <summary>
        /// Copied from Visualizer!
        /// </summary>
        private static bool HashSetContainsBiggerThan(HashSet<uint> hashSet, uint value)
        {
            foreach (uint element in hashSet)
            {
                if (element > value)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion
    }
}
