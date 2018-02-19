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
        private string[] _environment;
        private Dictionary<string, string> _routerMapMode = new Dictionary<string, string>();
        private Dictionary<string, string> _envMapMode = new Dictionary<string, string>();
        public string Name { get; set; }

        public string[] Environment
        {
            get => _environment;
            set
            {
                _environment = value;
                foreach (var envMode in _environment)
                {
                    var envModeSplit = envMode.Split(':');
                    if (envModeSplit.Length == 2)
                    {
                        _envMapMode[envModeSplit[0]] = envModeSplit[1];
                    }
                }
            }
        }

        public EmitterConfig Emitter { get; set; }

        public string[] Router
        {
            get => _router;
            set
            {
                _router = value; 
                foreach (var routerMode in _router)
                {
                    var routerModeSplit = routerMode.Split(':');
                    if (routerModeSplit.Length == 2)
                    {
                        _routerMapMode[routerModeSplit[0]] = routerModeSplit[1];
                    }
                }
            }
        }


        //public Dictionary<string, RouterMode> RouterModes => _envMapMode;

    }

    public class EmitterConfig
    {
        public string ExchangeName { get; set; }
        public string RoutingKey { get; set; }
    }

    public class PublisherConfig
    {
        public string Name { get; set; }
        public string ExchangeName { get; set; }

        public string RoutingKey { get; set; }
        public int MessageCount { get; set; }
        public int Period { get; set; }
    }

    public class ConsumerConfig
    {
        public string Name { get; set; }
        public int Instances { get; set; }
        public string[] QueueNames { get; set; }
        public ConsumerPublishConfig Publish { get; set; }
    }

    public class ConsumerPublishConfig
    {
        public string ExchangeName { get; set; }
        public string RoutingKey { get; set; }
    }
}
