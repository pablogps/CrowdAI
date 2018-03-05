using System;
using System.Collections.Generic;
using SharpNeat.Core;
using SharpNeat.EvolutionAlgorithms;
using Malmo;

namespace Evolution.UsersInfo
{
    static public class ActiveUsersList<TGenome>
        where TGenome : class, IGenome<TGenome>
    {
        // userNameToUser cannot be accessed from outside due to problems with dictionaries (?)
        private static Dictionary<string, User> userNameToUser;
        private static Dictionary<string, NeatEvolutionAlgorithm<TGenome>> userNameToAlgorithm;
        // TODO: Ensure this is not larger than the population size!
        // These variables are loaded from the Evolution/Malmo.config.xml file (so there is no need to compile),
        // so these values will be overwritten and are only a refference!
        private static int maxNumberOfUsers = 3;
        private static int portsPerUser = 2;
        private static int populationSize = 4;

        static ActiveUsersList()
        {
            userNameToUser = new Dictionary<string, User>();
            userNameToAlgorithm = new Dictionary<string, NeatEvolutionAlgorithm<TGenome>>();
        }
        
        public static int MaxNumberOfUsers
        {
            get { return maxNumberOfUsers; }
            set
            {
                maxNumberOfUsers = value;
                System.Diagnostics.Debug.WriteLine("Maximum number of simultaneous users being updated to: " + maxNumberOfUsers);
            }
        }

        public static int PortsPerUser
        {
            get { return portsPerUser; }
            set
            {
                portsPerUser = value;
                System.Diagnostics.Debug.WriteLine("Ports per user being updated to: " + portsPerUser);
            }
        }

        public static int PopulationSize
        {
            get { return populationSize; }
            set
            {
                populationSize = value;
                System.Diagnostics.Debug.WriteLine("Population size being updated to: " + populationSize);
            }
        }

        static public int Count()
        {
            return userNameToUser.Count;
        }

        // No longer allowed due to risk of conflict writing to the same file!
        /*
        static public void ListUsers()
        {
            System.Diagnostics.Debug.WriteLine("\nList of current registered users: ");
            foreach (KeyValuePair<string, User> pair in userNameToUser)
            {
                WriteLineForDebug("Registered user: " + pair.Key + " with time: " + pair.Value.TimeSinceLastPing().ToString());
                System.Diagnostics.Debug.WriteLine("Username: " + pair.Key);
            }
        }
        */  

        static public void RemoveInactiveUsers()
        {
            List<string> candidatesToRemove = FindOldUsers();
            foreach (string user in candidatesToRemove)
            {
                RemoveUser(user);
            }
        }

        static private List<string> FindOldUsers()
        {
            int minutessToRemove = 3;
            List<string> oldUsers = new List<string>();
            foreach (KeyValuePair<string, User> pair in userNameToUser)
            {
                if (pair.Value.TimeSinceLastPing() >= minutessToRemove)
                {
                    System.Diagnostics.Debug.WriteLine("Username: " + pair.Key + " was found inactive with a timestamp of "
                                                       + pair.Value.TimeSinceLastPing() + " seconds.");
                    oldUsers.Add(pair.Key);
                }
            }
            return oldUsers;
        }

        static public bool ContainsUser(string userName)
        {
            return userNameToUser.ContainsKey(userName);
        }

        static public bool DoesUserHaveEvolAlg(string userName)
        {
            // Unregistered user? (won't have an algorithm, so returns false)
            if (!userNameToAlgorithm.ContainsKey(userName))
            {
                return false;
            }
            else
            {
                // Pre-registered user?
                // Branching sometimes creates pre-registered users, which have
                // not been initialized yet! These have a null evolution algorithm
                // so we return "false" (and they are normally started)
                if (userNameToAlgorithm[userName] == null)
                {
                    return false;
                }
            }
            return true;
        }

        static public void AddUser(string userName, NeatEvolutionAlgorithm<TGenome> evolutionAlgorithm)
        {
            // New user
            if (!ContainsUser(userName))
            {
                System.Diagnostics.Debug.WriteLine("Registering user: " + userName);
                User newUser = new User();
                userNameToUser.Add(userName, newUser);
                userNameToAlgorithm.Add(userName, evolutionAlgorithm);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Finishing registration of user: " + userName);
                userNameToAlgorithm[userName] = evolutionAlgorithm;
            }
        }
        
        static public void AddParentToUserName(string userName, int? parentID)
        {
            System.Diagnostics.Debug.WriteLine("User: " + userName + " is branching from " + parentID.ToString());            
            if (!userNameToUser.ContainsKey(userName))
            {
                System.Diagnostics.Debug.WriteLine("Trying to add a parent to a user not yet active! " + userName);
                AddUser(userName, null);
                AddParentToUserName(userName, parentID);
            }
            else
            {
                userNameToUser[userName].Parent = parentID;
            }
        }
        
        static public bool IsUsersEvolutionReady(string userName)
        {
            // Users that are not found return "false" (safer return value)
            if (!userNameToUser.ContainsKey(userName))
            {
                return false;
            }
            else
            {
                return userNameToUser[userName].IsEvolutionReady;
            }
        }

        static public bool IsUserWaitingForVideos(string userName)
        {
            // Users that are not found return "true" (safer return value)
            if (!userNameToUser.ContainsKey(userName))
            {
                return true;
            }
            else
            {
                return userNameToUser[userName].IsWaitingForVideos;
            }
        }
        
        static public void SetUserWaitingForVideos(string userName)
        {
            SetUserWaitingForVideos(userName, true);
        }
        static public void SetUserWaitingForVideos(string userName, bool status)
        {
            if (userNameToUser.ContainsKey(userName))
            {
                userNameToUser[userName].IsWaitingForVideos = status;
            }
        }

        static public void SetUserWaitingForAlgorithm(string userName)
        {
            SetUserWaitingForAlgorithm(userName, true);
        }
        static public void SetUserWaitingForAlgorithm(string userName, bool status)
        {
            if (userNameToUser.ContainsKey(userName))
            {
                // If is waiting, is not ready... a bit confusing this.
                userNameToUser[userName].IsEvolutionReady = !status;
            }
        }

        static public void SetRunningStatus(string userName, bool isRunning)
        {
            if (!userNameToUser.ContainsKey(userName))
            {
                System.Diagnostics.Debug.WriteLine("Trying to change running status of user not yet initiated " + userName);
                AddUser(userName, null);
            }
            userNameToUser[userName].IsEvolutionRunning = isRunning;
        }

        static void RemoveUser(string userName)
        {
            System.Diagnostics.Debug.WriteLine("Removing user: " + userName);
            SharpNeat.PopulationReadWrite.WriteLineForDebug("Removing user from active list. ", userName);
            ProgramMalmo.FreePort(userName);
            userNameToAlgorithm[userName].StopEvolution();
            EvolutionCoordination.DessubscribeEventsFromUser(userName);
            userNameToUser.Remove(userName);
            userNameToAlgorithm.Remove(userName);
        }

        static public NeatEvolutionAlgorithm<TGenome> EvolutionAlgorithmForUser(string userName)
        {
            NeatEvolutionAlgorithm<TGenome> evolAlg = null;
            if (userNameToUser.ContainsKey(userName))
            {
                evolAlg = userNameToAlgorithm[userName];
            }
            return evolAlg;
        }

        static public int? UserParent(string userName)
        {
            int? parentID = null;
            if (userNameToUser.ContainsKey(userName))
            {
                parentID = userNameToUser[userName].Parent;
            }
            return parentID;
        }

        static public bool IsUserRunning(string userName)
        {
            bool isRunning = false;
            if (userNameToUser.ContainsKey(userName))
            {
                isRunning = userNameToUser[userName].IsEvolutionRunning;
            }
            return isRunning;
        }

        static public void ResetTimer(string userName)
        {
            if (userNameToUser.ContainsKey(userName))
            {
                userNameToUser[userName].ResetTimer();
            }
        }
    }
}
