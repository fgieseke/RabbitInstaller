using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Newtonsoft.Json;
using RabbitInstaller.Infrastructure;
using System.IO;
using System.Threading;
using RabbitMQ.Client;
using RawRabbit.Configuration;

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
            _subscribers = new List<SimulationConsumer>();
            _modelConfig = LoadJson<ModelConfig>("setup.json");
            var configuration = new RawRabbitConfiguration
            {
                Hostnames = _modelConfig.Hosts.ToList(),
                Username = _modelConfig.User,
                Password = _modelConfig.Password,
                VirtualHost = _modelConfig.VHost,
                RouteWithGlobalId = false
            };

            using (var connection = BusClientFactory.CreateConnection(configuration))
            {

                try
                {
                    Console.WriteLine("RabbitCLI> type 'help' or '?' for command list.");
                    bool isRunning = true;
                    while (isRunning)
                    {

                        Console.Write("RabbitCLI> ");
                        var command = Console.ReadLine();
                        if (command == null)
                            break;

                        var actions = command?.Split(' ');
                        switch (actions[0])
                        {
                            case "?":
                            case "help":
                                Console.WriteLine("run <scenario-name>  : runs a scenario");
                                Console.WriteLine("setup                : Setup infrastructure");
                                Console.WriteLine("sc or scenarios      : List of scenarios");
                                Console.WriteLine("cleanup              : Cleanup infrastructure: Removes all defined exchanges!");
                                Console.WriteLine("delete <queuename>   : Deletes a queue");
                                Console.WriteLine("x or exit            : exits CLI");
                                Console.WriteLine("cls                  : console clear");
                                break;
                            case "cls":
                                Console.Clear();
                                break;
                            case "setup":
                                SetupModel(connection, _modelConfig);
                                break;
                            case "sc":
                            case "scenarios":
                                ListScenarios();
                                break;
                            case "cleanup":
                                CleanupModel(connection, _modelConfig);
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
                            case "delete":
                                if (actions.Length < 2)
                                {
                                    Console.WriteLine("\nDelete requires a second argument 'queuename'!");
                                }
                                else
                                {
                                    DeleteQueue(connection, actions[1]);
                                }
                                break;
                            case "x":
                            case "exit":
                                isRunning = false;
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

        private static void DeleteQueue(IConnection connection, string queuename)
        {
            using (var channel = new ModelBuilder(connection).Model)
            {
                try
                {
                    var queueExists = channel.QueueDeclarePassive(queuename);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unknown queue '{queuename}'");
                    return;
                }
                Console.WriteLine($"Queue '{queuename}' deleted.");

            }
        }

        private static void ListScenarios()
        {
            var scenarioConfigFile = LoadJson<ScenarioConfigFile>("scenarios.json");
            if (scenarioConfigFile == null)
            {
                Console.WriteLine("Missing file 'scenarios.json'.");
                return;
            }

            foreach (var scenario in scenarioConfigFile.Scenarios)
            {
                Console.WriteLine($"# {scenario.Name}");
                Console.WriteLine($"  Steps");
                foreach (var step in scenario.Steps)
                {
                    Console.WriteLine($"    - {step}");
                }
                if (scenario.Environments == null)
                    continue;
                Console.WriteLine($"  Uses environments:");
                foreach (var env in scenario.Environments)
                {
                    Console.WriteLine($"    - {env.ExchangeName}: {env.Variant}");
                }
            }
        }

        private static void CleanupModel(IConnection connection, ModelConfig modelConfig)
        {
            using (var modelBuilder = new ModelBuilder(connection))
            {
                // now unbind the Out-Exchanges
                foreach (var exchangeConfig in modelConfig.Exchanges.Where(e =>
                    e.Direction == Enums.Exchange.Direction.Out))
                {
                    modelBuilder.UnbindExchange(exchangeConfig.ExchangeName, exchangeConfig.Binding.Exchanges);
                }

                // delete all Exchanges 
                foreach (var exchangeConfig in modelConfig.Exchanges)
                {
                    modelBuilder.DeleteExchange(exchangeConfig);
                }

            }

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
            if (_lastScenario != null && _lastScenario != scenarioName && !CleanUpScenario(envConfigFile.Environments, scenarioConfigFile, _lastScenario))
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

            try
            {
                using (var modelBuilder = new ModelBuilder(connection))
                {
                    SetupEnvironment(modelBuilder.Model, envConfigFile.Environments, scenario.Environments);
                    foreach (var subscriber in _subscribers)
                    {
                        subscriber.Start();
                    }

                    StartEmitter(modelBuilder.Model, scenario.Emitter);
                    Thread.Sleep(2000);
                    Console.WriteLine("Hit return when last message was received.");
                    Console.ReadLine();

                    Console.Write("Unsubscribing consumers...");
                    foreach (var subscriber in _subscribers)
                    {
                        subscriber.Stop();
                    }
                    _subscribers.Clear();
                    Console.WriteLine("Done!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static void SetupEnvironment(IModel channel, EnvironmentConfig[] envConfigs, EnvironmentElement[] environments)
        {
            Console.WriteLine("Setting up environments... ");
            foreach (var env in environments)
            {
                var envFound = envConfigs.FirstOrDefault(e => e.ExchangeName == env.ExchangeName);
                if (envFound?.Variants == null || !envFound.Variants.Any())
                    throw new ConfigurationErrorsException($"Could not find exchange '{env.ExchangeName}' in environmentConfig.");

                foreach (var variant in envFound.Variants)
                {
                    if (env.Variant != null && variant.Name != env.Variant)
                        continue;

                    if (variant.Consumer != null)
                    {
                        foreach (var routingKey in variant.Consumer.Binding.RoutingKeys)
                        {
                            var queueBinding = DeclareAndBindQueues(channel, envFound.ExchangeName, variant.QueueNamePattern, routingKey);
                            var sim = new SimulationConsumer(channel, $"{queueBinding.RoutingKey}-Consumer", queueBinding.QueueName);
                            _subscribers.Add(sim);
                        }
                    }
                    if (variant.Router != null)
                    {
                        foreach (var routingKey in variant.Router.Binding.RoutingKeys)
                        {
                            var queueBinding = DeclareAndBindQueues(channel, envFound.ExchangeName, variant.QueueNamePattern, routingKey);
                            if (variant.Router.Publish != null)
                            {
                                var mode = variant.Router.Publish.Modes.FirstOrDefault(m => m.Name == env.RoutingMode);
                                if (mode == null)
                                    throw new ConfigurationErrorsException($"Could not find routing mode '{env.RoutingMode}' in environmentConfig.");

                                var sim = new SimulationConsumer(channel, $"{queueBinding.RoutingKey}-Consumer", queueBinding.QueueName, new ConsumerPublishConfig
                                {
                                    ExchangeName = variant.Router.Publish.ExchangeName,
                                    RoutingKey = mode.RoutingKey
                                });
                                _subscribers.Add(sim);
                            }
                        }
                    }
                }
            }
        }

        private static QueueBindingInfo DeclareAndBindQueues(IModel channel, string exchangeName, string queueName, string routingKey, Dictionary<string, object> arguments = null)
        {
            var routingParts = routingKey.Split('.');
            // variable queueName 
            var queueName2Bind = ReplacePlaceHolder(queueName, routingParts);
            channel.QueueDeclare(queueName2Bind, durable: true, autoDelete: false, exclusive: false);

            channel.QueueBind(queueName2Bind, exchangeName, routingKey, arguments);
            channel.QueuePurge(queueName2Bind);

            var qBind = new QueueBindingInfo
            {
                ExchangeName = exchangeName,
                QueueName = queueName2Bind,
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


        private static void StartEmitter(IModel channel, EmitterConfig scenarioEmitter)
        {
            foreach (var routingKey in scenarioEmitter.RoutingKeys)
            {
                Console.WriteLine($"Hit return to start emitting message with routingkey '{routingKey}'");
                Console.ReadLine();
                var simEmit = new SimulationEmitter(channel, scenarioEmitter.ExchangeName, routingKey);
                simEmit.Start();
                Thread.Sleep(2000);
            }

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
