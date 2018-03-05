using System;
using SharpNeat.Phenomes;
using MalmoObservations;

namespace Malmo
{
    class MazeControllerNoTurn
    {
        IBlackBox brain;
        ProgramMalmo programMalmo;
        ManyTiles neighbourTiles;
        double xPos;
        double zPos;
        const double xTarget = 1;
        const double zTarget = 12;

        public MazeControllerNoTurn(IBlackBox givenBrain, ProgramMalmo givenMalmo)
        {
            programMalmo = givenMalmo;
            SetDotAsDecimalSeparator();
            brain = givenBrain;
            programMalmo.ObservationsEvent += new EventHandler<ObservationEventArgs>(WhenObservationsEvent);
            programMalmo.ResetNetworkEvent += new EventHandler<ObservationEventArgs>(OnResetEvent);
            neighbourTiles = new ManyTiles();
        }

        public void CleanUp()
        {
            programMalmo.ObservationsEvent -= new EventHandler<ObservationEventArgs>(WhenObservationsEvent);
            programMalmo.ResetNetworkEvent -= new EventHandler<ObservationEventArgs>(OnResetEvent);
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

        void OnResetEvent(object sender, ObservationEventArgs eventArguments)
        {
            brain.ResetState();
        }

        void ObservationsToCommands(JsonObservations observations)
        {
            xPos = observations.XPos;
            zPos = observations.ZPos;
            try
            {
                UpdateTilesInfo(observations);
            }
            catch (IndexOutOfRangeException e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
                return;
            }
            /*
            System.Diagnostics.Debug.WriteLine("Far ahead: " + neighbourTiles.Level0.farAhead +
                ". Ahead: " + neighbourTiles.Level0.ahead +
                ". Far left: " + neighbourTiles.Level0.farLeft +
                ". Left: " + neighbourTiles.Level0.left +
                ". Right: " + neighbourTiles.Level0.right +
                ". Far right: " + neighbourTiles.Level0.farRight +
                ". Back: " + neighbourTiles.Level0.back +
                ". Far back: " + neighbourTiles.Level0.farBack);*/
            if (IsGoalMet())
            {
                return;
            }
            ObservationsToBrainInputs();
            brain.Activate();
            OutputsToCommands();
        }

        bool IsGoalMet()
        {
            if (neighbourTiles.LevelSub1.left == "grass")
            {
                System.Diagnostics.Debug.WriteLine("On the grass!!");
                return true;
            }
            return false;
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
            UpdateRangeDetectors();
            UpdateRadarInputs();
            /*
            System.Diagnostics.Debug.WriteLine("Inputs: " + brain.InputSignalArray[0].ToString() + " " +
                brain.InputSignalArray[1].ToString() + " " +
                brain.InputSignalArray[2].ToString() + " " +
                brain.InputSignalArray[3].ToString());
            */
        }

        void UpdateRangeDetectors()
        {
            brain.InputSignalArray[0] = 0.0;
            brain.InputSignalArray[1] = 0.0;
            brain.InputSignalArray[2] = 0.0;
            brain.InputSignalArray[3] = 0.0;
            // This is clearly not optimal code, but I don't know the recommended way to do this
            if (neighbourTiles.Level0.left != "air" || neighbourTiles.LevelSub1.left == "lava")
            {
                brain.InputSignalArray[0] = 1.0;
            }
            else if (neighbourTiles.Level0.farLeft != "air" || neighbourTiles.LevelSub1.farLeft == "lava")
            {
                brain.InputSignalArray[0] = 0.5;
            }
            if (neighbourTiles.Level0.ahead != "air" || neighbourTiles.LevelSub1.ahead == "lava")
            {
                brain.InputSignalArray[1] = 1.0;
            }
            else if (neighbourTiles.Level0.farAhead != "air" || neighbourTiles.LevelSub1.farAhead == "lava")
            {
                brain.InputSignalArray[1] = 0.5;
            }
            if (neighbourTiles.Level0.right != "air" || neighbourTiles.LevelSub1.right == "lava")
            {
                brain.InputSignalArray[2] = 1.0;
            }
            else if (neighbourTiles.Level0.farRight != "air" || neighbourTiles.LevelSub1.farRight == "lava")
            {
                brain.InputSignalArray[2] = 0.5;
            }
            if (neighbourTiles.Level0.back != "air" || neighbourTiles.LevelSub1.back == "lava")
            {
                brain.InputSignalArray[3] = 1.0;
            }
            else if (neighbourTiles.Level0.farBack != "air" || neighbourTiles.LevelSub1.farBack  == "lava")
            {
                brain.InputSignalArray[3] = 0.5;
            }
        }

        void UpdateRadarInputs()
        {
            double xDist;
            double zDist;
            xDist = xTarget - xPos;
            zDist = zTarget - zPos;
            const double maxXdistPossible = 9;
            const double maxZdistPossible = 12;
            xDist /= maxXdistPossible;
            zDist /= maxZdistPossible;
            brain.InputSignalArray[4] = xDist;
            brain.InputSignalArray[5] = zDist;
            // IDEA: make detectors progressive. For example, the first should be 1 for pi/2
            // and 0 for pi/2 +- pi/8, but changing progressively.
        }

        void OutputsToCommands()
        {
            /*
            System.Diagnostics.Debug.WriteLine("Outputs: " + brain.OutputSignalArray[0].ToString() + " " +
                brain.OutputSignalArray[0].ToString() + " " +
                brain.OutputSignalArray[1].ToString());*/

            double frontSignal = brain.OutputSignalArray[0];
            double lateralSignal = brain.OutputSignalArray[1];
            if (frontSignal > lateralSignal)
            {
                if (frontSignal > 0.5)
                {
                    //System.Diagnostics.Debug.WriteLine("ADVANCE");
                    programMalmo.AddCommandToList("move 1");
                }
                else
                {
                    //System.Diagnostics.Debug.WriteLine("BACKWARDS");
                    programMalmo.AddCommandToList("move -1");
                }
            }
            else
            {
                if (lateralSignal > 0.5)
                {
                    //System.Diagnostics.Debug.WriteLine("RIGHT");
                    programMalmo.AddCommandToList("strafe 1");
                }
                else
                {
                    //System.Diagnostics.Debug.WriteLine("LEFT");
                    programMalmo.AddCommandToList("strafe -1");
                }
            }            
        }
    }
}

