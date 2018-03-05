using System.Collections.Generic;

namespace MalmoObservations
{
	public class JsonObservations
	{
		public int DistanceTravelled { get; set; }
		public int TimeAlive { get; set; }
		public int MobsKilled { get; set; }
		public int PlayersKilled { get; set; }
		public int DamageTaken { get; set; }
		public double Life { get; set; }
		public int Score { get; set; }
		public int Food { get; set; }
		public int XP { get; set; }
		public bool IsAlive { get; set; }
		public int Air { get; set; }
		public string Name { get; set; }
		public double XPos { get; set; }
		public double YPos { get; set; }
		public double ZPos { get; set; }
		public double Pitch { get; set; }
		public double Yaw { get; set; }
		public int WorldTime { get; set; }
		public int TotalTime { get; set; }
        public List<string> level0 { get; set; }
        public List<string> levelSub1 { get; set; }
        public List<string> levelSub2 { get; set; }

        public JsonObservations()
        {
            level0 = new List<string>();
            levelSub1 = new List<string>();
            levelSub2 = new List<string>();
        }
	}
}