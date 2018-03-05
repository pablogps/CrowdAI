// --------------------------------------------------------------------------------------------------
//  Copyright (c) 2016 Microsoft Corporation
//  
//  Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
//  associated documentation files (the "Software"), to deal in the Software without restriction,
//  including without limitation the rights to use, copy, modify, merge, publish, distribute,
//  sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
//  
//  The above copyright notice and this permission notice shall be included in all copies or
//  substantial portions of the Software.
//  
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
//  NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
//  DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// --------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Research.Malmo;
using Newtonsoft.Json;
using MalmoObservations;
using System.Runtime.ExceptionServices;
using System.Security;
using System.IO;
// To use ActiveUsersList:
using Evolution.UsersInfo;
using SharpNeat.Genomes.Neat;

namespace Malmo
{
    public class ProgramMalmo
    {
        static Random rand;
        static List<int> usedPorts;
        static Dictionary<string, int> userToPort;

        bool isWorldCreated = false;

        AgentHost agentHost;
        MissionSpec mission;
        MissionRecordSpec missionRecord;
        WorldState worldState;
        ClientPool clientPool;
        int myPort;
        JsonObservations observations = new JsonObservations();
        string fileName;
        string userName;
        List<string> listOfCommmands;

        public event EventHandler<ObservationEventArgs> ObservationsEvent;
        public event EventHandler<ObservationEventArgs> ResetNetworkEvent;
        public event EventHandler<ObservationEventArgs> MissionEndEvent;

        // Static constructor. We set up the random number generator.
        static ProgramMalmo()
        {
            System.Diagnostics.Debug.WriteLine("This is the static constructor");
            // Careful! It may be wrong to use several random number generators
            // accross the project. Perhaps it would be easier to transform
            // NEAT's random generator into a static class (so that we don't need
            // to refer to the object in the genome factory!)
            rand = new Random();
            usedPorts = new List<int>();
            userToPort = new Dictionary<string, int>();
            System.Diagnostics.Debug.WriteLine("Warning! The world decorator in Malmo is using an" +
                              "independent random generator.");
        }

        public ProgramMalmo()
        {
            isWorldCreated = false;
        }
                
        static public void FreePort(string user)
        {
            SharpNeat.PopulationReadWrite.WriteLineForDebug("Removing user from port" + userToPort[user].ToString(), user);
            userToPort.Remove(user);
        }
        
        // We cannot modify a static dictionary in parallel evaluation!
        // We call it before, from ParallelGenomeListEvaluator
        static public void UpdateUserToPortDictionary(string aUser)
        {
            if (userToPort.ContainsKey(aUser))
            {
                System.Diagnostics.Debug.WriteLine("User " + aUser + " already in user to port");
                return;
            }
            // TODO: This is not clever! (What happens if there are no available ports?)
            // The user should not be allowed as active in EventsController.IsFreeEvolSlot...
            for (int i = 10000; i < 10020; i += ActiveUsersList<NeatGenome>.PortsPerUser)
            {
                if (!userToPort.ContainsValue(i))
                {
                    SharpNeat.PopulationReadWrite.WriteLineForDebug("Assigned port " + i.ToString(), aUser);
                    userToPort.Add(aUser, i);
                    return;
                }
            }
        }

        void AssingPort(int offset)
        {
            // We cannot modify a static dictionary in parallel evaluation!
            // We call it before, from ParallelGenomeListEvaluator
            //UpdateUserToPortDictionary();
            myPort = userToPort[userName];
            myPort = myPort + offset;
            //WriteToLog("Adding client info, port: " + myPort + " requested by user " + userName);
            clientPool = null;
            clientPool = new ClientPool();
            clientPool.add(new ClientInfo("127.0.0.1", myPort));
            System.Diagnostics.Debug.WriteLine("Creating a clientPool with number: " + myPort);
            //clientPool.add(new ClientInfo("127.0.0.1", 10000));
            // We always add 10005 as an extra as the last resource!
            clientPool.add(new ClientInfo("127.0.0.1", 10005));
        }
        
        public static void PingMalmo()
        {
            System.Diagnostics.Debug.WriteLine("MALMO: Hello");
        }

        /// <summary>
        /// Runs a Minecraft simulation. The parameters of the simulation are in
        /// the minecraftWorldXML file.
        /// Using newFileName allows to save files in an specific path (otherwise
        /// the save path will not change and new calls will overwrite data!)
        /// </summary>
        /// <param name="newFileName">New file name.</param>
        public void RunMalmo(SharpNeat.Core.ParallelEvaluationParameters parameters)
        {
            //System.Diagnostics.Debug.WriteLine("Enter Malmo with offset: " + parameters.assignedPort);
            fileName = parameters.simulationName;
            userName = parameters.userName;
            AssingPort(parameters.assignedPort);
            RunMalmo();
        }
        void RunMalmo()
        {
            // Normally we do not need this line, but we will Dispose() agentHost at the end
            // of the method because of a bug in Malmo.
            agentHost = new AgentHost();
            CreateResetNetworkEvent();
            // Deactivated for published version
            //ReadCommandLineArgs();
            InitializeMission();
            System.Diagnostics.Debug.WriteLine("Mission initialized");
            if (!TryStartMission())
            {
                CreateMissionEndEvent();
                return;
            }
            ConsoleOutputWhileMissionLoads();
            MainLoop();
            // We need to destroy the agentHost so that the data is saved. This is a bug in Malmo.
            agentHost.Dispose();
            System.Diagnostics.Debug.WriteLine("Mission has stopped.");
            // Extracts the results and moves the video to the candidates folder:
            SharpNeat.PopulationReadWrite.CreateCandidateVideoFromResults(fileName, userName);
            CreateMissionEndEvent();
        }

