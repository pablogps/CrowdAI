using System;

namespace Evolution
{
    public class EvolutionEventsArgs : EventArgs
    {
        public string userName;
        public int candidateIndex;
        public string folderName;
        public bool isNormalMutations;
    }
}
