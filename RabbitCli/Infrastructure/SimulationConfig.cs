namespace RabbitCli.Infrastructure
{
    public class SimulationConfig
    {
        /// <summary>
        /// In-ExchangeName to bind queues to.
        /// </summary>
        public string ExchangeName { get; set; }
        public string QueueNamePattern { get; set; }
        public ConsumerElement[] Consumer { get; set; }
        public RouterElement[] Router { get; set; }
        public PublishElement[] Ignored { get; set; }
        public PublishElement[] Deleted { get; set; }
        public PublishElement[] Unbind { get; set; }
    }

    public class RouterElement  : PublishElement
    {
        public string Executable { get; set; }
        public string Message { get; set; }
        /// <summary>
        /// Out-ExchangeName to send messages to.
        /// </summary>
        public string ExchangeName { get; set; }
        /// <summary>
        /// RoutingKey used when sending messages.
        /// </summary>
        public string To { get; set; }
    }

    public class ConsumerElement : PublishElement
    {
        public string Executable { get; set; }
        public string Message { get; set; }
    }

    public class PublishElement
    {
        public string QueueName { get; set; }
        public string RoutingKey { get; set; }
    }
}
