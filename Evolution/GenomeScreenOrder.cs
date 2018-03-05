using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpNeat.Genomes.Neat;
using SharpNeat.Core;

namespace Evolution
{
    /// <summary>
    /// This class remembers the index assigned to each genome. This index
    /// is used to represent the same genome (if it is still present after
    /// reproduction) in the same place. This is convenient since it allows
    /// to easily skip the production of new videos when old ones can be used.
    /// 
    /// The index is stored in a string in each genome.
    /// 
    /// GenomeScreenOrder was conceived as a static class, but that is
    /// problematic with different users having parallel evolution events.
    /// </summary>
    public class GenomeScreenOrder<TGenome>
        where TGenome : class, IGenome<TGenome>
    {
        private Dictionary<uint, int> idToIndex;
        public Dictionary<uint, int> IdToIndex
        {
            get { return idToIndex; }
            set { idToIndex = value; }
        }

        private Dictionary<int, string> indexToName;
        public Dictionary<int, string> IndexToName
        {
            get { return indexToName; }
            set { indexToName = value; }
        }

        private int candidatesUsed;
        private string nextUnusedName;
        private Dictionary<uint, int> oldIDtoIndex;
        private List<TGenome> offspringList;
        
        public GenomeScreenOrder()
        {
            candidatesUsed = 0;
            nextUnusedName = "Candidate0";
            ResetDictionaries();
        }

        public void ResetDictionaries()
        {
            idToIndex = new Dictionary<uint, int>();
            indexToName = new Dictionary<int, string>();
        }

        public void ProcessNewGeneration(List<TGenome> newOffspringList, string userName)
        {
            offspringList = newOffspringList;
            oldIDtoIndex = idToIndex;
            idToIndex = new Dictionary<uint, int>();
            CopyParentsToDictionary();
            DeleteOldVideos(userName);
            AddNewChildren();
        }

        private void CopyParentsToDictionary()
        {
            foreach (TGenome child in offspringList)
            {
                if (oldIDtoIndex.ContainsKey(child.Id))
                {
                    int oldIndex = oldIDtoIndex[child.Id];
                    idToIndex.Add(child.Id, oldIndex);
                    AssignNameToGenome(child, indexToName[oldIndex]);
                }
            }
        }

        /// <summary>
        /// Checks all possible indices (from 0 to the number of candidates).
        /// If the index is in already in the new dictionary, it is reused
        /// by a champion genome. Otherwise it corresponds to a child genome,
        /// and the video file (and its name) will be new, so we delete those
        /// videos!
        /// </summary>
        private void DeleteOldVideos(string userName)
        {
            for (int index = 0; index < offspringList.Count; ++index)
            {
                if (!idToIndex.ContainsValue(index))
                {
                    // Delete video with name: indexToName[index]
                    if (indexToName.ContainsKey(index))
                    {
                        SharpNeat.PopulationReadWrite.DeleteVideo(indexToName[index], userName);
                    }
                }
            }
        }

        private void AddNewChildren()
        {
            foreach (TGenome child in offspringList)
            {
                // If the ID is not yet added...
                if (!idToIndex.ContainsKey(child.Id))
                {
                    int newIndex = FindSmallestAvailableIndex();
                    idToIndex.Add(child.Id, newIndex);
                    UpdateNextUnusedName();
                    indexToName[newIndex] = nextUnusedName;
                    AssignNameToGenome(child, nextUnusedName);
                }
            }
        }

        private int FindSmallestAvailableIndex()
        {
            int smallestIndex = 0;
            bool searching = true;
            bool indexInUse;
            while (searching)
            {
                indexInUse = false;
                foreach (KeyValuePair<uint, int> entry in idToIndex)
                {
                    if (entry.Value == smallestIndex)
                    {
                        indexInUse = true;
                        break;
                    }
                }
                // If this index is not in use, then it is available and
                // the search can end! Otherwise tries the next.
                if (!indexInUse)
                {
                    searching = false;
                }
                else
                {
                    ++smallestIndex;
                }
            }
            return smallestIndex;
        }

        private void AssignNameToGenome(TGenome genome, string newName)
        {
            genome.CandidateName = newName;
        }

        private void UpdateNextUnusedName()
        {
            ++candidatesUsed;
            nextUnusedName = "Candidate" + candidatesUsed.ToString();
        }
    }
}
