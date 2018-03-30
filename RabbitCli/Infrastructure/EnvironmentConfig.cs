namespace RabbitCli.Infrastructure
{
    public class EnvironmentConfigFile
    {
        public EnvironmentConfig[] Environments { get; set; }
    }

    public class EnvironmentConfig
    {
        public string ExchangeName { get; set; }
        public VariantElement[] Variants { get; set; }

    }

    public class VariantElement
    {
        public string Name { get; set; }
        public string QueueNamePattern { get; set; }
        public ConsumerElement Consumer { get; set; }
        public RouterElement Router { get; set; }
    }

    public class RouterElement : ConsumerElement
    {
        public PublishElement Publish { get; set; }
    }

    public class PublishElement
    {
        public RouterMode[] Modes { get; set; }
        public string ExchangeName { get; set; }
    }

    public class ConsumerElement 
    {
        public BindingElement Binding { get; set; }
        public string Executable { get; set; }
    }

    public class BindingElement
    {
        public string[] RoutingKeys { get; set; }
    }


    public class RouterMode
    {
        public string Name { get; set; }
        public string RoutingKey { get; set; }
    }
}
