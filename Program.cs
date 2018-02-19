using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Newtonsoft.Json;
using RabbitInstaller.Infrastructure;
using RawRabbit.Common;
using System.IO;
using Fclp.Internals.Extensions;
using RabbitMQ.Client;

namespace RabbitInstaller
{
    class Program
    {
        private static string _lastScenario;
        private static EnvironmentConfigFile _lastEnvConfigFile;
        private static ScenarioConfigFile _lastScenarioConfigFile;
        private static ModelConfig _modelConfig;
        private static List<SimulationConsumer> _subscribers;

        static void Main(string[] args)
        {

            var serviceBusConfig = ConfigurationManager.ConnectionStrings["RabbitMqConnection"];
            var configuration = ConnectionStringParser.Parse(serviceBusConfig.ConnectionString);
            configuration.RouteWithGlobalId = false;
            configuration.VirtualHost = "poc";

            _modelConfig = LoadJson<ModelConfig>("setup3.json");

            using (var connection = BusClientFactory.CreateConnection(configuration))
            {

                try
                {
                    while (true)
                    {

                        Console.Write("RabbitSim> ");
                        var command = Console.ReadLine();
                        if (command == null)
                            break;

                        var actions = command?.Split(' ');
                        switch (actions[0])
                        {
                            case "help":
                                Console.WriteLine("run scenario-name: runs a scenario");
                                Console.WriteLine("setup: Setup Infrastructure");
                                Console.WriteLine("cleanup: Cleanup Infrastructure: Removes all defined Exchanges!");
                                break;
                            case "cls":
                                Console.Clear();
                                break;
                            case "setup":
                                SetupModel(connection, _modelConfig);
                                break;
                            case "cleanup":
                                CleanupModel(connection, _modelConfig, _lastScenario);
                                break;
                            case "run":
                                if (actions.Length < 2)
                                {
                                    Console.WriteLine("\nRun requires a second argument 'scenario-name'!");
                                }
                                else
                                {
                                    RunScenario(connection, actions[1]);
                                }
                                break;
                        }

                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
                finally
                {
                    Console.WriteLine("\nShutting down connections...");
                    connection.Close();
                }
            }
            Console.WriteLine("Press [enter] to exit.");
            Console.ReadLine();

        }

        private static void CleanupModel(IConnection connection, ModelConfig modelConfig, string lastScenario)
        {
            throw new NotImplementedException();
        }

        private static void SetupModel(IConnection connection, ModelConfig modelConfig)
        {
            using (var modelBuilder = new ModelBuilder(connection))
            {
                // create all Exchanges 
                foreach (var exchangeConfig in modelConfig.Exchanges)
                {
                    modelBuilder.CreateExchange(exchangeConfig);
                    // create queues and bindings to this In-exchange on the fly
                    if (exchangeConfig.Direction == Enums.Exchange.Direction.In)
                    {
                        if (exchangeConfig.Binding?.Exchanges?.Count() > 0)
                        {
                            throw new ConfigurationErrorsException(
                                $"Binding an exchange to the IN exchange '{exchangeConfig.ExchangeName}' in not allowed!");
                        }
                    }
                }

                // now bind the Out-Exchanges
                foreach (var exchangeConfig in modelConfig.Exchanges.Where(e =>
                    e.Direction == Enums.Exchange.Direction.Out))
                {
                    modelBuilder.BindExchange(exchangeConfig.ExchangeName, exchangeConfig.Binding.Exchanges);
                    if (exchangeConfig.Binding?.Queues?.Count() > 0)
                    {
                        throw new ConfigurationErrorsException(
                            $"Binding queues to the OUT exchange '{exchangeConfig.ExchangeName}' in not allowed!");
                    }
                }
            }
        }

        public static void CleanUpEnvironment()
        {
            Console.WriteLine("Unbinding all queues...");

            Console.WriteLine("Unbinding all queues...Done");
        }

        private static T LoadJson<T>(string fileName)
        {
            using (var r = new StreamReader(fileName))
            {
                var json = r.ReadToEnd();
                var config = JsonConvert.DeserializeObject<T>(json);
                return config;
            }
        }

        private static void RunScenario(IConnection connection, string scenarioName)
        {
            var envConfigFile = LoadJson<EnvironmentConfigFile>("environment.json");
            var scenarioConfigFile = LoadJson<ScenarioConfigFile>("scenarios.json");
            if (_lastScenario != null && _lastScenario != scenarioName && !CleanUpScenario(envConfigFile.Environment, scenarioConfigFile, _lastScenario))
                return;

            _lastScenario = scenarioName;
            _lastScenarioConfigFile = scenarioConfigFile;
            _lastEnvConfigFile = envConfigFile;

            var scenario = scenarioConfigFile.Scenarios.FirstOrDefault(s => s.Name.ToLower() == scenarioName.ToLower());
            if (scenario == null)
            {
                Console.WriteLine($"Unknown scenario '{scenarioName}'!");
                return;
            }
            Console.WriteLine($"Preparing scenario '{scenarioName}'...");
            if (scenario.Emitter == null)
            {
                Console.WriteLine("No emitter configured!");
                return;
            }

            using (var modelBuilder = new ModelBuilder(connection))
            {
                var queueBinds = SetupEnvironment(modelBuilder.Model, envConfigFile.Environment, scenario.Environment);
                SubscribeRouters(scenario.Router);
                foreach (var queueBind in queueBinds)
                {
                    var routingParts = queueBind.RoutingKey.Split('.');
                    var consumerName = $"{queueBind.ExchangeName}-{routingParts[1]}-{routingParts[2]}-Consumer";
                    SimulationConsumer simConsumer = new SimulationConsumer(modelBuilder.Model, consumerName, queueBind.QueueName);
                    _subscribers.Add(simConsumer);
                }

                RunEmitter(scenario.Emitter);
            }
        }
        private static IEnumerable<QueueBindingInfo> SetupEnvironment(IModel channel, EnvironmentConfig[] envConfigs, string[] environments)
        {
            var list = new List<QueueBindingInfo>();

            foreach (var env in environments)
            {
                var envSplit = env.Split(':');
                var envExchangeName = envSplit[0];
                var envVariant = envSplit.Length > 1 ? envSplit[1] : null;
                var envFound = envConfigs.FirstOrDefault(e => e.ExchangeName == envExchangeName);
                if (envFound?.Variants == null || !envFound.Variants.Any())
                {
                    Console.WriteLine($"Could not find environment '{envExchangeName}'.");
                    continue;
                }
                var envFoundExchangeName = envFound.ExchangeName;
                if (envFoundExchangeName == null)
                {
                    Console.WriteLine($"Could not find exchange '{envExchangeName}' in environmentConfig.");
                    continue;
                }

                foreach (var variant in envFound.Variants)
                {
                    if (envVariant != null && variant.Name != envVariant)
                        continue;

                    if (variant.Consumer != null && variant.Consumer.Any())
                    {
                        foreach (var consumerElement in variant.Consumer)
                        {
                            foreach (var routingKey in consumerElement.Binding.RoutingKeys)
                            {
                                var queueBinding = DeclareAndBindQueues(channel, envFoundExchangeName, envFound.QueueName, routingKey);
                                list.Add(queueBinding);
                            }
                        }
                    }
                    if (variant.Router != null && variant.Router.Any())
                    {
                        foreach (var routerElement in variant.Router)
                        {
                            foreach (var routingKey in routerElement.Binding.RoutingKeys)
                            {
                                var queueBinding = DeclareAndBindQueues(channel, envFoundExchangeName, envFound.QueueName,routingKey);
                                list.Add(queueBinding);
                            }
                        }
                    }
                }
            }

            return list;
        }

        private static QueueBindingInfo DeclareAndBindQueues(IModel channel, string exchangeName, string queueName, string routingKey, Dictionary<string, object> arguments = null)
        {
            var routingParts = routingKey.Split('.');
            // variable queueName 
            var queueName2Bind = ReplacePlaceHolder(queueName, routingParts);
            channel.QueueDeclare(queueName2Bind, durable: true, autoDelete: false, exclusive: false);

            channel.QueueBind(queueName, exchangeName, routingKey, arguments);

            var qBind = new QueueBindingInfo
            {
                ExchangeName = exchangeName,
                QueueName = queueName,
                RoutingKey = routingKey,
                Arguments = arguments

            };

            return qBind;
        }

        private static string ReplacePlaceHolder(string templateString, string[] routingParts)
        {
            var replacedString = templateString;
            replacedString = replacedString.Replace("{messageType}", routingParts[0]);
            replacedString = replacedString.Replace("{contentType}", routingParts[1]);
            replacedString = replacedString.Replace("{origin}", routingParts[2]);
            replacedString = replacedString.Replace("{state}", routingParts[3]);
            return replacedString;
        }

        private static void SubscribeConsumer(ModelBuilder consumer, ConsumerElement variantConsumer)
        {
            throw new NotImplementedException();
        }

        private static void RunEmitter(EmitterConfig scenarioEmitter)
        {
            throw new NotImplementedException();
        }

        private static void SubscribeRouters(string[] scenarioRouter)
        {


        }



        private static bool CleanUpScenario(EnvironmentConfig[] envConfig, ScenarioConfigFile scenarioConfigFile,
            string lastScenario)
        {
            Console.Write("Clean up old environment.");
            Console.WriteLine("Done.");
            Console.WriteLine("Could not clean up environment.");
            return true;
        }
    }
}
