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
using System.Collections;
using System.Collections.Generic;
using SharpNeat.Network;

namespace SharpNeat.Genomes.Neat
{
    /// <summary>
    /// Represents a sorted list of NeuronGene objects. The sorting of the items
    /// is done on request rather than being strictly enforced at all times 
    /// (e.g. as part of adding and removing items). This approach is currently
    /// more convenient for use in some of the routines that work with NEAT genomes.
    /// 
    /// Because we are not using a strictly sorted list such as the generic 
    /// class SortedList[K,V] a customised BinarySearch() method is provided for
    /// fast lookup of items if the list is known to be sorted. If the list is
    /// not sorted then the BinarySearch method's behaviour is undefined. This 
    /// is potentially a source of bugs and thus this class should probably 
    /// migrate to SortedList[K,V] or be modified to ensure items are sorted 
    /// prior to a binary search.
    /// 
    /// At the moment the list should be sorted, but it may not need to be so
    /// in the future (ESP), mainly if edition of old modules is allowed. In this
    /// case the list will be locally sorted. Example:
    /// 
    /// neuron base (bias + input + output + regulatory)
    ///                 .
    ///                 .
    ///                 .
    /// local input neurons in module i     (sorted)
    /// local output neurons in module i    (sorted)
    /// hidden neurons in module i          (sorted)
    /// local input neurons in module i+1   (sorted)
    /// local output neurons in module i+1  (sorted)
    /// hidden neurons in module i+1        (sorted)
    /// local input neurons in module i-n   (sorted)
    /// local output neurons in module i-n  (sorted)
    /// hidden neurons in module i-n        (sorted)
    /// (end)
    /// 
    /// For this reason, as well as for performance, BinarySearch will be 
    /// restricted to the relevant part of the list (remember that only one 
    /// module, the last in the list, will be active at any given time in ESP).
    ///  
    /// Sort order is with respect to connection gene innovation ID.
    /// </summary>
    public class NeuronGeneList : List<NeuronGene>, INodeList
    {
        static readonly NeuronGeneComparer __neuronGeneComparer = 
                new NeuronGeneComparer();
        // Indicates the first neuron that belongs to the current module.
        // It will be of type local_input (regulatory neurons are stored
        // before the modules).
        static private int _firstModuleIndex = 0;
        // This will be the last neuron in the base of the genome. This includes
        // bias, input, global output and regulatory.
        static private int _lastBaseIndex = 0;

        #region Constructors

        /// <summary>
        /// Construct an empty list.
        /// </summary>
        public NeuronGeneList()
        {
        }

        /// <summary>
        /// Construct an empty list with the specified capacity.
        /// </summary>
        public NeuronGeneList(int capacity) : base(capacity)
        {
        }

        /// <summary>
        /// Copy constructor. The newly allocated list has a capacity 1 larger than copyFrom
        /// allowing for a single add node mutation to occur without reallocation of memory.
        /// </summary>
        public NeuronGeneList(ICollection<NeuronGene> copyFrom) 
            : base(copyFrom.Count+1)
        {
            // ENHANCEMENT: List.Foreach() is potentially faster than a foreach loop. 
            foreach(NeuronGene srcGene in copyFrom) {
                Add(srcGene.CreateCopy());
            }
        }

        #endregion

		/// <summary>
		/// Gets or sets the list index for the first neuron in the active module.
		/// </summary>
		public int FirstIndex
		{ 
			get { return _firstModuleIndex; }
			set { _firstModuleIndex = value; }
		}
		/// <summary>
		/// Gets or sets the list index for the last neuron in the base genome:
		/// bias + input + output + regulatory.
		/// </summary>
		public int LastBase
		{ 
			get { return _lastBaseIndex; }
			set { _lastBaseIndex = value; }
		}

        #region Public Methods

        public static void ResetStaticProperties()
        {
            _firstModuleIndex = 0;
            _lastBaseIndex = 0;
        }

        public void PrintNeuronListData()
        {
            System.Diagnostics.Debug.WriteLine("\nNeuron list data:");
            System.Diagnostics.Debug.WriteLine("Index for first element in active module: " + _firstModuleIndex);
            System.Diagnostics.Debug.WriteLine("Index for the last element in the base: " + _lastBaseIndex);
            foreach (NeuronGene neuron in this)
            {
                neuron.PrintNeuronInfo();
            }
        }

