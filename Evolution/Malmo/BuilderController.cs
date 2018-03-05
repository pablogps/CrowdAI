using System;
using SharpNeat.Phenomes;
using MalmoObservations;

namespace Malmo
{
    /// <summary>
    /// This brain works with 5+1 inputs and 3 outputs.
    /// Input bias is handled separately
    /// Input 0-3: blocks front and front down plus the same back
    /// Input 4: danger front down (next "walk over" tile), only if not covered by other blocks.
    ///          This may represent either lava or a fall (2 or more blocks).
    /// Output 0: lay block (build action)
    /// Output 1-2: Rotate (90 degrees) left-right
    /// </summary>
    public class BuilderController
    {
        IBlackBox brain;
        ProgramMalmo programMalmo;
        FourTiles neighbourTiles;

        public BuilderController(IBlackBox givenBrain, ProgramMalmo givenMalmo)
        {
            programMalmo = givenMalmo;
            SetDotAsDecimalSeparator();
            brain = givenBrain;
            programMalmo.ObservationsEvent += new EventHandler<ObservationEventArgs>(WhenObservationsEvent);
            neighbourTiles = new FourTiles();
        }

        /*
         // This is not working as expected, so we use CleanUp that can be called explicitly,
         // followed by instance = null and then waiting for the garbage collector.
        ~BuilderController()
        {
            programMalmo.ObservationsEvent -= new EventHandler<ObservationEventArgs>(WhenObservationsEvent);
        }
        */
        
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
            try {
                UpdateTilesInfo(observations);
            }
            catch(IndexOutOfRangeException e)
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
            try {
                neighbourTiles.SetTileIndicesGivenObs(observations);
            }
            catch(IndexOutOfRangeException e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
                throw;
            }
        }

        void ObservationsToBrainInputs()
        {
            ProcessForwardTiles();
            ProcessBackwardTiles();
        }

        void ProcessForwardTiles()
        {
            if (neighbourTiles.Level0.ahead != "air")
            {
                // block ahead
                System.Diagnostics.Debug.WriteLine("block ahead");
                brain.InputSignalArray[0] = 1.0;
                brain.InputSignalArray[1] = 1.0;
                brain.InputSignalArray[4] = 0.0;
            }
            else
            {
                ForwardLook1Down();
            }
        }

        /// <summary>
        /// When level0 forward is "air" we need to look one level deeper.
        /// Is this a safe step forward? A step down? Danger ahead?
        /// </summary>
        void ForwardLook1Down()
        {
            if (neighbourTiles.LevelSub1.ahead != "air")
            {
                if (neighbourTiles.LevelSub1.ahead != "lava")
                {
                    // solid ground ahead
                    System.Diagnostics.Debug.WriteLine("ground ahead");
                    brain.InputSignalArray[0] = 0.0;
                    brain.InputSignalArray[1] = 1.0;
                    brain.InputSignalArray[4] = 0.0;                    
                }
                else
                {   // lava on next floor tile!
                    System.Diagnostics.Debug.WriteLine("lava around");
                    brain.InputSignalArray[0] = 0.0;
                    brain.InputSignalArray[1] = 1.0;
                    brain.InputSignalArray[4] = 1.0;
                }
            }
            else
            {   // levels 0 and sub1 ahead = air
                ForwardLook2Down();
            }
        }

        /// <summary>
        /// When level0 and levelSub1 forward are "air" we need to look one level deeper
        /// to see if it is a safe step or a high fall (or lava down ahead)
        /// </summary>
        void ForwardLook2Down()
        {
            if (neighbourTiles.LevelSub2.ahead == "air" || neighbourTiles.LevelSub2.ahead == "lava")
            {
                // high fall or lava down ahead
                System.Diagnostics.Debug.WriteLine("fall or danger ahead");
                brain.InputSignalArray[0] = 0.0;
                brain.InputSignalArray[1] = 0.0;
                brain.InputSignalArray[4] = 1.0;
            }
            else
            {
                // one (safe) step down
                System.Diagnostics.Debug.WriteLine("safe step ahead");
                brain.InputSignalArray[0] = 0.0;
                brain.InputSignalArray[1] = 0.0;
                brain.InputSignalArray[4] = 0.0;
            }
        }

