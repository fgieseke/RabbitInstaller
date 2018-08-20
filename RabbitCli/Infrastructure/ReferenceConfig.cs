using System.Collections.Generic;
using Newtonsoft.Json;

namespace RabbitCli.Infrastructure
{
    public class ReferenceConfig
    {
        public BindingConfig[] Bindings { get; set; }
    }

    public class BindingConfig 
    {
        [JsonProperty("source")]
        public string ExchangeName { get; set; }

        [JsonProperty("destination")]
        public string  QueueName { get; set; }

        [JsonProperty("routing_key")]
        public string RoutingKey { get; set; }

        [JsonProperty("destination_type")]
        public string DestinationType { get; set; }


        [JsonProperty("vhost")]
        public string VHost { get; set; }

        public Dictionary<string, object> Arguments { get; set; }


    }
}