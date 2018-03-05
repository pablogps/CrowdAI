using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Evolution.Malmo
{
    class RawXMLmissionFactory
    {
        public static string xmlMission = @"
<?xml version=""1.0"" encoding=""UTF-8"" standalone=""no"" ?>
<Mission xmlns = ""http://ProjectMalmo.microsoft.com"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">

  <About>
    <Summary>Nothing.</Summary>
  </About>

  <ModSettings>
    <MsPerTick>20</MsPerTick>
  </ModSettings>

  <ServerSection>
    <ServerInitialConditions>
      <Time>
        <StartTime>1000</StartTime>
        <AllowPassageOfTime>false</AllowPassageOfTime>
      </Time>
    </ServerInitialConditions>
    <ServerHandlers>
      <FlatWorldGenerator generatorString = ""3;7,45*5;,biome_1"" />
      < ServerQuitFromTimeUp timeLimitMs=""3000""/>
      <ServerQuitWhenAnyAgentFinishes/>
    </ServerHandlers>
  </ServerSection>

  <AgentSection mode = ""Creative"" >
    < Name > Paco </ Name >
    < AgentStart >
      < Placement x=""0.5"" y=""46.0"" z=""0.5"" pitch=""70"" yaw=""0""/>
      <Inventory>
        <InventoryItem slot = ""0"" type=""dirt""/>
      </Inventory>
    </AgentStart>
    <AgentHandlers>
      <ChatCommands/>
      <ObservationFromFullStats/>
      <ObservationFromGrid>
        <Grid name = ""level0"" >
          < min x=""-2"" y=""0"" z=""-2""/>
          <max x = ""2"" y=""0"" z=""2""/>
        </Grid>
        <Grid name = ""levelSub1"" >
          < min x=""-2"" y=""-1"" z=""-2""/>
          <max x = ""2"" y=""-1"" z=""2""/>
        </Grid>
        <Grid name = ""levelSub2"" >
          < min x=""-2"" y=""-2"" z=""-2""/>
          <max x = ""2"" y=""-2"" z=""2""/>
        </Grid>
      </ObservationFromGrid>
      <DiscreteMovementCommands/>
      <VideoProducer
       viewpoint = ""1"" >
          < Width > 432 </ Width >
          < Height > 240 </ Height >
      </ VideoProducer >
    </ AgentHandlers >
  </ AgentSection >

</ Mission >
";
    }
}
