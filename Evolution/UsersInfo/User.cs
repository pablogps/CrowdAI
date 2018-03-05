
namespace Evolution.UsersInfo
{
    public class User
    {
        public bool IsEvolutionReady { get; set; }
        public bool IsWaitingForVideos { get; set; }

        private bool isEvolutionRunning;
        public bool IsEvolutionRunning { get; set; }

        private int? parent;
        public int? Parent { get; set; }

        private System.Diagnostics.Stopwatch timeSincePing;

        public User()
        {
            isEvolutionRunning = false;
            parent = null;
            timeSincePing = new System.Diagnostics.Stopwatch();
            timeSincePing.Start();
            IsEvolutionReady = false;
            IsWaitingForVideos = true;
        }

        public int TimeSinceLastPing()
        {
            return timeSincePing.Elapsed.Minutes;
        }

        public void ResetTimer()
        {
            System.Diagnostics.Debug.WriteLine("Current elapsed time: " + TimeSinceLastPing().ToString());
            timeSincePing.Restart();
            System.Diagnostics.Debug.WriteLine("Stopwach reset: " + TimeSinceLastPing().ToString());
        }
    }
}

// This won't work for some reason I cannot fully understand yet. So evolutionAlgorithm is kept
// in a dictionary in ActiveUsersList :(
/*

using System;
using SharpNeat.Core;
using SharpNeat.EvolutionAlgorithms;

namespace Evolution.UsersInfo
{
    public class User<TGenome>
        where TGenome : class, IGenome<TGenome>
    {
        private NeatEvolutionAlgorithm<TGenome> evolutionAlgorithm;
        public NeatEvolutionAlgorithm<TGenome> EvolutionAlgorithm { get; set; }

        private bool isEvolutionRunning;
        public bool IsEvolutionRunning { get; set; }

        private int? parent;
        public int? Parent { get; set; }

        private System.Diagnostics.Stopwatch timeSincePing;

        public User() : this(null) {}
        public User(NeatEvolutionAlgorithm<TGenome> givenEvolutionAlgorithm)
        {
            evolutionAlgorithm = givenEvolutionAlgorithm;
            isEvolutionRunning = false;
            parent = null;
            timeSincePing = new System.Diagnostics.Stopwatch();
        }

        public int TimeSinceLastPing()
        {
            return timeSincePing.Elapsed.Seconds;
        }

        public void ResetTimer()
        {
            timeSincePing.Reset();
        }
    }
}

*/
