using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpNeat.Core
{
    public class ParallelEvaluationParameters
    {
        public string simulationName;
        public string userName;
        public int assignedPort;

        public ParallelEvaluationParameters(string newSimulationName, string newUserName, int newAssignedPort)
        {
            simulationName = newSimulationName;
            userName = newUserName;
            assignedPort = newAssignedPort;
        }
    }
}