        /// <summary>
        /// Note that currently the information backwards is more incomplete, 
        /// and does not inform about dangers and falls. This may be relevant
        /// as it is possible to trap the player by building a 2-step fall
        /// ahead with a fall back as well!
        /// </summary>
        void ProcessBackwardTiles()
        {
            if (neighbourTiles.Level0.back != "air")
            {
                // block back
                System.Diagnostics.Debug.WriteLine("block back");
                brain.InputSignalArray[2] = 1.0;
                brain.InputSignalArray[3] = 1.0;
            }
            else
            {
                if (neighbourTiles.LevelSub1.back != "air")
                {
                    // some solid block back (maybe lava?)
                    System.Diagnostics.Debug.WriteLine("ground or something back");
                    brain.InputSignalArray[2] = 0.0;
                    brain.InputSignalArray[3] = 1.0;
                }
                else
                {
                    // air behind (one or two-level fall?)
                    System.Diagnostics.Debug.WriteLine("fall behind");
                    brain.InputSignalArray[2] = 0.0;
                    brain.InputSignalArray[3] = 0.0;
                }
            }
        }

        void OutputsToCommands()
        {
            System.Diagnostics.Debug.WriteLine("Outputs: " + brain.OutputSignalArray[0].ToString() + " " +
                brain.OutputSignalArray[1].ToString() + " " + 
                brain.OutputSignalArray[2].ToString());

            /*
            if (IsAheadFree())
            {
                //programMalmo.AddCommandToList("use 1");
            }
            else
            {
                //programMalmo.AddCommandToList("jumpmove 1");
            }
            programMalmo.AddCommandToList("move 1");
            return;
            */

            if (brain.OutputSignalArray[0] > 0.75)
            {
                System.Diagnostics.Debug.WriteLine("output place");
                // action (place block: takes precedence)
                if (IsAheadFree())
                {
                    //programMalmo.AddCommandToList("use 1");
                }
            }
            else
            {
                // process rotation outputs
                if (!ProcessRotation())
                {
                    // if no actions and no rotation, then movement:
                    System.Diagnostics.Debug.WriteLine("MOVE");
                    AddMovement();
                }
            }
        }

        bool IsAheadFree()
        {
            if (neighbourTiles.Level0.ahead == "air")
            {
                //System.Diagnostics.Debug.WriteLine("Ahead is free!");
                return true;
            }
            //System.Diagnostics.Debug.WriteLine("Ahead is NOT free");
            return false;
        }
        
        bool ProcessRotation()
        {
            double leftSignal = brain.OutputSignalArray[1];
            double rightSignal = brain.OutputSignalArray[2];
            double difference = rightSignal - leftSignal;
            const double threshold = 0.1;
            if (leftSignal < 0.75 && rightSignal < 0.75)
            {
                return false;
            }
            else if (difference < -threshold)
            {
                System.Diagnostics.Debug.WriteLine("LEFT");
                programMalmo.AddCommandToList("turn -1");
                return true;
            }
            else if (difference > threshold)
            {
                System.Diagnostics.Debug.WriteLine("RIGHT");
                programMalmo.AddCommandToList("turn 1");
                return true;
            }
            else
            {
                // Neither RIGHT nor LEFT is much more active than
                // the other, so no turning is done (moves instead)
                return false;
            }
        }

        void AddMovement()
        {
            // If ahead is not dangerous, move
            if (brain.InputSignalArray[4] < 0.1)
            {
                // is jump needed? 
                // TODO: We need to check for walls!
                if (brain.InputSignalArray[0] < 0.1)
                {
                    programMalmo.AddCommandToList("move 1");
                }
                else
                {
                    programMalmo.AddCommandToList("jumpmove 1");
                }
            }
        }
    }
}
