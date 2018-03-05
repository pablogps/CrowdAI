using System;
using System.Collections.Generic;
using Evolution;
using Evolution.UsersInfo;
using SharpNeat.Genomes.Neat;

namespace INM.Controllers.Events
{
    public static class EventsController
    {
        public static event EventHandler<EvolutionEventsArgs> StartEvolutionEvent;
        public static event EventHandler<EvolutionEventsArgs> ResetEvolutionEvent;
        public static event EventHandler<EvolutionEventsArgs> StopEvolutionEvent;
        public static event EventHandler<EvolutionEventsArgs> BranchEvent;
        public static event EventHandler<EvolutionEventsArgs> NextGenerationEvent;
        public static event EventHandler<EvolutionEventsArgs> SaveCandidate;
        
        private static List<string> startedUsers;

        static EventsController()
        {
            StartEvolutionEvent += new EventHandler<EvolutionEventsArgs>(EvolutionCoordination.OnStartEvolution);
            startedUsers = new List<string>();
        }

        public static bool RaiseStartEvolutionEvent(string userName)
        {
            if (!IsFreeEvolSlot(userName))
            {
                return false;
            }
            LaunchBasicEvent(StartEvolutionEvent, userName);
            return true;
        }

        public static bool IsFreeEvolSlot(string userName)
        {
            // If the user is already active, it will use its own allocated spot
            if (ActiveUsersList<NeatGenome>.ContainsUser(userName))
            {
                return true;
            }
            // If not already active, it will need a new spot:
            ActiveUsersList<NeatGenome>.RemoveInactiveUsers();
            // This wait is probably innecessary, but there have been errors where new users try to
            // take the place of others, causing trouble.
            System.Threading.Thread.Sleep(100);
            if (ActiveUsersList<NeatGenome>.Count() >= ActiveUsersList<NeatGenome>.MaxNumberOfUsers)
            {
                WriteLineForDebug("Requested a slot but the server was full.", userName);
                return false;
            }
            return true;
        }

        public static void RaiseBranchEvent(string userName)
        {
            CheckAndRegisterEvolAlg(userName);
            // Branching works differntly for new users (need to setup evolution)
            // and old users (need to restart evolution)
            if (!startedUsers.Contains(userName))
            {
                RaiseStartEvolutionEvent(userName);
            }
            else
            {
                LookForEvolutionAlgorithm(userName);
                LaunchBasicEvent(BranchEvent, userName);
            }
        }

        private static void CheckAndRegisterEvolAlg(string userName)
        {
            var evolutionAlgorithm = ActiveUsersList<NeatGenome>.EvolutionAlgorithmForUser(userName);
            if (evolutionAlgorithm != null)
            {
                LookForEvolutionAlgorithm(userName);
            }
        }
        
        public static void RaiseResetEvolutionEvent(string userName)
        {
            LookForEvolutionAlgorithm(userName);
            LaunchBasicEvent(ResetEvolutionEvent, userName);
        }

        public static void RaiseStopEvolutionEvent(string userName)
        {
            LookForEvolutionAlgorithm(userName);
            LaunchBasicEvent(StopEvolutionEvent, userName);
        }

        private static void LaunchBasicEvent(EventHandler<EvolutionEventsArgs> currentEvent, string userName)
        {
            // Always check that there are listeners for the event
            if (null != currentEvent)
            {
                // Catches exceptions thrown by event listeners. This prevents 
                // listener exceptions from terminating the algorithm thread.
                try
                {
                    EvolutionEventsArgs myArguments = new EvolutionEventsArgs();
                    myArguments.userName = userName;
                    currentEvent(null, myArguments);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("EventsController: Event threw exception" + ex);
                }
            }
        }

        public static void RaiseNextGenerationEvent(int candidateIndex, string userName, bool isNormalMutations)
        {
            LookForEvolutionAlgorithm(userName);
            // Always check that there are listeners for the event
            if (null != NextGenerationEvent)
            {
                // Creates here the arguments for the event
                EvolutionEventsArgs myArguments = new EvolutionEventsArgs();
                myArguments.userName = userName;
                myArguments.candidateIndex = candidateIndex;
                myArguments.isNormalMutations = isNormalMutations;
                // Catches exceptions thrown by event listeners. This prevents 
                // listener exceptions from terminating the algorithm thread.
                try
                {
                    NextGenerationEvent(null, myArguments);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("NextGenerationEvent listener threw exception" + ex);
                }
            }
        }

