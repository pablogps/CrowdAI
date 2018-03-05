using System.Collections.Generic;

namespace SharpNeat.IecEsp
{
	/// <summary>
	/// This class simply encapsulates the multiple variables used in GUI 
	/// management in EspNeatOrganizer, to keep things tidy and less troublesome.
	/// We include a reset method.
    /// 
    /// This class previously used lists of lists (where the list for module
    /// 3 would be in the index = 3). They have been "upgraded" to dictionarys
    /// so that modules need not be in order, but so that the module Id is 
    /// still enough to find the desired list.
    /// 
    /// See "newLink" struct deffinition at the end of this script!
	/// </summary>
    public class UIvariables
    { 
        // Module ID list is the list with the modules in the genome (it helps
        // because they may not be in order, for example we may have
        // module Id 2, module Id 4, module Id 3).
        public List<int> moduleIdList;
        public Dictionary<int, List<int>> hierarchy;
        public Dictionary<int, int> pandemonium;
		public Dictionary<int ,List<newLink>> localOutputList;
		public Dictionary<int, List<newLink>> localInputList;
		public Dictionary<int, List<newLink>> regulatoryInputList;

        public bool tryReset = false;

        public UIvariables()
        {
            Reset();
        }

        public void Reset()
        {
			moduleIdList = new List<int>();
            hierarchy = new Dictionary<int, List<int>>();
            pandemonium = new Dictionary<int, int>();
			localOutputList = new Dictionary<int, List<newLink>>();
			localInputList = new Dictionary<int, List<newLink>>();
			regulatoryInputList = new Dictionary<int, List<newLink>>(); 
        }
	}

    /// <summary>
    /// This struct is used so we can have the complete information for new
    /// links in the same list. We need a source or target (other neuron) and
    /// the weight. (Located in the GuiVariables script.)
    /// </summary>
    public struct newLink
    {
        public uint otherNeuron;
        public double weight;
        public uint id;
    }
}
