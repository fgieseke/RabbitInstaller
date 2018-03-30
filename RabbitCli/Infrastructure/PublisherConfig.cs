namespace RabbitCli.Infrastructure
{
    public class PublisherConfig
    {
        public string Name { get; set; }
        public string ExchangeName { get; set; }

        public string RoutingKey { get; set; }
        public int MessageCount { get; set; }
        public int Period { get; set; }
    }
}