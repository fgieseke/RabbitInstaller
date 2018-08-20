using System.Collections.Generic;

namespace RabbitCli.Infrastructure
{
    public class ScenarioConfigFile
    {
        public string[] Steps { get; set; }

        public EmitterConfig[] Emitter { get; set; }
    }

    public class EmitterConfig
    {
        public string ExchangeName { get; set; }
        public string[] RoutingKeys { get; set; }
    }
}
