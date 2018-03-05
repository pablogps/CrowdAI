using System;
using System.Collections.Generic;

namespace MalmoObservations
{
    public class ManyTiles
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
            public string farAhead;
            public string farLeft;
            public string left;
            public string right;
            public string farRight;
            public string back;
            public string farBack;
            public string aheadLeft;
            public string aheadRight;
            public string backLeft;
            public string backRight;
            public string farAheadLeft;
            public string farAheadRight;
            public string farBackLeft;
            public string farBackRight;
        }

        public ManyTiles()
        {
            if (!isIndicesListCreated)
            {
                InitializeIndicesList();
                isIndicesListCreated = true;
            }
        }

        // Yes, this has got a bit out of hand...
        public void SetTileIndicesGivenObs(JsonObservations observations)
        {
            if (observations.level0.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("here we are at the exception");
                throw new IndexOutOfRangeException();
            }

            int yaw = (int)YawBetweenZeroAndThreeSixty(observations.Yaw);

            level0.farAhead = observations.level0[indicesGivenYaw[yaw][0]];
            level0.ahead = observations.level0[indicesGivenYaw[yaw][1]];
            level0.farLeft = observations.level0[indicesGivenYaw[yaw][2]];
            level0.left = observations.level0[indicesGivenYaw[yaw][3]];
            level0.right = observations.level0[indicesGivenYaw[yaw][4]];
            level0.farRight = observations.level0[indicesGivenYaw[yaw][5]];
            level0.back = observations.level0[indicesGivenYaw[yaw][6]];
            level0.farBack = observations.level0[indicesGivenYaw[yaw][7]];
            level0.aheadLeft = observations.level0[indicesGivenYaw[yaw][8]];
            level0.aheadRight = observations.level0[indicesGivenYaw[yaw][9]];
            level0.backLeft = observations.level0[indicesGivenYaw[yaw][10]];
            level0.backRight = observations.level0[indicesGivenYaw[yaw][11]];
            level0.farAheadLeft = observations.level0[indicesGivenYaw[yaw][12]];
            level0.farAheadRight= observations.level0[indicesGivenYaw[yaw][13]];
            level0.farBackLeft = observations.level0[indicesGivenYaw[yaw][14]];
            level0.farBackRight = observations.level0[indicesGivenYaw[yaw][15]];

            levelSub1.farAhead = observations.levelSub1[indicesGivenYaw[yaw][0]];
            levelSub1.ahead = observations.levelSub1[indicesGivenYaw[yaw][1]];
            levelSub1.farLeft = observations.levelSub1[indicesGivenYaw[yaw][2]];
            levelSub1.left = observations.levelSub1[indicesGivenYaw[yaw][3]];
            levelSub1.right = observations.levelSub1[indicesGivenYaw[yaw][4]];
            levelSub1.farRight = observations.levelSub1[indicesGivenYaw[yaw][5]];
            levelSub1.back = observations.levelSub1[indicesGivenYaw[yaw][6]];
            levelSub1.farBack = observations.levelSub1[indicesGivenYaw[yaw][7]];
            levelSub1.aheadLeft = observations.levelSub1[indicesGivenYaw[yaw][8]];
            levelSub1.aheadRight = observations.levelSub1[indicesGivenYaw[yaw][9]];
            levelSub1.backLeft = observations.levelSub1[indicesGivenYaw[yaw][10]];
            levelSub1.backRight = observations.levelSub1[indicesGivenYaw[yaw][11]];
            levelSub1.farAheadLeft = observations.levelSub1[indicesGivenYaw[yaw][12]];
            levelSub1.farAheadRight = observations.levelSub1[indicesGivenYaw[yaw][13]];
            levelSub1.farBackLeft = observations.levelSub1[indicesGivenYaw[yaw][14]];
            levelSub1.farBackRight = observations.levelSub1[indicesGivenYaw[yaw][15]];

            levelSub2.farAhead = observations.levelSub2[indicesGivenYaw[yaw][0]];
            levelSub2.ahead = observations.levelSub2[indicesGivenYaw[yaw][1]];
            levelSub2.farLeft = observations.levelSub2[indicesGivenYaw[yaw][2]];
            levelSub2.left = observations.levelSub2[indicesGivenYaw[yaw][3]];
            levelSub2.right = observations.levelSub2[indicesGivenYaw[yaw][4]];
            levelSub2.farRight = observations.levelSub2[indicesGivenYaw[yaw][5]];
            levelSub2.back = observations.levelSub2[indicesGivenYaw[yaw][6]];
            levelSub2.farBack = observations.levelSub2[indicesGivenYaw[yaw][7]];
            levelSub2.aheadLeft = observations.levelSub2[indicesGivenYaw[yaw][8]];
            levelSub2.aheadRight = observations.levelSub2[indicesGivenYaw[yaw][9]];
            levelSub2.backLeft = observations.levelSub2[indicesGivenYaw[yaw][10]];
            levelSub2.backRight = observations.levelSub2[indicesGivenYaw[yaw][11]];
            levelSub2.farAheadLeft = observations.levelSub2[indicesGivenYaw[yaw][12]];
            levelSub2.farAheadRight = observations.levelSub2[indicesGivenYaw[yaw][13]];
            levelSub2.farBackLeft = observations.levelSub2[indicesGivenYaw[yaw][14]];
            levelSub2.farBackRight = observations.levelSub2[indicesGivenYaw[yaw][15]];
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
                // Looking straight
                if (discreteYaw < 45 || discreteYaw >= 315)
                {
                    neighboursIndicesList = new List<int> { 22, 17, 14, 13, 11, 10, 7, 2, 18, 16, 8, 6, 24, 20, 4, 0 };
                }
                // Looking right
                else if (discreteYaw < 135)
                {
                    neighboursIndicesList = new List<int> { 10, 11, 22, 17, 7, 2, 13, 14, 16, 6, 18, 8, 20, 0, 24, 4 };
                }
                // Looking back
                else if (discreteYaw < 225)
                {
                    neighboursIndicesList = new List<int> { 2, 7, 10, 11, 13, 14, 17, 22, 6, 8, 16, 18, 0, 4, 20, 24 };
                }
                // Looking left
                else
                {
                    neighboursIndicesList = new List<int> { 14, 13, 2, 7, 17, 22, 11, 10, 8, 18, 6, 16, 4, 24, 0, 20 };
                }
                indicesGivenYaw.Add(neighboursIndicesList);
            }
        }
    }
}
