using System;
using SharpNeat.Phenomes;
using MalmoObservations;

namespace Malmo
{
    class MazeController
    {
        IBlackBox brain;
        ProgramMalmo programMalmo;
        FourTiles neighbourTiles;

        public MazeController(IBlackBox givenBrain, ProgramMalmo givenMalmo)
        {
            programMalmo = givenMalmo;
            SetDotAsDecimalSeparator();
            brain = givenBrain;
            programMalmo.ObservationsEvent += new EventHandler<ObservationEventArgs>(WhenObservationsEvent);
            neighbourTiles = new FourTiles();
        }

        public void CleanUp()
        {
            programMalmo.ObservationsEvent -= new EventHandler<ObservationEventArgs>(WhenObservationsEvent);
        }

        /// <summary>
        /// This class will take numbers and pass them as commands to ProgramMalmo.
        /// But Malmo works with dot as separator (0.9) and we must make sure
        /// numbers are not printed using comas (0,9) which would not work.
        /// </summary>
        void SetDotAsDecimalSeparator()
        {
            System.Globalization.CultureInfo customCulture =
                    (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;
        }

        /// <summary>
        /// Program Malmo will execute Minecraft. At some points it will create
        /// an update event, so which this controller is subscribed. This event
        /// passes the observations as parameters (observations are the state
        /// of the Minecraft simulation, such as position or the type of blocks
        /// in the surroundings).
        /// This class, the brain, will take these observations and translate
        /// them into commands for the Minecraft character (using a neural network).
        /// This will be done every time the event is called.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="eventArguments">Event arguments.</param>
        void WhenObservationsEvent(object sender, ObservationEventArgs eventArguments)
        {
            ObservationsToCommands(eventArguments.observations);
        }

        void ObservationsToCommands(JsonObservations observations)
        {
            try
            {
                UpdateTilesInfo(observations);
            }
            catch (IndexOutOfRangeException e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
                return;
            }
            ObservationsToBrainInputs();
            brain.Activate();
            OutputsToCommands();
        }

        void UpdateTilesInfo(JsonObservations observations)
        {
            try
            {
                neighbourTiles.SetTileIndicesGivenObs(observations);
            }
            catch (IndexOutOfRangeException e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
                throw;
            }
        }

        void ObservationsToBrainInputs()
        {
            brain.InputSignalArray[0] = 0.0;
            brain.InputSignalArray[1] = 0.0;
            brain.InputSignalArray[2] = 0.0;
            brain.InputSignalArray[3] = 0.0;
            brain.InputSignalArray[4] = 0.0;
            if (neighbourTiles.Level0.frontLeft != "air" || neighbourTiles.LevelSub1.frontLeft == "lava")
            {
                brain.InputSignalArray[0] = 1.0;
            }
            if (neighbourTiles.Level0.ahead != "air" || neighbourTiles.LevelSub1.ahead == "lava")
            {
                brain.InputSignalArray[1] = 1.0;
            }
            if (neighbourTiles.Level0.frontRight != "air" || neighbourTiles.LevelSub1.frontRight == "lava")
            {
                brain.InputSignalArray[2] = 1.0;
            }
            if (neighbourTiles.Level0.left != "air" || neighbourTiles.LevelSub1.left == "lava")
            {
                brain.InputSignalArray[3] = 1.0;
            }
            if (neighbourTiles.Level0.right != "air" || neighbourTiles.LevelSub1.right == "lava")
            {
                brain.InputSignalArray[4] = 1.0;
            }
            System.Diagnostics.Debug.WriteLine("Inputs: " + brain.InputSignalArray[0].ToString() + " " +
                brain.InputSignalArray[1].ToString() + " " +
                brain.InputSignalArray[2].ToString() + " " +
                brain.InputSignalArray[3].ToString() + " " +
                brain.InputSignalArray[4].ToString());
        }

        void OutputsToCommands()
        {
            System.Diagnostics.Debug.WriteLine("Outputs: " + brain.OutputSignalArray[0].ToString() + " " +
                brain.OutputSignalArray[0].ToString() + " " +
                brain.OutputSignalArray[1].ToString());

            double leftSignal = brain.OutputSignalArray[0];
            double rightSignal = brain.OutputSignalArray[1];
            double difference = rightSignal - leftSignal;
            const double threshold = 0.1;
            if (leftSignal < 0.75 && rightSignal < 0.75)
            {
                System.Diagnostics.Debug.WriteLine("MOVE");
                programMalmo.AddCommandToList("move 1");
            }
            else if (difference < -threshold)
            {
                System.Diagnostics.Debug.WriteLine("LEFT");
                programMalmo.AddCommandToList("turn -1");
            }
            else if (difference > threshold)
            {
                System.Diagnostics.Debug.WriteLine("RIGHT");
                programMalmo.AddCommandToList("turn 1");
            }
            else
            {
                // Neither RIGHT nor LEFT is much more active than
                // the other, so no turning is done (moves instead)
                System.Diagnostics.Debug.WriteLine("MOVE");
                programMalmo.AddCommandToList("move 1");
            }
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
