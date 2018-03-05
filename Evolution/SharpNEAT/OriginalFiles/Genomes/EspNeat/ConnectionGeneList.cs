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
    /// Represents an organized list of ConnectionGene objects. The sorting 
    /// of the items is done on request rather than being strictly enforced at all 
    /// times (e.g. as part of adding and removing genes).
    /// 
    /// Because we are not using a strictly sorted list such as the generic 
    /// class SortedList[K,V] a customised BinarySearch() method is provided for
    /// fast lookup of items. If the list is not sorted then the BinarySearch 
    /// method's behaviour is undefined. This is a potential a source of bugs.
    ///
    /// At the moment the list should be sorted, but it may not need to be so
    /// in the future (ESP), mainly if edition of old modules is allowed. In this
    /// case the list will be locally sorted. Example:
    /// 
    ///                 .
    ///                 .
    ///                 .
    /// protected connections in module i   (sorted)
    /// connections in module i             (sorted)
    /// protected connections in module i+1 (sorted)
    /// connections in module i+1           (sorted)
    /// protected connections in module i-n (sorted)
    /// connections in module i-n           (sorted)
    /// (end)
    /// 
    /// For this reason, as well as for performance, BinarySearch will be 
    /// restricted to the relevant part of the list (remember that only one 
    /// module, the last in the list, will be active at any given time in ESP).
    ///  
    /// Sort order is with respect to connection gene innovation ID.
    /// </summary>
    public class ConnectionGeneList : List<ConnectionGene>, IConnectionList
    {
        static readonly ConnectionGeneComparer __connectionGeneComparer = 
                new ConnectionGeneComparer();
        // Indicates the fist connection that belongs to this module and is not protected.
        static private int _firstActiveIndex = 0;

        #region Constructors

        /// <summary>
        /// Construct an empty list.
        /// </summary>
        public ConnectionGeneList()
        {
        }

        /// <summary>
        /// Construct an empty list with the specified capacity.
        /// </summary>
        public ConnectionGeneList(int capacity) : base(capacity)
        {
        }

        /// <summary>
        /// Copy constructor. The newly allocated list has a capacity 2 larger 
        /// than copyFrom allowing addition mutations to occur without 
        /// reallocation of memory.
        /// Note that a single add node mutation adds two connections and a single
        /// add connection mutation adds one.
        /// </summary>
        public ConnectionGeneList(ICollection<ConnectionGene> copyFrom) : 
               base(copyFrom.Count + 2)
        {
            // ENHANCEMENT: List.Foreach() is potentially faster then a foreach loop. 
            // Comment: only by a negligible amount and it is less common.
            foreach (ConnectionGene srcGene in copyFrom) {
                Add(srcGene.CreateCopy());
            }
        }

        #endregion

        /// <summary>
        /// Returns the index for the first connection that is not protected 
        /// in the active module.
        /// </summary>
        public int FirstId
        { 
            get { return _firstActiveIndex; }
            set { _firstActiveIndex = value; }
        }

        #region Public Methods

        public static void ResetStaticProperties()
        {
            _firstActiveIndex = 0;
        }

        public void PrintConnectionListData()
        {
            System.Diagnostics.Debug.WriteLine("\nConnection list data:");
            System.Diagnostics.Debug.WriteLine("Index for first non-protected in active module: " + _firstActiveIndex);
            foreach (ConnectionGene connection in this)

			{
	            connection.PrintConnectionInfo();
            }
        }

        /// <summary>
        /// Locates the first non-protected connection in the active module 
        /// (the last module in the list!) Used to update _firstActiveIndex. 
        /// </summary>
        public void LocateFirstId()
        {
            // We need to set the value to 0 first, in case there are no 
            // connections at all! (So that the loop would never even begin!)
            _firstActiveIndex = 0;
            for (int index = Count - 1; index > -1; --index)
            {
                // If the element is protected, then the previous item was the 
                // first non-protected connection in the active module!
                // Notice we are going backwards.
                if (this[index].Protected)
                {
                    // Returns the index of the previous item.
                    _firstActiveIndex = index + 1;
                    return;
                }
            }
            // We should never get here! Perhaps add an exception or something.
        }

        /// <summary>
        /// Locates the index for the first connection in the given module.
        /// </summary>
        public int FindFirstInModule(int module)
        {
            // i = 0 is reserved for provisional bias-to-regulatory connections, 
            // which is given module Id = 0. We could skip this one.
            for (int i = 0; i < Count; ++i)
            {
                if (this[i].ModuleId == module)
                {
                    return i;
                }
            }
            // If there are none (should not be the case) returns a negative value.
            return -1;
        }

        /// <summary>
        /// Inserts an ConnectionGene into its correct (sorted) location 
        /// within the gene list.
        /// Normally connection genes can safely be assumed to have a new 
        /// Innovation ID higher than all existing IDs, and so we can just 
        /// call Add().
        /// This routine handles genes with older IDs that need placing correctly.
        /// </summary>
        public void InsertIntoPosition(ConnectionGene connectionGene)
        {
            // Determine the insert idx with a linear search, starting from the
            // end, since mostly we expect to be adding genes that belong only 
            // 1 or 2 genes from the end at most.

            // ESP: Luckily this gets us to the correct module as well!
            // When we create a module there are a few protected connections 
            // (from local_output neurons to their targets) and these take
            // the lowest ID values in the module, so there is no risk of running
            // into an older module, even if we are working with the first 
            // (non-protected) connection ID!

            // Even if we add new local_output neurons after the module has been
            // evolved, some ID values are reserved for these, so they will 
            // still be the lowest in the module!
            int idx = Count - 1;
            // We limit the search to non-protected connections.
            for(; idx >= _firstActiveIndex; --idx)
            {
                if (this[idx].InnovationId < connectionGene.InnovationId)
                {   // Insert idx found.
                    break;
                }
            }
            Insert(idx + 1, connectionGene);
        }

        /// <summary>
        /// Remove the connection gene with the specified innovation ID.
        /// </summary>
        public void Remove(uint innovationId)
        {
            int idx = BinarySearch(innovationId);
            if (idx < 0) {
                throw new ApplicationException("Attempt to remove connection " +
                                               "with an unknown innovationId");
            } 
            RemoveAt(idx);
        }

        /// <summary>
        /// Sort connection genes into ascending order by their innovation IDs.
        /// 
        /// Sorts the non-protected connections in the active module. The
        /// protected connections are sorted by construction, and we do not want
        /// to interfere with older modules.
        /// </summary>
        public void SortByInnovationId()
        {
            // Fist we get only the active part in a new List.
            ConnectionGeneList littleList = new ConnectionGeneList(Count -
                                                                   _firstActiveIndex);
            for (int i = _firstActiveIndex; i < Count; ++i)
            {
                littleList.Add(this[i]);
            }
            // Now we sort only the active part...
            littleList.Sort(__connectionGeneComparer);
            // And we copy back these values.
            int small_indx = 0;
            for (int i = _firstActiveIndex; i < Count; ++i)
            {
                this[i] = littleList[small_indx];
                ++small_indx;
            }
        }

        /// <summary>
        /// Obtain the index of the gene with the specified innovation ID by 
        /// performing a binary search. Binary search is fast and can be 
        /// performed so long as we know the genes are sorted by innovation ID.
        /// If the genes are not sorted then the behaviour of this method is undefined.
        /// 
        /// This method will only search in the active module!
        /// </summary>
        public int BinarySearch(uint innovationId) 
        {          
            // We only want to search in the current module, also excluding the
            // protected connections.
            int lo = _firstActiveIndex;
            int hi = Count - 1;

            while (lo <= hi) 
            {
                // This gets the middle index. For lo = 0 and hi = 10, i = 5.
                // In base 10 moving all digits to the right is division by 10.
                // In base 2 it is division by 2!
                int i = (lo + hi) >> 1;

                // Note. we don't calculate this[i].InnovationId-innovationId 
                // because we are dealing with uint.
                // ENHANCEMENT: List<T>[i] invokes a bounds check on each call. 
                // Can we avoid this?
                if (this[i].InnovationId < innovationId) {
                    // Our low index is now the middle + 1
                    lo = i + 1;
                } else if (this[i].InnovationId > innovationId) {
                    // Our hight index is now the middle - 1
                    hi = i - 1;
                } else {
                    return i;
                }
            }
            // If there is no success, returns -1
            return -1;
        }

        /// <summary>
        /// We cannot guarantee that the connections will be sorted (in case
        /// an old module is evolved, for example). We need a slower but
        /// reliable search method. Use when the sarch is intended for all
        /// connections, not only those in the active module!
        /// </summary>
        public int IndexForId(uint id)
        {
            for (int i = 0; i < Count; ++i)
            {
                if (this[i].InnovationId == id)
                {
                    return i;
                }
            }
            // If there is no success, returns -1
            return -1;
        }

        /// <summary>
        /// Finds a protected connection with the provided source
        /// </summary>
        public ConnectionGene FindProtectedWithSource(uint source)
        {
            for (int i = 0; i < Count; ++i)
            {
                if (this[i].SourceNodeId == source)
                {
                    return this[i];
                }
            }
            // IMPORTANT!
            // We should NEVER get here.
            // TODO: Issue warning.
            Console.WriteLine("Failed to find a protected connection with " +
                              "source: " + source);
            // We return the first connection so everything compiles fine, but
            // this should be done in a safer way!
            return this[0];
        }

        /// <summary>
        /// Resets the IsMutated flag on all EspConnectionGenes in the list.
        /// NOTICE: Loop starts with the first non-protected connection in the
        /// active module. If other connections may change (protected connections
        /// for example) they should be included in this loop. In case of doubt, 
        /// just take int i = 0 for all connections (genome mutations should not
        /// have a huge impact in performance).
        /// </summary>
        public void ResetIsMutatedFlags()
        {
			// NOTICE: i = _firstActiveIndex
			int count;
			count = Count;
            for (int i = _firstActiveIndex; i < count; ++i) {
                this[i].IsMutated = false;
            }
        }

        /// <summary>
        /// For debug purposes only. Don't call this method in normal 
        /// circumstances as it is an expensive O(n) operation.
        /// 
        /// Also: if modifying old modules is allowed the complete list may
        /// NOT be sorted! (Only locally, each module.)
        /// </summary>
        public bool IsSorted()
        {
			int count;
			count = Count;
            if (0 == count) {
                return true;
            }

            uint prev = this[0].InnovationId;
            for (int i = 1; i < count; i++)
            {
                if (this[i].InnovationId <= prev) {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region IConnectionList Members

        INetworkConnection IConnectionList.this[int index]
        {
            get { return this[index]; }
        }

        int IConnectionList.Count
        {
            get { return Count; }
        }

        IEnumerator<INetworkConnection> IEnumerable<INetworkConnection>.GetEnumerator()
        {
            foreach(ConnectionGene gene in this) {
                yield return gene;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<INetworkConnection>)this).GetEnumerator();
        }

        #endregion
    }
}