        public static void RaiseSaveCandidate(int candidateIndex, string folderName, string userName)
        {
            LookForEvolutionAlgorithm(userName);
            // Always check that there are listeners for the event
            if (null != SaveCandidate)
            {
                // Creates here the arguments for the event
                EvolutionEventsArgs myArguments = new EvolutionEventsArgs();
                myArguments.userName = userName;
                myArguments.candidateIndex = candidateIndex;
                myArguments.folderName = folderName;
                // Catches exceptions thrown by event listeners. This prevents 
                // listener exceptions from terminating the algorithm thread.
                try
                {
                    SaveCandidate(null, myArguments);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("SaveCandidateEvent listener threw exception" + ex);
                }
            }
        }

        /// <summary>
        /// Evolution algorithm is not a static class. This means we cannot subscribe it to the
        /// event handler at the constructor. We cannot do it either right after raising the "start evolution"
        /// event, since it takes a while to take effect. This method is used before trying to raise
        /// the "next generation" event. If the evolution algorithm is foundm, it is subscribed.
        /// </summary>
        private static void LookForEvolutionAlgorithm(string userName)
        {
            if (!startedUsers.Contains(userName))
            {
                //ActiveUsersList<NeatGenome>.ListUsers();
                if (ActiveUsersList<NeatGenome>.ContainsUser(userName))
                {
                    var evolutionAlgorithm = ActiveUsersList<NeatGenome>.EvolutionAlgorithmForUser(userName);
                    startedUsers.Add(userName);
                    NextGenerationEvent += new EventHandler<EvolutionEventsArgs>(evolutionAlgorithm.ProceedNextGeneration);
                    SaveCandidate += new EventHandler<EvolutionEventsArgs>(evolutionAlgorithm.SaveGenomeAndExit);
                    ResetEvolutionEvent += new EventHandler<EvolutionEventsArgs>(EvolutionCoordination.OnResetEvolution);
                    StopEvolutionEvent += new EventHandler<EvolutionEventsArgs>(EvolutionCoordination.OnStopEvolution);
                    BranchEvent += new EventHandler<EvolutionEventsArgs>(EvolutionCoordination.OnBranch);
                }
            }
        }
          
        static public void WriteLineForDebug(string line, string user)
        {
            try
            {
                line += " " + DateTime.Now.ToString();
                string path = ReturnLogPath(user);
                string[] lines = { line };
                System.IO.File.AppendAllLines(path, lines);
                System.Diagnostics.Debug.WriteLine(line);
            }
            catch (Exception e)
            {
                string[] errorLine = { "Fatal error when writing to log from EventsController: " + e.Message + " " + DateTime.Now.ToString() };
                System.IO.File.AppendAllLines(@"C:\inetpub\wwwroot\Output_FatalErrors.txt", errorLine);
                return;
            }
        }

        static string ReturnLogPath(string user)
        {
            string path = "";
            if (user != null)
            {
                string folderPath = @"C:\inetpub\wwwroot\Logs\" + user;
                CreateFolderIfNotExists(folderPath);
                path = folderPath + "\\Output_Malmo.txt";
            }
            else
            {
                path = @"C:\inetpub\wwwroot\Output_Malmo.txt";
            }
            return path;
        }
        
        static void CreateFolderIfNotExists(string folderPath)
        {
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }
        }

        /*
        // This is very problematic: users may have multiple IPs, share one with others...
        // Improvement: use cookies
        static public string IPstring()
        {
            try
            {
                string userHostAddress = HttpContext.Current.Request.UserHostAddress;
                userHostAddress = new string((from c in userHostAddress where char.IsLetterOrDigit(c) select c).ToArray());
                return userHostAddress;
            }
            catch (Exception)
            {
                return "9999";
            }
        }
        */
    }
}