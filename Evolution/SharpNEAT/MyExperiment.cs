using SharpNeat.Core;
using SharpNeat.Phenomes;
using System.Xml;

namespace SharpNeat
{
    public class MyExperiment : SimpleNeatExperiment
    {
        public override IPhenomeEvaluator<IBlackBox> PhenomeEvaluator
        {
            get { return new OnlineMalmoPseudoEvaluator(); }
        }

        /// <summary>
        /// Defines the number of input nodes in the neural network.
        /// This is NOT considering bias.
        /// </summary>
        public override int InputCount
        {
            get { return 6; }
        }

        /// <summary>
        /// Defines the number of output nodes in the neural network.
        /// </summary>
        public override int OutputCount
        {
            get { return 2; }
        }

        /// <summary>
        /// The constructor with no inputs is made private to ensure only this
        /// constructor is used.
        /// </summary>
        public MyExperiment(string xmlFile, string userName)
        {
            XmlDocument xmlConfig = new XmlDocument();
            xmlConfig.Load(xmlFile);
            string experimentName = "Malmo";
            // The documentElement property returns the root node of the document.
            Initialize(experimentName, xmlConfig.DocumentElement, userName);
        }
        private MyExperiment() {}
    }
}
