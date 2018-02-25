using System.Collections.Generic;

namespace RabbitInstaller.Infrastructure
{
    public class QueueBindingInfo
    {
        public string ExchangeName { get; set; }
        public string QueueName { get; set; }
        public string RoutingKey { get; set; }
        public IDictionary<string, object> Arguments { get; set; }
    }
}