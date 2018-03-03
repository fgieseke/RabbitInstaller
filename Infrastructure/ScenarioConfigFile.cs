using System.Collections.Generic;
using System.Linq;

namespace RabbitInstaller.Infrastructure
{


    public class ScenarioConfigFile
    {

        public Scenario[] Scenarios { get; set; }
    }

    public class Scenario
    {
        private string[] _router;
        private string[] _environments;
        private Dictionary<string, string> _routerMapMode = new Dictionary<string, string>();
        private Dictionary<string, string> _envMapMode = new Dictionary<string, string>();
        public string Name { get; set; }
        public string[] Steps { get; set; }

        public EnvironmentElement[] Environments { get; set; }

        public EmitterConfig Emitter { get; set; }

    }

    public class EnvironmentElement
    {
        public string ExchangeName { get; set; }
        public string Variant { get; set; }
        public string RoutingMode { get; set; }

    }

    public class EmitterConfig
    {
        public string ExchangeName { get; set; }
        public string[] RoutingKeys { get; set; }
    }

    //public class ConsumerConfig
    //{
    //    public string Name { get; set; }
    //    public int Instances { get; set; }
    //    public string[] QueueNames { get; set; }
    //    public ConsumerPublishConfig Publish { get; set; }
    //}
}