        /// <summary>
        /// Locates the last base item. 
        /// </summary>
        public void LocateLastBase()
        {
            // The first neuron that is NOT part of the base must be a 
            // local input type, and at least index 4 (bias + 1xInput + 
            // 1xOutput + 1xRegulatory --> local in with index 4).
            // But let us start with 3 in case there are no regulatory yet!
            // Really, maybe we could start at 0, in practice it would be the same!
            for (int index = 3; index < Count; ++index)
            {
                if (this[index].NodeType == NodeType.Local_Input)
                {
                    // Returns the index from the previous item!
                    _lastBaseIndex = index - 1;
                    return;
                }
            }
            // If we are here it means there are no local input yet, so the
            // last base is... the last index!
            _lastBaseIndex = Count - 1;
        }

        /// <summary>
        /// Used to count the number of local in neurons when an old module
        /// us set as active.
        /// </summary>
        public int CountLocalIn()
        {
            int returnCount = 0;
            int activeModuleId = this[Count - 1].ModuleId;
            for (int i = 0; i < Count; ++i)
            {
                if (this[i].NodeType == NodeType.Local_Input &&
                    this[i].ModuleId == activeModuleId)
                {
                    ++returnCount;
                }
            }
            return returnCount;
        }
        /// <summary>
        /// Should this be a different method or should it be fused with
        /// CountLocalIn?
        /// </summary>
        public int CountLocalOut()
        {
            int returnCount = 0;
            int activeModuleId = this[Count - 1].ModuleId;
            for (int i = 0; i < Count; ++i)
            {
                if (this[i].NodeType == NodeType.Local_Output &&
                    this[i].ModuleId == activeModuleId)
                {
                    ++returnCount;
                }
            }
            return returnCount;
        }

        /// <summary>
        /// Locates the first neuron in the active module (the last module in
        /// the list!) 
        /// </summary>
        public void LocateFirstIndex()
        {
            // By default, the first after the base (useful in case there is only
            // one module, where the if within the loop will never be true!)
            _firstModuleIndex = _lastBaseIndex + 1;

            // Starting at the end, looks for the first element with different
            // module ID (end of the active module)
			int activeModule = this[Count - 1].ModuleId;
            for (int index = Count - 1; index > _lastBaseIndex; --index)
            {
                // If the element belongs to a different module, then the 
                // previous item was the first in the active module!
                if (this[index].ModuleId != activeModule)
                {
                    // Returns the index of the previous item (notice we
                    // are going backwards).
                    _firstModuleIndex = index + 1;
                    return;
                }
			}

			// Before adding the first module, ALL elements have module Id = 0.
            if (this[Count - 1].ModuleId == 0)
            {
                _firstModuleIndex = 0;
            }
        }

        /// <summary>
        /// Inserts a NeuronGene into its correct (sorted) location within the gene list.
        /// Normally neuron genes can safely be assumed to have a new Innovation ID higher
        /// than all existing IDs, and so we can just call Add().
        /// This routine handles genes with older IDs that need placing correctly.
        /// </summary>
        public void InsertIntoPosition(NeuronGene neuronGene)
        {
            // Determine the insert idx with a linear search, starting from the end 
            // since mostly we expect to be adding genes that belong only 1 or 2 genes
            // from the end at most.

            // ESP: Luckily this gets us to the correct module as well!
            // The first neurons in the module are local input and local output
            // neurons, which take the first ID values. We even reserve some
            // IDs in case we want to add new local in/out neurons later, so this
            // will always have a lower ID than hidden neurons.
            int idx = Count - 1;
            for (; idx > -1; idx--)
            {
                if (this[idx].InnovationId < neuronGene.InnovationId)
                {   // Insert idx found.
                    break;
                }
            }
            Insert(idx + 1, neuronGene);
        }

        /// <summary>
        /// Remove the neuron gene with the specified innovation ID.
        /// Returns the removed gene.
        /// </summary>
        public NeuronGene Remove(uint neuronId)
        {
            int idx = BinarySearch(neuronId);
            if (idx < 0) {
                throw new ApplicationException("Attempt to remove neuron with " +
                                               "an unknown neuronId");
            } 
            NeuronGene neuronGene = this[idx];
            RemoveAt(idx);
            return neuronGene;
        }

