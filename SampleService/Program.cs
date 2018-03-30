using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using log4net.Config;
using RabbitMQ.Client;
using RawRabbit.Common;
using RawRabbit.Configuration;

namespace SampleService
{
    class Program
    {
        public static ILog Logger { get; private set; }

        static void Main(string[] args)
        {
            Logger = LogManager.GetLogger("root");
            XmlConfigurator.Configure();

            Logger.Info("Service started");
            var configuration = new RawRabbitConfiguration
            {
                Hostnames = new List<string>( new [] {"localhost"}),
                Username = "guest",
                Password = "guest",
                VirtualHost = "/",
                RouteWithGlobalId = false
            };
            using (var connection = CreateConnection(configuration))
            using (var model = connection.CreateModel())
            {
                var consumer = new SimulationConsumer(Logger, model, "SampleService", "Translator-text-xyz-Queue");
                consumer.Start();
                Console.WriteLine("Hit return to exit...");
                Console.ReadLine();
            }
        }


        public static IConnection CreateConnection(RawRabbitConfiguration config)
        {
            var factory = CreateConnectionFactory(config);
            return factory.CreateConnection();
        }

        public static ConnectionFactory CreateConnectionFactory(RawRabbitConfiguration config)
        {
            var provider = new ClientPropertyProvider();

            var factory = new ConnectionFactory
            {
                VirtualHost = config.VirtualHost,
                UserName = config.Username,
                Password = config.Password,
                Port = config.Port,
                HostName = config.Hostnames.FirstOrDefault() ?? string.Empty,
                AutomaticRecoveryEnabled = config.AutomaticRecovery,
                TopologyRecoveryEnabled = config.TopologyRecovery,
                NetworkRecoveryInterval = config.RecoveryInterval,
                ClientProperties = provider.GetClientProperties(config),
                Ssl = config.Ssl
            };

            return factory;
        }
    }
}

