using System;
using System.Collections.Generic;

namespace MalmoObservations
{
	public class FourTiles
	{
        // For some reason { get; } is not working...
        private LevelInfo level0 = new LevelInfo();
        public LevelInfo Level0 { get { return level0; } }
        private LevelInfo levelSub1 = new LevelInfo();
        public LevelInfo LevelSub1 { get { return levelSub1; } }
        private LevelInfo levelSub2 = new LevelInfo();
        public LevelInfo LevelSub2 { get { return levelSub2; } }

        static List<List<int>> indicesGivenYaw;
		static bool isIndicesListCreated = false;

        // The information for the types of blocks around the player will be given 
        // by height levels (e.g., blocks at player level, one block down, etc.)
        public struct LevelInfo
        {
            public string ahead;
            public string left;
            public string right;
            public string back;
            public string frontLeft;
            public string frontRight;
            public string backLeft;
            public string backRight;
        }

        public FourTiles()
        {
            if (!isIndicesListCreated)
            {
                InitializeIndicesList();
                isIndicesListCreated = true;
            }
        }     

        public void SetTileIndicesGivenObs(JsonObservations observations)
        {
            if (observations.level0.Count == 0)
            {
                throw new IndexOutOfRangeException();
            }

            int yaw = (int)YawBetweenZeroAndThreeSixty(observations.Yaw);

            level0.ahead = observations.level0[indicesGivenYaw[yaw][0]];
            level0.left = observations.level0[indicesGivenYaw[yaw][1]];
            level0.right = observations.level0[indicesGivenYaw[yaw][2]];
            level0.back = observations.level0[indicesGivenYaw[yaw][3]];
            level0.frontLeft = observations.level0[indicesGivenYaw[yaw][4]];
            level0.frontRight = observations.level0[indicesGivenYaw[yaw][5]];
            level0.backLeft = observations.level0[indicesGivenYaw[yaw][6]];
            level0.backRight = observations.level0[indicesGivenYaw[yaw][7]];

            levelSub1.ahead = observations.levelSub1[indicesGivenYaw[yaw][0]];
            levelSub1.left = observations.levelSub1[indicesGivenYaw[yaw][1]];
            levelSub1.right = observations.levelSub1[indicesGivenYaw[yaw][2]];
            levelSub1.back = observations.levelSub1[indicesGivenYaw[yaw][3]];
            levelSub1.frontLeft = observations.levelSub1[indicesGivenYaw[yaw][4]];
            levelSub1.frontRight = observations.levelSub1[indicesGivenYaw[yaw][5]];
            levelSub1.backLeft = observations.levelSub1[indicesGivenYaw[yaw][6]];
            levelSub1.backRight = observations.levelSub1[indicesGivenYaw[yaw][7]];

            levelSub2.ahead = observations.levelSub2[indicesGivenYaw[yaw][0]];
            levelSub2.left = observations.levelSub2[indicesGivenYaw[yaw][1]];
            levelSub2.right = observations.levelSub2[indicesGivenYaw[yaw][2]];
            levelSub2.back = observations.levelSub2[indicesGivenYaw[yaw][3]];
            levelSub2.frontLeft = observations.levelSub2[indicesGivenYaw[yaw][4]];
            levelSub2.frontRight = observations.levelSub2[indicesGivenYaw[yaw][5]];
            levelSub2.backLeft = observations.levelSub2[indicesGivenYaw[yaw][6]];
            levelSub2.backRight = observations.levelSub2[indicesGivenYaw[yaw][7]];
        }
        
		double YawBetweenZeroAndThreeSixty(double rawYaw)
		{
			double newYaw = rawYaw;
			if (newYaw < 0)
			{
				newYaw += 360;
			}
			return newYaw;
		}

		/// <summary>
		/// This class needs to initialize indicesGivenYaw.
		/// This object stores the indices for the neighbours depending on
		/// the orientation (yaw). Neighbours are given in a 3x3 grid so
		/// we can discretisize yaw values and consider only 8 directions,
		/// the borders of which happen at odd number * 360 degrees / 16 (pieces)
		/// </summary>
		void InitializeIndicesList()
		{
			// The best way would probably be to load this from a file.
			// But this looks fast enough:
			indicesGivenYaw = new List<List<int>>();
			for (int discreteYaw = 0; discreteYaw < 360; ++discreteYaw)
			{
				List<int> neighboursIndicesList = new List<int>();
				if (discreteYaw < Math.Floor(1.0 * 360 / 16))
				{
					neighboursIndicesList = new List<int> { 7, 5, 3, 1, 8, 6, 2, 0 };
				}
				else if (discreteYaw < Math.Floor(3.0 * 360 / 16))
				{
					neighboursIndicesList = new List<int> { 6, 8, 0, 2, 7, 3, 5, 1 };
				}
				else if (discreteYaw < Math.Floor(5.0 * 360 / 16))
				{
					neighboursIndicesList = new List<int> { 3, 7, 1, 5, 6, 0, 8, 2 };
				}
				else if (discreteYaw < Math.Floor(7.0 * 360 / 16))
				{
					neighboursIndicesList = new List<int> { 0, 6, 2, 8, 3, 1, 7, 5 };
				}
				else if (discreteYaw < Math.Floor(9.0 * 360 / 16))
				{
					neighboursIndicesList = new List<int> { 1, 3, 5, 7, 0, 2, 6, 8 };
				}
				else if (discreteYaw < Math.Floor(11.0 * 360 / 16))
				{
					neighboursIndicesList = new List<int> { 2, 0, 8, 6, 1, 5, 3, 7 };
				}
				else if (discreteYaw < Math.Floor(13.0 * 360 / 16))
				{
					neighboursIndicesList = new List<int> { 5, 1, 7, 3, 2, 8, 0, 6 };
				}
				else if (discreteYaw < Math.Floor(15.0 * 360 / 16))
				{
					neighboursIndicesList = new List<int> { 8, 2, 6, 0, 5, 7, 1, 3 };
				}
				else
				{
					neighboursIndicesList = new List<int> { 7, 5, 3, 1, 8, 6, 2, 0 };
				}
				indicesGivenYaw.Add(neighboursIndicesList);
			}
		}
	}
}

/*      The code below is an old approach, useful for future reference for myself.
        Simple example of use of exceptions and try/catch.      
       
        public string Ahead(int index) 
		{
            return ReturnHeadingValue(ahead, index);
        }

        string ReturnHeadingValue(string[] array, int index)
        {
            try
            {
                int siftedIndex = TestIndex(index);
                return array[siftedIndex];
            }
            catch (IndexOutOfRangeException e)
            {
                return "IndexError!";
            }
        }

        int TestIndex(int index)
        {
            if (index > 0 || index < -2)
            {
                System.Diagnostics.Debug.WriteLine("Out of bounds index in FourTiles query");
                throw new IndexOutOfRangeException();
            }
            return index + 2;
        }
*/
