using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Research.Malmo;
using Newtonsoft.Json;
using MalmoObservations;

namespace Malmo
{
    public class MinimalMalmo
    {
        //AgentHost agentHost = new AgentHost();
        AgentHost agentHost;
        //MissionSpec mission;
        //static MissionRecordSpec missionRecord;
        //static WorldState worldState;

        // Static constructor. We set isWorldCreated as false.
        public MinimalMalmo()
        {
            // Careful! It may be wrong to use several random number generators
            // accross the project. Perhaps it would be easier to transform
            // NEAT's random generator into a static class (so that we don't need
            // to refer to the object in the genome factory!)
            //rand = new Random();
            System.Diagnostics.Debug.WriteLine("Warning! The world decorator in Malmo is using an" +
                              "independent random generator.");
            //isWorldCreated = false;
            //AgentHost agentHost = new AgentHost(); ERROR
            //MissionSpec mission = new MissionSpec(); FINE
            //MissionRecordSpec missionRecord = new MissionRecordSpec(); FINE
            //WorldState worldState = new WorldState(); FINE
        }

        public void Initialize()
        {
            //MissionSpec mission = new MissionSpec();
            agentHost = new AgentHost();
        }

        public void PingMalmo()
        {
            System.Diagnostics.Debug.WriteLine("MALMO: Hello");
        }
    }
}