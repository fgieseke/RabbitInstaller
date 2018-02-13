using System.Collections.Generic;
using RawRabbit.Configuration.Exchange;
using RawRabbit.Configuration.Queue;

namespace RabbitInstaller.Infrastructure
{
    public class ModelConfig
    {
        public string[] Hosts { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string VHost { get; set; }
        public ExchangeModelConfig[] Exchanges { get; set; }
        public QueueConfiguration[] Queues { get; set; }
        public SimulationConfig Simulation { get; set; }
    }

    public class SimulationConfig
    {
        public bool Enabled { get; set; }
        public ScenarioConfig[] Scenarios { get; set; }
    }

    public class ScenarioConfig
    {
        public string Name { get; set; }
        public ConsumerConfig[] Consumers { get; set; }
        public PublisherConfig[] Publishers { get; set; }

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

    public class ExchangeModelConfig : ExchangeConfiguration
    {
        public BindingModelConfig[] Bindings { get; set; }
    }

    public class BindingModelConfig
    {
        public string ExchangeName { get; set; }
        public string QueueName { get; set; }
        public string[] RoutingKeys { get; set; }
        public Dictionary<string, object> Arguments { get; set; }
    }
}
