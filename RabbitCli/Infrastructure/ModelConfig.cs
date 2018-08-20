using System.Collections.Generic;
using RawRabbit.Configuration.Exchange;
using RawRabbit.Configuration.Queue;

namespace RabbitCli.Infrastructure
{
    public class ModelConfig
    {
        public string[] Hosts { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string VHost { get; set; }
        public int Port { get; set; }
    }


    public class SetupConfig
    {
        public ExchangeModelConfig[] Exchanges { get; set; }
        public string ExecutableBasePath { get; set; }
    }

    public class ExchangeModelConfig : ExchangeConfiguration
    {
        public BindingModelConfig Binding { get; set; }
        public string Direction { get; set; }
        /// <summary>
        /// If True this Exchange will not be deleted on cleanup and In/Out-Binding-Rules will not be checked
        /// </summary>
        public bool IsLegacy { get; set; }

        /// <summary>
        /// If true it defines an alternate Exchange for this exchange for unrouted messages
        /// </summary>
        public bool KeepUnroutedMessages { get; set; }
    }

    public class BindingModelConfig
    {
        public ExchangeBindingConfiguration[] Exchanges { get; set; }
        public QueueBindingConfiguration[] Queues { get; set; }
    }

    public class QueueBindingConfiguration : QueueConfiguration
    {
        public string RoutingKey { get; set; }
    }

    public class ExchangeBindingConfiguration
    {
        public string ExchangeName { get; set; }
        public string[] RoutingKeys { get; set; }
        public IDictionary<string, object> Arguments { get; set; }
    }
}