        /// <summary>
        /// Gets the neuron gene with the specified innovation ID using a fast 
        /// binary search. Returns null if no such gene is in the list.
        /// 
        /// NOTICE! This method should NOT be used in ESP, since modules may not
        /// be in order. The only alternative would be to have a specific
        /// method to search only the active module, getting the module Id as
        /// a parameter or something like that. The cost of using GetNeuronByIdAll
        /// is probably not so important in any case (specially in interactive
        /// evolution, the time spent in producing a new generation is not
        /// so relevant).
        /// </summary>
        public NeuronGene GetNeuronById(uint neuronId)
        {
            int idx = BinarySearch(neuronId);
            if (idx < 0) 
            {   // Not found.
                return null;
            }
            return this[idx];
        }

        /// <summary>
        /// Slower search algorithm that look through ALL the list (not just the
        /// active module!). Returns null if no such gene is in the list.
        /// </summary>
        public NeuronGene GetNeuronByIdAll(uint neuronId)
        {
            for (int idx = 0; idx < Count; ++idx)
            {
                if (this[idx].Id == neuronId)
                {
                    return this[idx];
                }
            }
            // If not found:
            return null;
        }

        /// <summary>
        /// Given a module Id and a position (for example, "second in module
        /// three") finds the Id for the corresponding local output neuron.
        /// This is used in visualizer when the user wants to connect an old
        /// local output neuron with a new local input. 
        /// </summary>
        public uint GetNeuronByModAndPosition(int module, int position)
        {
            // Because in Visualizer counts modules diferently (it does not need
            // to take the base into account).
            ++module;
            bool started = false;
            int currentPosition = 0;

            // We can avoid the base (from 0 to _lastBaseIndex, included)
            // We can also avoid, at the very least, one local input neuron.
            for (int i = _lastBaseIndex + 2; i < Count; ++i)
            {
                // When we get to the local output neurons in the desired module
                // we start to count.
                if (this[i].ModuleId == module &&
                    this[i].NodeType == NodeType.Local_Output)
                {
                    started = true;
                }
                // If we are countin:
                if (started)
                {
                    if (currentPosition == position)
                    {
                        return this[i].Id;
                    }
                    ++currentPosition;
                }
            }
            // This should NOT happen! This method should only be called expecting
            // success.
            return 0;
        }

		/// <summary>
		/// Finds the index for the last local output neuron added to the module.
        /// At first we were interested in teh ID, not the index, but from the
        /// index it is easy to get the ID, not the other way around.
		/// </summary>
        public bool FindLastLocalOut(int module, out int index)
		{
            // Counts backwards, since most often we will be interested in the
            // active module.
            for (int i = Count - 1; i > 0; --i)
            {
                if (this[i].ModuleId == module && 
                    this[i].NodeType == NodeType.Local_Output)
                {
                    index = i;
                    return true;
                }
            }
            // We need to assign some value to our out parameters, even in
            // failure. This should NOT happen!
            index = 0;
            return false;			
		}

        /// <summary>
        /// Returns the index for the regulatory neuron belonging to the 
        /// given module.
        /// </summary>
        public bool FindRegulatory(int module, out int index)
        {            
            // We start counting from _lastBaseIndex, the last regulatory
            // neuron in the list!
            for (int i = _lastBaseIndex; i > 0; --i)
            {
                // We also check that the type is regulatory.
                if (this[i].ModuleId == module && 
                    this[i].NodeType == NodeType.Regulatory)
                {
                    index = i;
                    return true;
                }
            }
            // We need to assign some value to our out parameters, even in
            // failure.
            index = 0;
            return false;
        }

