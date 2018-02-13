using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RabbitInstaller.Infrastructure;
using RawRabbit;
using RawRabbit.Common;
using RawRabbit.Context;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using ExchangeType = RawRabbit.Configuration.Exchange.ExchangeType;

namespace RabbitInstaller
{
    class Program
    {
        static void Main(string[] args)
        {

            var serviceBusConfig = ConfigurationManager.ConnectionStrings["RabbitMqConnection"];
            var configuration = ConnectionStringParser.Parse(serviceBusConfig.ConnectionString);
            configuration.RouteWithGlobalId = false;
            configuration.VirtualHost = "poc";

            var modelConfig = LoadJson("setup.json");
            var scenarioName = "Default";
            ScenarioConfig scenario = modelConfig.Simulation.Scenarios.First();

            if (args.Length > 0)
            {
                var keyValue = args[0].Split(':');
                if (keyValue.Length > 1 && keyValue[0] == "-s")
                {
                    scenarioName = keyValue[1];
                    scenario = modelConfig.Simulation.Scenarios.FirstOrDefault(s => s.Name == scenarioName);
                }
            }


            using (var connection = BusClientFactory.CreateConnection(configuration))
            {

                try
                {
                    using (var modelBuilder = new ModelBuilder(connection))
                    {


                        foreach (var exchangeConfig in modelConfig.Exchanges)
                        {
                            modelBuilder.CreateExchange(exchangeConfig);
                        }

                        foreach (var queueConfig in modelConfig.Queues)
                        {
                            modelBuilder.CreateQueue(queueConfig);
                        }

                        foreach (var exchangeConfig in modelConfig.Exchanges)
                        {
                            modelBuilder.BindExchange(exchangeConfig.ExchangeName, exchangeConfig.Bindings);
                        }


                        if (modelConfig.Simulation.Enabled)
                        {
                            SetupSimulation(modelBuilder, scenario);

                            modelBuilder.StartSimulation();
                        }

                        Console.WriteLine("Press [c] to close connection.");
                        Console.ReadLine();
                    }
                }
                finally
                {
                    connection.Close();
                }
            }
            Console.WriteLine("Press [enter] to exit.");
            Console.ReadLine();

        }

        private static void SetupSimulation(ModelBuilder modelBuilder, ScenarioConfig scenario)
        {
            Console.WriteLine($"Setting up scenario '{scenario.Name}'...");

            Console.WriteLine("Setting up consumers ...");

            foreach (var consumer in scenario.Consumers)
            {
                for (int i = 0; i < consumer.Instances; i++)
                {
                    foreach (var queueName in consumer.QueueNames)
                    {
                        var simConsumer = new SimulationConsumer(modelBuilder.Model, consumer.Name + "-" + i, queueName, consumer.Publish);
                        modelBuilder.AddConsumer(simConsumer);
                    }
                }

            }

            Console.WriteLine("Setting up publishers ...");

            foreach (var publisher in scenario.Publishers)
            {
                var simPublisher = new SimulationPublisher(modelBuilder.Model, publisher);
                modelBuilder.AddPublisher(simPublisher);
            }
        }


        private static ModelConfig LoadJson(string fileName)
        {
            using (var r = new StreamReader(fileName))
            {
                var json = r.ReadToEnd();
                var config = JsonConvert.DeserializeObject<ModelConfig>(json);
                return config;
            }
        }


    }
}
