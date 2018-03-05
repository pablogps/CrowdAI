using System.Collections.Generic;
using Microsoft.Research.Malmo;

namespace Malmo
{
    class MazeMaker
    {
        MissionSpec mission;

        // This should only live in one script (original is in ProgramMalmo)
        struct Block
        {
            public int xCoord;
            public int yCoord;
            public int zCoord;
            public string type;
        }

        // Agent starts in x="0.5" y="46.0" z="0.5"
        // These coordinates are only for the contents of the maze, the walls are done easily in loops
        List<int> cobbleStoneX = new List<int>()
        { 2, 2, 2, 7, 3, 5, 0, 1, 6, 6, 7 };
        List<int> cobbleStoneY = new List<int>()
        { 0, 1, 2, 3, 5, 5, 7, 7, 7, 10, 10 };
        List<int> lavaX = new List<int>()
        { 4, 4, 2, 3, 3, 3, 8 };
        List<int> lavaY = new List<int>()
        { 4, 5, 6, 8, 9, 10, 10 };
        List<int> lapisX = new List<int>()
        { 0, 1, 0, 1 };
        List<int> lapisY = new List<int>()
        { 0, 0, 1, 1 };
        List<int> grassX = new List<int>()
        { 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2 };
        List<int> grassY = new List<int>()
        { 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 11 };

        public MazeMaker(MissionSpec givenMission)
        {
            mission = givenMission;
        }
        private MazeMaker()
        {
            mission = null;
        }

        public void CreateMaze()
        {
            FillWithClay();
            BuildWalls();
            AddListOfBlocks(cobbleStoneX, cobbleStoneY, 46, "cobblestone");
            AddListOfBlocks(cobbleStoneX, cobbleStoneY, 47, "cobblestone");
            AddListOfBlocks(lavaX, lavaY, 45, "lava");
            AddListOfBlocks(lapisX, lapisY, 45, "lapis_block");
            AddListOfBlocks(grassX, grassY, 45, "grass");
        }

        void FillWithClay()
        {
            Block block = new Block();
            block.yCoord = 45;
            block.type = "clay";
            for (int i = -8; i < 1; ++i)
            {
                block.xCoord = i;
                for (int j = 0; j < 12; ++j)
                {
                    block.zCoord = j;
                    AddSpecialBlock(block);
                }
            }
        }

        void BuildWalls()
        {
            Block block = new Block();
            block.yCoord = 46;
            block.type = "cobblestone";

            for (int i = -9; i < 2; ++i)
            {
                block.xCoord = i;
                block.zCoord = -1;
                AddSpecialBlock(block);
                block.yCoord = 47;
                AddSpecialBlock(block);
                block.zCoord = 12;
                AddSpecialBlock(block);
                block.yCoord = 46;
                AddSpecialBlock(block);
            }
            for (int j = 0; j < 12; ++j)
            {
                block.zCoord = j;
                block.xCoord = 1;
                AddSpecialBlock(block);
                block.yCoord = 47;
                AddSpecialBlock(block);
                block.xCoord = -9;
                AddSpecialBlock(block);
                block.yCoord = 46;
                AddSpecialBlock(block);
            }
        }

        void AddListOfBlocks(List<int> coordX, List<int> coordY, int height, string type)
        {
            Block block = new Block();
            block.yCoord = height;
            block.type = type;
            if (coordX.Count == coordY.Count)
            {
                for (int i = 0; i < coordX.Count; ++i)
                {
                    block.xCoord = -coordX[i];
                    block.zCoord = coordY[i];
                    AddSpecialBlock(block);
                }
            }
        }

        // This should only live in one script (original is in ProgramMalmo, changes made)
        void AddSpecialBlock(Block block)
        {
            mission.drawBlock(block.xCoord, block.yCoord, block.zCoord, block.type);
        }

        /*
        // OLD CODE: Creates a lava ring and two lines of colour for orientation.
        void AddLavaRing()
        {
            Block block = new Block();
            block.yCoord = 45;
            block.type = "lava";
            for (int i = -10; i < 11; ++i)
            {
                block.xCoord = i;
                for (int j = -10; j < 11; ++j)
                {
                    block.zCoord = j;
                    // This will create a crown of lava blocks
                    if (i * i + j * j >= 64 && i * i + j * j < 100)
                    {
                        TryAddSpecialBlock(block, 1.0);
                    }
                }
            }
        }
        void AddLines()
        {
            Block goldBlock = new Block();
            goldBlock.yCoord = 45;
            goldBlock.type = "gold_block";
            Block lapisBlock = new Block();
            lapisBlock.yCoord = 45;
            lapisBlock.type = "lapis_block";
            goldBlock.zCoord = 1;
            lapisBlock.zCoord = -1;
            for (int i = -7; i < 8; ++i)
            {
                goldBlock.xCoord = i;
                lapisBlock.xCoord = i;
                TryAddSpecialBlock(goldBlock, 1.0);
                TryAddSpecialBlock(lapisBlock, 1.0);
            }
        }
        void TryAddSpecialBlock(Block block, double chanceThreshold)
        {
            if (rand.NextDouble() < chanceThreshold)
            {
                mission.drawBlock(block.xCoord, block.yCoord, block.zCoord, block.type);
            }
        }
        */
    }
}