        /// <summary>
        /// Tries to find the propossed target for a new local output neuron.
        /// This version is to find an output neuron as target.
        /// Returns the innovation ID, not the index.
        /// For regulatory neuron targets we use FindRegulatory (and then
        /// we get the ID from the idx that method returns).
        /// </summary>
        public bool FindTargetOut(int targetNum, out uint targetId)
        {
            // We need to assign some value to our out parameters, even in
            // failure.
            targetId = 0;
            bool started = false;
            uint count = 1;
            // At the very least we can safely skip the bias and one input
            // neuron. This should be fast, so we do not really need to bother
            // getting the exact first neuron in the list.
            for (int i = 2; i < Count;++i)
            {
                // When we get to the first output neuron, starts counting.
                if (!started && this[i].NodeType == NodeType.Output)
                {
                    started = true;
                }
                // If we get into the regulatory section we return fail.
                if (started && this[i].NodeType == NodeType.Regulatory)
                {
                    return false;
                }
                // We count output neurons (count starting from 1, since this 
                // is used in a human interface).
                if (started)
                {
                    if (count == targetNum)
                    {
                        targetId = this[i].Id;
                        return true;
                    }
                    else
                    {
                        ++count;
                    }
                }
            }
            return false;
        } 

        /// <summary>
        /// Sort neuron gene's into ascending order by their innovation IDs.
        /// Only sorts the active module.
        /// </summary>
        public void SortByInnovationId()
        {
            // We only want to sort the non-protected connections, both for
            // eficiency and also to require only local sorting (for example
            // we can use this to continue evolving an old module, which would
            // be taken to the end of the neuron and connection lists.
            // Old version: Sort(__neuronGeneComparer);

            // Fist we get only the active module in a new List.
            NeuronGeneList littleList = new NeuronGeneList(Count -
                _firstModuleIndex);
            for (int i = _firstModuleIndex; i < Count; ++i)
            {
                littleList.Add(this[i]);
            }
            // Now we sort only the active part...
            littleList.Sort(__neuronGeneComparer);
            // And we copy back these values.
            int small_indx = 0;
            for (int i = _firstModuleIndex; i < Count; ++i)
            {
                this[i] = littleList[small_indx];
                ++small_indx;
            }
        }

        /// <summary>
        /// Obtain the index of the gene with the specified ID by performing a 
        /// binary search. Binary search is fast and can be performed so long as
        /// the genes are sorted by ID. If the genes are not sorted then the 
        /// behaviour of this method is undefined.
        /// 
        /// This method will only search in the active module!
        /// </summary>
        public int BinarySearch(uint id) 
        {    
            // If our target id is smaller than that of the first element
            // in our module, then it must be in the genome base!
            // But perhaps the opposite is more likely, so we test that first.
            int lo;
            int hi;

            if (id >= this[_firstModuleIndex].Id)
            {
                // We only want to search in the current module.
                lo = _firstModuleIndex;
                hi = Count - 1;                   
            }
            else
            {     
                // We only want to search in the genome base.           
                lo = 0;
                hi = _lastBaseIndex;                   
            }  

            // Now that we have the correct limits we can begin searching!
            while (lo <= hi) 
            {
                // This gets the middle index. For lo = 0 and hi = 10, i = 5.
                // In base 10 moving all digits to the right is division by 10.
                // In base 2 it is division by 2!                
                int i = (lo + hi) >> 1;

                if (this[i].Id < id) {
                    // Our low index is now the middle + 1
                    lo = i + 1;
                }
                else if(this[i].Id > id) {
                    // Our hight index is now the middle - 1
                    hi = i - 1;
                }
                else {
                    return i;
                }
            }   
            // If there is no success, returns -1         
            return -1; 
        }

        /// <summary>
        /// For debug purposes only. Don't call this method in normal circumstances as it is an
        /// expensive O(n) operation.
        ///
        /// Also: if modifying old modules is allowed the complete list may
        /// NOT be sorted! (Only locally, each module.)
        /// </summary>
        public bool IsSorted()
        {
            int count = this.Count;
            if(0 == count) {
                return true;
            }

            uint prev = this[0].InnovationId;
            for(int i=1; i<count; i++)
            {
                if(this[i].InnovationId <= prev) {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region INodeList<INetworkNode> Members

        INetworkNode INodeList.this[int index]
        {
            get { return this[index]; }
        }

        int INodeList.Count
        {
            get { return this.Count; }
        }

        IEnumerator<INetworkNode> IEnumerable<INetworkNode>.GetEnumerator()
        {
            foreach(NeuronGene nGene in this) {
                yield return nGene;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<INetworkNode>)this).GetEnumerator();
        }

        #endregion
    }
}
