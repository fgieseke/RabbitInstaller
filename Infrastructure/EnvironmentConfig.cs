﻿namespace RabbitInstaller.Infrastructure
{
    public class EnvironmentConfigFile
    {
        public EnvironmentConfig[] Environment { get; set; }
    }

    public class EnvironmentConfig
    {
        public string ExchangeName { get; set; }
        public string QueueName { get; set; }
        public VariantElement[] Variants { get; set; }

    }

    public class VariantElement
    {
        public string Name { get; set; }
        public ConsumerElement[] Consumer { get; set; }
        public RouterElement[] Router { get; set; }
    }

    public class RouterElement : ConsumerElement
    {
        public RouterMode[] Modes { get; set; }

    }

    public class ConsumerElement 
    {
        public BindingElement Binding { get; set; }
    }

    public class BindingElement
    {
        public string[] RoutingKeys { get; set; }
    }


    public class RouterMode
    {
        public string Name { get; set; }
        public string ExchangeName { get; set; }
        public string RoutingKey { get; set; }
    }
}
