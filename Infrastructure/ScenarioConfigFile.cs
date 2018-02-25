using System.Collections.Generic;
using System.Linq;

namespace RabbitInstaller.Infrastructure
{


    public class ScenarioConfigFile
    {
        //private Dictionary<string, VariantElement[]> _envMap;

        //public ScenarioConfigFile()
        //{
        //    RouterModes = new Dictionary<string, IEnumerable<RouterMode>>();
        //}

        public Scenario[] Scenarios { get; set; }

        //public Dictionary<string, IEnumerable<RouterMode>> RouterModes { get; private set; }

        //public RouterMode GetRouterConfig(string router, string mode)
        //{
        //    RouterModes.TryGetValue(router, out var routerModes);
        //    return routerModes?.FirstOrDefault(r => r.Name == mode);
        //}

        //public void AddEnvironments(EnvironmentConfig[] envConfig)
        //{
        //    _envMap = envConfig.ToDictionary(e => e.ExchangeName, e => e.Variants);
        //    foreach (var environmentConfig in envConfig.Where(e => e.Variants.Any(v => v.Router != null)))
        //    {
        //        RouterModes.Add(environmentConfig.ExchangeName, environmentConfig.Variants.Select(v => v.Router));
        //    }
        //}
    }

    public class Scenario
    {
        private string[] _router;
        private string[] _environments;
        private Dictionary<string, string> _routerMapMode = new Dictionary<string, string>();
        private Dictionary<string, string> _envMapMode = new Dictionary<string, string>();
        public string Name { get; set; }

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