        public void AddCommandToList(string newCommand)
        {
            listOfCommmands.Add(newCommand);
        }

        //----------------------------------------------------------------------------------------------------------------------
        void ReadCommandLineArgs()
        {
            try
            {
                agentHost.parse(new StringVector(Environment.GetCommandLineArgs()));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: {0}", ex.Message);
                Console.Error.WriteLine(agentHost.getUsage());
                Environment.Exit(1);
            }
            if (agentHost.receivedArgument("help"))
            {
                Console.Error.WriteLine(agentHost.getUsage());
                Environment.Exit(0);
            }
        }

        //----------------------------------------------------------------------------------------------------------------------
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        void InitializeMission()
        {
            string missionXMLpath = SharpNeat.PopulationReadWrite.GetEvolutionFolderPath();
            missionXMLpath += "minecraftWorldXML.txt";
            // We need to avoid trouble if the file gets simultaneus access demands
            string rawMissionXML = SaferRead(missionXMLpath);
            // This is a valid alternative, but changes in the mission xml will require compiling...
            //string rawMissionXML = Evolution.Malmo.RawXMLmissionFactory.xmlMission;
            try
            {
                // Hopefully this minimizes parallelization errors:
                // 'System.AccessViolationException' in MalmoNET.dll
                // Still, if it happens, this will require a server reset...
                RandomWait();
                mission = new MissionSpec(rawMissionXML, true);
            }
            catch (Exception ex)
            {
                string errorLine = "Fatal error when starting a mission in ProgramMalmo: " + ex.Message;
                SharpNeat.PopulationReadWrite.WriteErrorForDebug(errorLine);
                System.Diagnostics.Debug.WriteLine("\nFatal error when starting a mission in ProgramMalmo: " + ex.Message);
                Environment.Exit(1);
            }
            mission.timeLimitInSeconds(10);
            //mission.forceWorldReset();
            AddProceduralDecoration();
            string savePath = MakeSavePath();
            missionRecord = new MissionRecordSpec(savePath);
            //missionRecord = new MissionRecordSpec();
            missionRecord.recordCommands();
            missionRecord.recordMP4(30, 400000);
            missionRecord.recordRewards();
            missionRecord.recordObservations();
        }
        string SaferRead(string path)
        {
            string newFile = null;
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    newFile = reader.ReadToEnd();
                }
            }
            return newFile;
        }
        void RandomWait()
        {
            int millisecondsToSleep = (int)(100.0 * rand.NextDouble());
            Thread.Sleep(millisecondsToSleep);
        }

        void AddProceduralDecoration()
        {
            // We check this so that we don't repeat this step at every simulation.
            // If we repeat this (which is good if we want variation!) then make
            // sure to use forceWorldReset (otherwise modifications will be
            // cumulative!)
            if (!isWorldCreated)
            {
                mission.forceWorldReset();
                MazeMaker mazeMaker = new MazeMaker(mission);
                mazeMaker.CreateMaze();
                isWorldCreated = true;
            }
        }

        string MakeSavePath()
        {
            string saveDataPath = SharpNeat.PopulationReadWrite.GetEvolutionFolderPath();
            saveDataPath += "\\ExperimentsData\\" + userName + "\\";
            if (fileName != null)
            {
                return saveDataPath += fileName + ".tgz";
            }
            else
            {
                string missionXMLpath = SharpNeat.PopulationReadWrite.GetEvolutionFolderPath();
                return saveDataPath += "_SavedData.tgz";
            }
        }

        //----------------------------------------------------------------------------------------------------------------------
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        bool TryStartMission()
        {
            bool returnValue = true;
            try
            {
                // Default for only one Malmo instance and agent...
                //agentHost.startMission(mission, missionRecord);
                // For info on startMission https://goo.gl/JfvHHb
                // Basically, clientPool identifies the Minecraft instance, the "0" is the number
                // of agents in the mission (we use multiple missions with 1 agent each, 1 agent 
                // corresponds to 0). The string is a mission identifier.
                agentHost.startMission(mission, clientPool, missionRecord, 0, userName);
            }
            catch (Exception ex)
            {
                string errorLine = "Fatal error when starting a mission in ProgramMalmo: " + ex.Message;
                SharpNeat.PopulationReadWrite.WriteErrorForDebug(errorLine);
                Console.Error.WriteLine("Error starting mission: {0}", ex.Message);
                // I tried ignoring this critical error, but it is not possible to do so.
                Environment.Exit(1);
                //returnValue = false;
            }
            return returnValue;
        }

        //----------------------------------------------------------------------------------------------------------------------
        void ConsoleOutputWhileMissionLoads()
        {
            UpdateWorldState();
            while (!worldState.has_mission_begun)
            {
                System.Diagnostics.Debug.Write(".");
                Thread.Sleep(100);
                UpdateWorldState();
            }
            Console.WriteLine();
        }

        //----------------------------------------------------------------------------------------------------------------------
        void UpdateWorldState()
        {
            worldState = agentHost.getWorldState();
            foreach (TimestampedString error in worldState.errors)
            {
                Console.Error.WriteLine("Error: {0}", error.text);
            }
        }

        //----------------------------------------------------------------------------------------------------------------------
        void MainLoop()
        {
            ResetCommands();
            Thread.Sleep(100);
            while (worldState.is_mission_running)
            {
                worldState = agentHost.getWorldState();
                UpdateObservations();
                CreateObservationsEvent();
                ExecuteCommands();
                ResetCommands();
                Thread.Sleep(60);
                WriteFeedback();
            }
        }

        //----------------------------------------------------------------------------------------------------------------------
        void UpdateObservations()
        {
            if (worldState.number_of_observations_since_last_state > 0)
            {
                ParseObervations();
            }
        }

        //----------------------------------------------------------------------------------------------------------------------
        void CreateObservationsEvent()
        {
            // Always check that there are listeners for the event
            if (null != ObservationsEvent)
            {
                // Creates here the arguments for the event
                var myArguments = new ObservationEventArgs();
                myArguments.observations = observations;
                // Catches exceptions thrown by event listeners. This prevents 
                // listener exceptions from terminating the algorithm thread.
                try
                {
                    ObservationsEvent(null, myArguments);
                }
                catch (Exception ex)
                {
                    //__log.Error("ObservationsEvent listener threw exception", ex);
                    System.Diagnostics.Debug.WriteLine("ObservationsEvent listener threw exception" + ex);
                }
            }
        }

        //----------------------------------------------------------------------------------------------------------------------
        void CreateResetNetworkEvent()
        {
            // Always check that there are listeners for the event
            if (null != ResetNetworkEvent)
            {
                // Creates here the arguments for the event
                var myArguments = new ObservationEventArgs();
                // Catches exceptions thrown by event listeners. This prevents 
                // listener exceptions from terminating the algorithm thread.
                try
                {
                    ResetNetworkEvent(null, myArguments);
                }
                catch (Exception ex)
                {
                    //__log.Error("ResetNetworkEvent listener threw exception", ex);
                    System.Diagnostics.Debug.WriteLine("ResetNetworkEvent listener threw exception" + ex);
                }
            }
        }

        //----------------------------------------------------------------------------------------------------------------------
        void CreateMissionEndEvent()
        {
            // Always check that there are listeners for the event
            if (null != MissionEndEvent)
            {
                // Creates here the arguments for the event
                var myArguments = new ObservationEventArgs();
                myArguments.observations = observations;

                // Catch exceptions thrown by event listeners. This prevents 
                // listener exceptions from terminating the algorithm thread.
                try
                {
                    MissionEndEvent(null, myArguments);
                }
                catch (Exception ex)
                {
                    //__log.Error("MissionEndEvent listener threw exception", ex);
                    System.Diagnostics.Debug.WriteLine("MissionEndEvent listener threw exception" + ex);
                }
            }
        }

        //----------------------------------------------------------------------------------------------------------------------
        void ResetCommands()
        {
            listOfCommmands = new List<string>();
        }

        //----------------------------------------------------------------------------------------------------------------------
        void ExecuteCommands()
        {
            try
            {
                foreach (string command in listOfCommmands)
                {
                    //System.Diagnostics.Debug.WriteLine("Executing command " + command);
                    agentHost.sendCommand(command);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error executing commands at ProgramMalmo: " + ex.Message);
                string errorLine = "Error executing commands at ProgramMalmo: " + ex.Message;
                SharpNeat.PopulationReadWrite.WriteErrorForDebug(errorLine);
            }
        }

        //----------------------------------------------------------------------------------------------------------------------
        void ParseObervations()
        {
            int numberObservations = worldState.observations.Count;
            string msg = worldState.observations[numberObservations - 1].text;
            observations = JsonConvert.DeserializeObject<JsonObservations>(msg);
        }

        //----------------------------------------------------------------------------------------------------------------------
        void WriteFeedback()
        {
            /*Console.WriteLine("video, observations, rewards received: {0}, {1}, {2}",
							  worldState.number_of_video_frames_since_last_state,
							  worldState.number_of_observations_since_last_state,
							  worldState.number_of_rewards_since_last_state);*/
            //Console.WriteLine(".");
            WriteRewards();
            WriteErrors();
        }

        void WriteRewards()
        {
            foreach (TimestampedReward reward in worldState.rewards)
            {
                Console.Error.WriteLine("Summed reward: {0}", reward.getValue());
            }
        }

        void WriteErrors()
        {
            foreach (TimestampedString error in worldState.errors)
            {
                Console.Error.WriteLine("Error: {0}", error.text);
            }
        }
    }
}