using RabbitCli.Infrastructure;
using RabbitMQ.Client;
using RawRabbit.Configuration;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using EasyNetQ.Management.Client;
using EasyNetQ.Management.Client.Model;
using RabbitMQ.Client.Events;
using RawRabbit.Configuration.Exchange;
using RawRabbit.Configuration.Queue;
using Timer = System.Timers.Timer;

namespace RabbitCli
{
    public class Program
    {
        private static string _lastScenario;
        private static ModelConfig _modelConfig;
        private static List<SimulationConsumer> _subscribers;
        private static string _queueFile = "queues.txt";
        private static bool _userInteractive;
        private static Action<string> _switchEnvDelegate = env => { };
        private static Action<string, string> _switchLoginDelegate = (u, p) => { };
        private static string _currentEnv;
        private static SetupConfig _setupConfig;
        private static string _lastSimulation;

        static void Main(string[] args)
        {

            //var setupEnv = @".\Setup\setup.json";
            var setupEnv = ConfigurationManager.AppSettings["setupEnv"];

            _subscribers = new List<SimulationConsumer>();


            _userInteractive = args == null || args.Length == 0;

            if (_userInteractive)
            {
                UserInteractive(setupEnv);
            }
            else
            {
                var envFile = Utils.GetEnvFileName(setupEnv);
                RunBatch(args, envFile);
            }
        }

        private static RawRabbitConfiguration LoadAndGetRawRabbitConfiguration(string envFile)
        {

            try
            {
                _modelConfig = Utils.LoadJson<ModelConfig>(envFile);

                var configuration = new RawRabbitConfiguration
                {
                    Hostnames = _modelConfig.Hosts.ToList(),
                    Username = _modelConfig.User,
                    Password = _modelConfig.Password,
                    VirtualHost = _modelConfig.VHost,
                    RouteWithGlobalId = false
                };
                if (_modelConfig.Port > 0)
                {
                    configuration.Port = _modelConfig.Port;
                }

                return configuration;

            }
            catch (Exception exception)
            {
                WriteError(exception.Message);
                return null;
            }
        }


        private static void RunBatch(string[] args, string envFile)
        {
            var configuration = LoadAndGetRawRabbitConfiguration(envFile);
            if (configuration == null)
            {
                WriteError($"Could not load '{envFile}'.");

                Console.WriteLine("Press [enter] to exit.");
                Console.ReadLine();
                return;
            }

            using (var connection = BusClientFactory.CreateConnection(configuration))
            {
                try
                {
                    if (args[0].ToLower() == "batch" && !string.IsNullOrEmpty(args[1]))
                    {
                        // batch-mode
                        ExecuteBatch(connection, configuration, args[1], args.Length > 2 ? args[2] : "");
                    }
                    else
                    {
                        // command-mode
                        ExecuteCommand(args, connection, configuration);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        private static string[] GetCommandsFromBatchFile(string batchFileName, string subFolder = "")
        {
            var batchFullFileName = Path.Combine(".\\SetupBatch", subFolder, $"{batchFileName}.bat");
            try
            {
                var lines = File.ReadAllLines(batchFullFileName);
                return lines;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not read batch file '{batchFullFileName}':", ex.Message);
                throw;
            }
        }

        private static void UserInteractive(string setupEnv)
        {
            _currentEnv = setupEnv;
            var newEnv = _currentEnv;
            string userName = null;
            string passWord = null;
            var promt = "RabbitCLI";
            Console.SetIn(new StreamReader(Console.OpenStandardInput(8192))); // https://stackoverflow.com/questions/6081946/why-does-console-readline-have-a-limit-on-the-length-of-text-it-allows/6081967#6081967
            Console.WriteLine($"{promt} [{_currentEnv}]> type 'help' or '?' for command list.");
            Console.WriteLine($"Switch connection using switch <env>. (env: dev/prod/havarie)");

            var isRunning = true;
            _switchEnvDelegate = (env) =>
            {
                newEnv = env;
            };
            _switchLoginDelegate = (user, pass) =>
            {
                userName = user;
                passWord = pass;
            };

            while (isRunning)
            {
                var envFile = Utils.GetEnvFileName(_currentEnv);
                var configuration = LoadAndGetRawRabbitConfiguration(envFile);
                if (configuration == null)
                {
                    WriteError($"Could not load '{envFile}'.");
                    break;
                }
                if (userName != null)
                {
                    configuration.Username = userName;
                }
                else
                {
                    userName = configuration.Username;
                }

                if (passWord != null)
                {
                    configuration.Password = passWord;
                }
                Console.WriteLine($"Using {envFile}. Connected to: {string.Join(", ", configuration.Hostnames)}{configuration.VirtualHost}");
                Console.WriteLine($"Logged in as user '{configuration.Username}'.");
                using (var connection = BusClientFactory.CreateConnection(configuration))
                {
                    try
                    {
                        while (true)
                        {
                            Console.Write($"{promt} [{_currentEnv}]> ");
                            var command = Console.ReadLine();

                            if (command?.ToLower() == "x" || command?.ToLower() == "exit")
                            {
                                isRunning = false;
                                break;
                            }

                            ExecuteCommand(command?.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries), connection, configuration);
                            if (_currentEnv != newEnv || configuration.Username != userName)
                            {
                                _currentEnv = newEnv;
                                break; // close connection to reconnect with new one
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteError("Error: " + ex.Message);
                    }
                    finally
                    {
                        Console.WriteLine("\nShutting down connections...");
                        connection.Close();
                    }
                }
            }

            Console.WriteLine("Press [enter] to exit.");
            Console.ReadLine();

        }

        private static void ExecuteCommand(string[] commandArgs, IConnection connection, RawRabbitConfiguration configuration)
        {
            if (commandArgs == null || commandArgs.Length == 0)
            {
                Console.WriteLine("No command entered.");
                return;
            }
            try
            {
                switch (commandArgs[0].ToLower())
                {
                    case "?":
                    case "help":
                        Console.WriteLine("setup <setupfile>                                                  : Setup infrastructure with setupfile (without .json-extention)");
                        Console.WriteLine("cleanup [<setupfile>]                                              : Cleanup infrastructure: Removes all exchanges defined in setupfile (except legacy)!");
                        Console.WriteLine("switch <targetEnv>                                                 : Switch server environment. (Defined in SetupEnv/env.<env>.json)");
                        Console.WriteLine("login <username> <password>                                        : Login as user <userName>");
                        Console.WriteLine("dq|deletequeue <queuename>                                         : Deletes a queue");
                        Console.WriteLine("bq|bindqueue <queue> <exchangeNameFrom> <routingkey>               : Binds a queue from an exchange with routingkeys");
                        Console.WriteLine("uq|unbindqueue <queue> <exchangeNameFrom> <routingkey>             : Unbinds a queue from an exchange with routingkeys");
                        Console.WriteLine("dx|deleteexchange <exchangeName>                                   : Deletes an exchange");
                        Console.WriteLine("ux|unbindexchange <exchangeNameFrom> <exchangeNameTo> <routingkey> : Unbinds an exchange from another exchange with routingkeys");
                        Console.WriteLine("batch [<subfolder>] <batchName>                                    : Executes commands from batchfile (located under '\\SetupBatch\\<subfolder>')");
                        Console.WriteLine("mm|movemessages <queueName> <targetExchange> [<routingKey>]        : Move messages from one queue to an exchange. Use routingKey to filter messages.");
                        Console.WriteLine("cu|createuser <username> <password> <tag1>...<tagN>                : Create User with name, password and tags");
                        Console.WriteLine("du|deleteuser <username>                                           : Delete User by name");
                        Console.WriteLine("sup|setuserpermissions <username> <config> <write> <read>          : Set user permission with permissions for config, write, read");
                        Console.WriteLine("sc|scenarions                                                      : List of scenarios");
                        Console.WriteLine("sse|setupsimulationenv exchangeName exportFile                     : Setup simulation environment for exchange with reference to existing environment (located in Subfolder 'SetupEnv/Export')");
                        Console.WriteLine("run <scenario-name> <simulationPath>                               : Runs a scenario with preconfigured simulations in SetupSim/<simulationPath>");
                        Console.WriteLine("x|exit                                                             : exits CLI");
                        Console.WriteLine("cls                                                                : console clear");
                        break;
                    case "cls":
                        Console.Clear();
                        break;
                    case "setup":
                        if (commandArgs.Length < 2)
                        {
                            Console.WriteLine("\nsetup requires a second argument 'setupFileName'!");
                            return;
                        }

                        var setupFile = Utils.GetSetupFileName(commandArgs[1]);
                        _setupConfig = Utils.LoadJson<SetupConfig>(setupFile);
                        SetupModel(connection, _setupConfig);
                        break;
                    case "switch":
                        if (commandArgs.Length < 2)
                        {
                            Console.WriteLine("\nswitch requires a second argument 'targetEnv'!");
                            return;
                        }
                        SwitchEnv(commandArgs[1]);
                        break;
                    case "login":
                        if (commandArgs.Length < 3)
                        {
                            Console.WriteLine("\nlogin requires arguments 'userName' and 'password'!");
                            return;
                        }
                        LoginAs(commandArgs[1], commandArgs[2]);
                        break;
                    case "sc":
                    case "scenarios":
                        ListScenarios();
                        break;
                    case "cleanup":
                        var setupConfig = _setupConfig;
                        if (commandArgs.Length > 1)
                        {
                            var setupFile1 = Utils.GetSetupFileName(commandArgs[1]);
                            setupConfig = Utils.LoadJson<SetupConfig>(setupFile1);
                        }
                        if (setupConfig?.Exchanges == null)
                        {
                            Console.WriteLine("\nCleanup requires a second argument 'setupFileName'. Or call setup first.!");
                            return;
                        }
                        CleanupModel(connection, setupConfig);
                        break;

                    case "run":
                        if (commandArgs.Length < 2)
                        {
                            Console.WriteLine("\nRun requires a second argument 'scenario-name'!");
                            return;
                        }
                        else if (commandArgs.Length < 3)
                        {
                            Console.WriteLine("\nRun requires a third argument 'simulationPath'!");
                            return;
                        }
                        RunScenario(connection, commandArgs[1], commandArgs[2]);
                        break;
                    case "dq":
                    case "deletequeue":
                        if (commandArgs.Length < 2)
                        {
                            Console.WriteLine("\nDeleteQueue requires a second argument 'queuename'!");
                            return;
                        }
                        DeleteQueue(connection, commandArgs[1]);
                        break;
                    case "uq":
                    case "unbindqueue":
                        if (commandArgs.Length < 4)
                        {
                            Console.WriteLine("\nUnbindQueue requires arguments 'queueName', 'exchangeName' and 'routingKey'!");
                            return;
                        }
                        UnbindQueue(connection, commandArgs[1], commandArgs[2], commandArgs[3]);
                        break;
                    case "bq":
                    case "bindqueue":
                        if (commandArgs.Length < 4)
                        {
                            Console.WriteLine("\nBindQueue requires arguments 'queueName', 'exchangeName' and 'routingKey'!");
                            return;
                        }
                        BindQueue(connection, commandArgs[1], commandArgs[2], commandArgs[3]);
                        break;
                    case "ux":
                    case "unbindexchange":
                        if (commandArgs.Length < 4)
                        {
                            Console.WriteLine("\nUnbindExchange requires arguments 'exchangeNameFrom', 'exchangeNameTo' and 'routingKey'!");
                            return;
                        }
                        UnbindExchange(connection, commandArgs[1], commandArgs[2], commandArgs[3]);
                        break;
                    case "dx":
                    case "deleteexchange":
                        if (commandArgs.Length < 2)
                        {
                            Console.WriteLine("\nDeleteExchange requires a second argument 'exchangename'!");
                            return;
                        }
                        DeleteExchange(connection, commandArgs[1]);
                        break;
                    case "batch":
                        if (commandArgs.Length < 2)
                        {
                            Console.WriteLine("\nBatch requires a second argument 'batchfile' without extention!");
                            return;
                        }

                        if (commandArgs.Length > 2)
                        {
                            ExecuteBatch(connection, configuration, commandArgs[2], commandArgs[1]);
                        }
                        else
                        {
                            ExecuteBatch(connection, configuration, commandArgs[1]);
                        }
                        break;
                    case "cu":
                    case "createuser":
                        {
                            if (commandArgs.Length < 3)
                            {
                                Console.WriteLine("\nCreate user requires arguments username and password!");
                                return;
                            }
                            string[] tags = { };
                            if (commandArgs.Length > 3)
                            {
                                Array.Resize<string>(ref tags, commandArgs.Length - 3);
                                Array.Copy(commandArgs, 3, tags, 0, commandArgs.Length - 3);
                            }
                            CreateUser(configuration, commandArgs[1], commandArgs[2], tags);
                            break;
                        }
                    case "sup":
                    case "setuserpermissions":
                        {
                            if (commandArgs.Length < 4)
                            {
                                Console.WriteLine("\nSet user permissions requires arguments username, permissionConfig, permissionWrite, permissionRead!");
                                return;
                            }
                            SetUserPermissions(configuration, commandArgs[1], commandArgs[2], commandArgs[3], commandArgs[4]);
                            break;
                        }
                    case "du":
                    case "deleteuser":
                        {
                            if (commandArgs.Length < 2)
                            {
                                Console.WriteLine("\nDelete user arguments username!");
                                return;
                            }
                            DeleteUser(configuration, commandArgs[1]);
                            break;
                        }
                    case "mm":
                    case "movemessages":
                        {
                            if (commandArgs.Length < 3)
                            {
                                Console.WriteLine("\nMoveMessage requires  arguments queueName and targetExchange!");
                                return;
                            }

                            var routingKey = commandArgs.Length > 3 ? commandArgs[3] : "";
                            MoveMessages(connection, commandArgs[1], commandArgs[2], routingKey);
                            break;
                        }
                    case "sse":
                    case "setupsimulationenv":
                        if (commandArgs.Length < 2)
                        {
                            Console.WriteLine("\nsse requires an argument exchangeName. Optionally a reference file in SetupEnv without extention!");
                            return;
                        }
                        SetupSimEnv(connection, commandArgs[1], commandArgs.Length > 2 ? commandArgs[2] : null);
                        break;
                    default:
                        Console.WriteLine($"\nUnknown command '{commandArgs[0]}'.");
                        break;
                }
            }
            catch (Exception ex)
            {
                WriteError(ex.Message);
            }
        }


        private static void SwitchEnv(string env)
        {
            _switchEnvDelegate?.Invoke(env);
        }

        public static void LoginAs(string userName, string password)
        {
            _switchLoginDelegate?.Invoke(userName, password);
        }

        private static void ExecuteBatch(IConnection connection, RawRabbitConfiguration configuration, string batchFileName, string subFolder = "")
        {
            var commands = GetCommandsFromBatchFile(batchFileName, subFolder);
            foreach (var command in commands)
            {
                if (string.IsNullOrEmpty(command) || command.StartsWith("//") || command.StartsWith("#") || command.StartsWith("-"))
                    continue;
                var firstSpacePos = command.IndexOf(" ", StringComparison.Ordinal);
                var batchCommand = command.Substring(0, firstSpacePos).ToLower().Replace("rabbitcli", "") + command.Substring(firstSpacePos + 1);
                var commandArgs = batchCommand.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                ExecuteCommand(commandArgs, connection, configuration);
            }
        }


        private static void CleanUpEnvironment(IConnection connection)
        {
            var queuesStr = File.ReadAllText(_queueFile);
            var queues = queuesStr.Split(',');

            foreach (var queue in queues.Distinct())
            {
                DeleteQueue(connection, queue);
            }
        }

        private static void DeleteQueue(IConnection connection, string queuename)
        {
            using (var channel = new ModelBuilder(connection).Model)
            {
                try
                {
                    Console.Write($"Deleting queue '{queuename}' ... ");
                    channel.QueueDeclarePassive(queuename);
                    channel.QueueDelete(queuename);
                    Console.WriteLine("Done!");
                }
                catch (Exception)
                {
                    WriteError("Unknown queue!");
                }
            }
        }

        private static void UnbindQueue(IConnection connection, string queuename, string exchangeName, string routingKey, IDictionary<string, object> args = null)
        {
            using (var channel = new ModelBuilder(connection).Model)
            {
                Console.Write($"Unbinding queue '{queuename}' from '{exchangeName}' with routingkey '{routingKey}' ... ");
                try
                {
                    channel.ExchangeDeclarePassive(exchangeName);
                }
                catch (Exception)
                {
                    WriteError($"Unknown exchange: '{exchangeName}'!");
                    return;
                }
                try
                {
                    channel.QueueDeclarePassive(queuename);
                }
                catch (Exception)
                {
                    WriteError($"Unknown queue: '{queuename}'!");
                    return;
                }


                try
                {
                    channel.QueueUnbind(queuename, exchangeName, routingKey, args);
                    Console.WriteLine("Done!");
                }
                catch (Exception ex)
                {
                    WriteError($"Failed!\n{ex.Message}");
                }
            }
        }

        private static void BindQueue(IConnection connection, string queuename, string exchangeName, string routingKey, IDictionary<string, object> args = null)
        {
            using (var channel = new ModelBuilder(connection).Model)
            {
                Console.Write($"Binding queue '{queuename}' to '{exchangeName}' with routingkey '{routingKey}' ... ");
                try
                {
                    channel.ExchangeDeclarePassive(exchangeName);
                }
                catch (Exception)
                {
                    WriteError($"Unknown exchange: '{exchangeName}'!");
                    return;
                }
                try
                {
                    channel.QueueDeclarePassive(queuename);
                }
                catch (Exception)
                {
                    WriteError($"Unknown queue: '{queuename}'!");
                    return;
                }

                try
                {
                    channel.QueueBind(queuename, exchangeName, routingKey, args);
                    Console.WriteLine("Done!");
                }
                catch (Exception ex)
                {
                    WriteError($"Failed!\n{ex.Message}");
                }
            }
        }

        private static void UnbindExchange(IConnection connection, string exchangeNameFrom, string exchangeNameTo, string routingKey, IDictionary<string, object> args = null)
        {
            using (var channel = new ModelBuilder(connection).Model)
            {
                Console.Write($"Unbinding exchange  '{exchangeNameTo}' from '{exchangeNameFrom}' with routingkey '{routingKey}' ... ");
                try
                {
                    channel.ExchangeDeclarePassive(exchangeNameFrom);
                }
                catch (Exception)
                {
                    WriteError($"Unknown exchange: '{exchangeNameFrom}'!");
                    return;
                }
                try
                {
                    channel.ExchangeDeclarePassive(exchangeNameTo);
                }
                catch (Exception)
                {
                    WriteError($"Unknown exchange: '{exchangeNameTo}'!");
                    return;
                }


                try
                {
                    channel.ExchangeUnbind(exchangeNameTo, exchangeNameFrom, routingKey);
                    Console.WriteLine("Done!");
                }
                catch (Exception ex)
                {
                    WriteError($"Failed!\n{ex.Message}");
                }
            }
        }
        private static void ListScenarios()
        {
            string[] scenarioConfigFiles = { };
            try
            {
                scenarioConfigFiles = Directory.GetFiles("Scenarios", "*.json");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Scenarios have to be located in folder 'Scenarios'. Folder 'Scenarios' is missing.");
                WriteError(ex.Message);
            }
            if (scenarioConfigFiles.Length == 0)
            {
                Console.WriteLine("No scenario-files found. Add a file with extention '.json'.");
            }

            foreach (var scenarioFile in scenarioConfigFiles)
            {
                var scenario = Utils.LoadJson<ScenarioConfigFile>(scenarioFile);

                Console.WriteLine($"# {scenarioFile.Replace(".json", "").Replace("Scenarios\\", "")}");
                Console.WriteLine($"  Steps");
                foreach (var step in scenario.Steps)
                {
                    Console.WriteLine($"    - {step}");
                }

                foreach (var emitterConfig in scenario.Emitter)
                {
                    Console.WriteLine($"  Sends to exchange '{emitterConfig.ExchangeName}' :");
                    foreach (var key in emitterConfig.RoutingKeys)
                    {
                        Console.WriteLine($"    - {key}");
                    }
                }
            }
        }

        private static void CleanupModel(IConnection connection, SetupConfig setupConfig)
        {
            using (var modelBuilder = new ModelBuilder(connection))
            {
                // now unbind the Out-Exchanges or the Legacy-Exchanges
                foreach (var exchangeConfig in setupConfig.Exchanges.Where(e =>
                    e.Direction == Enums.Exchange.Direction.Out || e.IsLegacy))
                {
                    modelBuilder.UnbindExchange(exchangeConfig.ExchangeName, exchangeConfig.Binding?.Exchanges);
                }

                // delete all Exchanges 
                foreach (var exchangeConfig in setupConfig.Exchanges.Where(e => !e.IsLegacy))
                {
                    modelBuilder.DeleteExchange(exchangeConfig.ExchangeName);

                    if (exchangeConfig.KeepUnroutedMessages)
                    {
                        var alternateExchangeName = GetAlternateExchangeName(exchangeConfig.ExchangeName);
                        modelBuilder.DeleteExchange(alternateExchangeName);

                        modelBuilder.Model.QueueDelete($"{alternateExchangeName}-UnroutedMessages-Queue");
                    }

                }

            }

        }

        private static void SetupModel(IConnection connection, SetupConfig setupConfig)
        {
            using (var modelBuilder = new ModelBuilder(connection))
            {
                // create all Exchanges 
                foreach (var exchangeConfig in setupConfig.Exchanges)
                {
                    if (exchangeConfig.KeepUnroutedMessages && !exchangeConfig.Arguments.ContainsKey("alternate-exchange"))
                    {
                        var alternateExchangeName = GetAlternateExchangeName(exchangeConfig.ExchangeName);
                        exchangeConfig.Arguments["alternate-exchange"] = alternateExchangeName;
                        var alternateExchangeConfig = new ExchangeConfiguration
                        {
                            Durable = true,
                            ExchangeName = alternateExchangeName,
                            AutoDelete = false,
                            ExchangeType = "Topic"
                        };
                        modelBuilder.CreateExchange(alternateExchangeConfig);
                    }
                    modelBuilder.CreateExchange(exchangeConfig);
                }

                // bind all IN-Exchanges 
                foreach (var exchangeConfig in setupConfig.Exchanges)
                {
                    // create bindings to this In-exchange on the fly
                    if (exchangeConfig.Binding?.Exchanges?.Length > 0)
                    {
                        foreach (var bindingExchange in exchangeConfig.Binding.Exchanges)
                        {
                            if (!CheckBinding(setupConfig, bindingExchange))
                                continue;
                            modelBuilder.BindExchange(exchangeConfig.ExchangeName, new[] { bindingExchange });
                        }
                    }

                    if (exchangeConfig.KeepUnroutedMessages)
                    {
                        var alternateExchangeName = GetAlternateExchangeName(exchangeConfig.ExchangeName);
                        var queueConfig = new QueueConfiguration
                        {
                            QueueName = $"{alternateExchangeName}Messages-Queue",
                            AutoDelete = false,
                            Durable = true
                        };
                        modelBuilder.CreateQueue(queueConfig);
                        modelBuilder.Model.QueueBind(queueConfig.QueueName, alternateExchangeName, "#");
                    }
                    if (exchangeConfig.Direction == Enums.Exchange.Direction.Out && exchangeConfig.Binding?.Queues?.Count() > 0)
                    {
                        WriteError($"Binding queues to the OUT exchange '{exchangeConfig.ExchangeName}' in not allowed!");
                    }
                }
            }
        }

        private static string GetExchangeNameWithoutPostfix(ExchangeModelConfig exchangeConfig)
        {
            var lastIndexPos = exchangeConfig.ExchangeName.LastIndexOf("-", StringComparison.Ordinal);
            var exchangeNameWithoutPostfix = lastIndexPos > 0
                ? exchangeConfig.ExchangeName.Substring(0, lastIndexPos)
                : exchangeConfig.ExchangeName;
            return exchangeNameWithoutPostfix;
        }

        private static string GetAlternateExchangeName(string exchangeName)
        {
            //var lastIndexPos = exchangeName.LastIndexOf("-", StringComparison.Ordinal);
            //var exchangeNameWithoutPostfix = lastIndexPos > 0
            //    ? exchangeName.Substring(0, lastIndexPos)
            //    : exchangeName;
            //var postFix = lastIndexPos > 0 ? exchangeName.Substring(lastIndexPos) : "";
            var alternateExchangeName = $"{exchangeName}-Unrouted";
            return alternateExchangeName;
        }

        private static bool CheckBinding(SetupConfig modelConfig, ExchangeBindingConfiguration bindingExchange)
        {
            var targetExchange = modelConfig.Exchanges.FirstOrDefault(e =>
                e.ExchangeName == bindingExchange.ExchangeName);
            if (targetExchange == null)
            {
                WriteError($"Unknown exchange '{bindingExchange.ExchangeName}'.");
                return false;
            }
            if (targetExchange.Direction == Enums.Exchange.Direction.Out)
            {
                WriteError($"Cannot bind to OUT-exchange '{bindingExchange.ExchangeName}'.");
                return false;
            }
            return true;
        }

        private static void RunScenario(IConnection connection, string scenarioName, string simulationPath)
        {
            ScenarioConfigFile scenario = null;
            try
            {
                scenario = Utils.LoadJson<ScenarioConfigFile>(Path.Combine("Scenarios", scenarioName + ".json"));
            }
            catch (Exception e)
            {
                WriteError(e.Message);
                return;
            }

            if (_lastScenario != null && _lastScenario != scenarioName)
                return;

            _lastScenario = scenarioName;
            _lastSimulation = simulationPath;

            if (scenario == null)
            {
                WriteError($"Unknown scenario '{scenarioName}'!");
                return;
            }
            Console.WriteLine($"Preparing scenario '{scenarioName}'...");
            if (scenario.Emitter == null)
            {
                WriteError("No emitter configured!");
                return;
            }


            var simPath = Utils.GetSimulationFolderName(simulationPath);
            string[] scenarioSimFiles = new string[] { };
            try
            {
                scenarioSimFiles = Directory.GetFiles(simPath, "*.json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Simulations have to be located in folder '{simPath}'. Folder '{simPath}' is missing.");
                WriteError(ex.Message);
            }

            if (scenarioSimFiles.Length == 0)
            {
                Console.WriteLine("No scenario-files found. Add a file with extention '.json'.");
                return;
            }

            try
            {
                using (var modelBuilder = new ModelBuilder(connection))
                {
                    SetupEnvironment(modelBuilder.Model, scenarioSimFiles);

                    foreach (var subscriber in _subscribers)
                    {
                        subscriber.Start();
                    }

                    var success = StartEmitter(modelBuilder.Model, scenario.Emitter);
                    if (!success)
                        return;
                    Thread.Sleep(2000);
                    Console.WriteLine("Hit return when last message was received.");
                    Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                WriteError(ex.Message);
            }
            finally
            {
                Console.Write("Unsubscribing consumers...");
                foreach (var subscriber in _subscribers)
                {
                    subscriber.Stop();
                }
                _subscribers.Clear();
                Console.WriteLine("Done!");
                CleanUpEnvironment(connection);
            }
        }

        private static void SetupEnvironment(IModel channel, IEnumerable<string> scenarioSimFiles)
        {
            var queuesCreated = new List<string>();
            foreach (var simulationFile in scenarioSimFiles)
            {
                var simConfig = Utils.LoadJson<SimulationConfig>(simulationFile);

                Console.WriteLine($"Setting up simulation for '{simConfig.ExchangeName}'... ");

                if (simConfig.ExchangeName == null)
                    throw new ArgumentNullException(nameof(simConfig));

                if (simConfig.Consumer != null)
                {
                    foreach (var consumer in simConfig.Consumer)
                    {
                        if (consumer.RoutingKey == null)
                        {
                            WriteError($"Missing RoutingKey for consumer of exchange '{simConfig.ExchangeName}.'");
                            return;
                        }

                        var routingKey = consumer.RoutingKey;

                        var queueName = ReplacePlaceHolder(consumer.QueueName ?? simConfig.QueueNamePattern,
                            routingKey);

                        var queueBinding = DeclareAndBindQueues(channel, simConfig.ExchangeName, queueName, routingKey);
                        queuesCreated.Add(queueBinding.QueueName);

                        if (consumer.Executable == null ||
                            consumer.Executable.ToLower() == "simulation")
                        {
                            var sim = new SimulationConsumer(channel, $"{simConfig.ExchangeName}",
                                queueBinding.QueueName, consumer.Message);
                            _subscribers.Add(sim);
                        }
                        else if (consumer.Executable != null &&
                                 consumer.Executable.ToLower().Contains(".exe"))
                        {
                            var basePath = _setupConfig?.ExecutableBasePath ?? "";
                            var executable = Path.Combine(basePath, consumer.Executable);
                            if (!File.Exists(executable))
                            {
                                WriteError(
                                    $"Path to '{executable}' not valid. Check environment.variant.executable and setup.ExecutableBasePath.");
                                continue;
                            }

                            var startInfo = new ProcessStartInfo { FileName = executable };
                            try
                            {
                                Process.Start(startInfo);
                            }
                            catch (Exception e)
                            {
                                WriteError(e.Message);
                                continue;
                            }
                        }

                    }
                }
                if (simConfig.Router != null)
                {
                    foreach (var route in simConfig.Router)
                    {
                        var routingKey = route.RoutingKey;

                        var queueName =
                            ReplacePlaceHolder(route.QueueName ?? simConfig.QueueNamePattern, routingKey);

                        var queueBinding =
                            DeclareAndBindQueues(channel, simConfig.ExchangeName, queueName, routingKey);

                        queuesCreated.Add(queueBinding.QueueName);

                        var routingKeyOut = route.To;
                        if (routingKeyOut == null)
                            throw new ConfigurationErrorsException(
                                $"Could not find routingKey 'To' for publishing in env '{simConfig.ExchangeName}'. Check {simulationFile}.");

                        var sim = new SimulationRouter(channel, $"{simConfig.ExchangeName}",
                            queueBinding.QueueName,
                            new ConsumerPublishConfig
                            {
                                ExchangeName = route.ExchangeName,
                                RoutingKey = routingKeyOut
                            });
                        _subscribers.Add(sim);

                    }
                }

            }


            try
            {
                File.WriteAllText(_queueFile, string.Join(",", queuesCreated.Distinct()));
            }
            catch (Exception ex)
            {
                WriteError(ex.Message);
            }
        }




        private static QueueBindingInfo DeclareAndBindQueues(IModel channel, string exchangeName, string queueName, string routingKey, Dictionary<string, object> arguments = null)
        {
            // variable queueName 
            var queueName2Bind = ReplacePlaceHolder(queueName, routingKey) + "-SIM";
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

        private static void DeleteExchange(IConnection connection, string exchangeName)
        {
            using (var modelBuilder = new ModelBuilder(connection))
            {
                try
                {
                    modelBuilder.DeleteExchange(exchangeName);
                }
                catch (Exception ex)
                {
                    WriteError($"Could not delete exchange '{exchangeName}'. {ex.Message}");
                }
            }
        }


        private static string ReplacePlaceHolder(string templateString, string routingKey)
        {
            var routingParts = routingKey.Split('.');

            var replacedString = templateString;
            if (routingParts.Length > 0)
                replacedString = replacedString.Replace("{messageType}", routingParts[0]);
            if (routingParts.Length > 1)
                replacedString = replacedString.Replace("{contentType}", routingParts[1]);
            if (routingParts.Length > 2)
                replacedString = replacedString.Replace("{origin}", routingParts[2]);
            if (routingParts.Length > 3)
                replacedString = replacedString.Replace("{action}", routingParts[3]);
            return replacedString;
        }


        private static bool StartEmitter(IModel channel, EmitterConfig[] scenarioEmitter)
        {
            foreach (var emitter in scenarioEmitter)
            {
                foreach (var routingKey in emitter.RoutingKeys)
                {
                    Console.WriteLine(
                        $"Hit return to start emitting message with routingkey '{routingKey}' or 'x' to stop");
                    var input = Console.ReadLine();
                    if (input != null && input.ToLower() == "x")
                        return false;

                    var simEmit = new SimulationEmitter(channel, emitter.ExchangeName, routingKey);
                    simEmit.Start();
                    Thread.Sleep(2000);
                }
            }

            return true;
        }

        private static void WriteError(string msg)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(msg);
            Console.ForegroundColor = color;
        }

        private static void SetupSimEnv(IConnection connection, string exchange, string referenceFile = null)
        {
            var envConfig = SimulationDialog.CreateConfig(connection, _modelConfig.VHost, _currentEnv, exchange, referenceFile);


            if (envConfig == null)
            {
                Console.WriteLine($"Cancelled creation of environment for '{exchange}'.");
                return;
            }


            var subFolder = Utils.ReadFromConsole("Enter subfolder for config", _currentEnv);

            var fileName = Utils.GetSimulationFileName(subFolder, exchange);
            try
            {
                Utils.SaveJsonToDisk(fileName, envConfig);
                Console.WriteLine($"Saved configuration for '{exchange}' to '{fileName}'.");
            }
            catch (Exception ex)
            {
                WriteError($"Could not save to '{fileName}': {ex.Message}");
                return;
            }

            if (envConfig.Deleted != null && envConfig.Deleted.Any())
            {
                Console.WriteLine("There are queues to be deleted from this exchange.");
                var createBatch = Utils.ReadFromConsole("Do you want to create a batch file? (Y)es | (N)o.", "Y", new[] { "Y", "N" });
                if (createBatch.ToLower() == "n" || createBatch.ToLower() == "x")
                    return;
                var batchFileName = Utils.ReadFromConsole("Enter a filename for the batchfile", $"DeleteQueues{exchange}");
                if (string.IsNullOrEmpty(batchFileName) || batchFileName.ToLower() == "x")
                    return;

                var batchFileFullName = Utils.GetBatchFileName(batchFileName);
                var batches = CreateBatchCommands("dq", envConfig.Deleted);
                try
                {
                    var batchContent = string.Join("\n", batches);
                    Utils.SaveToDisk(batchFileFullName, batchContent);
                    Console.WriteLine($"Saved batch for '{exchange}' to '{batchFileFullName}'.");
                }
                catch (Exception ex)
                {
                    WriteError($"Could not save to '{batchFileFullName}': {ex.Message}");
                }
            }

            if (envConfig.Unbind != null && envConfig.Unbind.Any())
            {
                Console.WriteLine("There are queues to be unbound from this exchange.");
                var createBatch = Utils.ReadFromConsole("Do you want to create a batch file? (Y)es | (N)o.", "Y", new[] { "Y", "N" });
                if (createBatch.ToLower() == "n" || createBatch.ToLower() == "x")
                    return;
                var batchFileName = Utils.ReadFromConsole("Enter a filename for the batchfile", $"UnbindQueues{exchange}");
                if (string.IsNullOrEmpty(batchFileName) || batchFileName.ToLower() == "x")
                    return;

                var batchFileFullName = Utils.GetBatchFileName(batchFileName);
                var batches = CreateBatchCommands("uq", envConfig.Deleted);
                try
                {
                    Utils.SaveToDisk(batchFileFullName, string.Join("\n", batches));
                    Console.WriteLine($"Saved batch for '{exchange}' to '{batchFileFullName}'.");
                }
                catch (Exception ex)
                {
                    WriteError($"Could not save to '{batchFileFullName}': {ex.Message}");
                }
            }
        }

        private static IEnumerable<string> CreateBatchCommands(string command, IEnumerable<PublishElement> elements)
        {
            return elements.Select(publishElement => $"rabbitCli {command} {publishElement.QueueName}");
        }

        private static void CreateUser(RawRabbitConfiguration configuration, string userName, string password, params string[] tags)
        {
            using (var rabbitClient = new ManagementClient(configuration.Hostnames.First(), configuration.Username, configuration.Password))
            {
                User user = null;
                try
                {
                    user = rabbitClient.GetUserAsync(userName).Result;
                    Console.WriteLine($"User '{userName}' exists. Changing password.");
                }
                catch
                {
                    // nothing
                }

                try
                {
                    var userInfo = new UserInfo(userName, password);
                    foreach (var tag in tags)
                    {
                        userInfo.AddTag(tag.ToLower());
                    }
                    rabbitClient.CreateUserAsync(userInfo).Wait();
                    var updateText = user != null ? "updated" : "created";
                    Console.WriteLine($"User '{userName}' {updateText}.");
                }
                catch (Exception ex)
                {
                    WriteError($"Could not create user '{userName}'. " + ex.Message);
                }
            }
        }

        private static void DeleteUser(RawRabbitConfiguration configuration, string userName)
        {
            using (var rabbitClient = new ManagementClient(configuration.Hostnames.First(), configuration.Username, configuration.Password))
            {
                User user = null;
                try
                {
                    user = rabbitClient.GetUserAsync(userName).Result;
                }
                catch
                {
                    Console.WriteLine($"User '{userName}' does not exists. Verify name.");
                }

                try
                {
                    rabbitClient.DeleteUserAsync(user).Wait();
                    Console.WriteLine($"User '{userName}' deleted.");
                }
                catch (Exception ex)
                {
                    WriteError($"Could not delete user '{userName}'. " + ex.Message);
                }
            }
        }


        private static void MoveMessages(IConnection connection, string queueName, string exchangeName, string routingKey)
        {
            using (var channel = new ModelBuilder(connection).Model)
            {

                try
                {
                    channel.ExchangeDeclarePassive(exchangeName);
                }
                catch (Exception)
                {
                    WriteError($"Unknown exchange: '{exchangeName}'!");
                    return;
                }
                try
                {
                    channel.QueueDeclarePassive(queueName);
                }
                catch (Exception)
                {
                    WriteError($"Unknown queue: '{queueName}'!");
                    return;
                }

                string subscription = null;
                try
                {
                    Console.WriteLine($"Moving messages from queue '{queueName}' ... ");
                    var messageCount = 0;
                    var lastMessageCount = 0;
                    var isRunning = true;

                    var consumer = new EventingBasicConsumer(channel);
                    object locker = new object();

                    consumer.Received += (sender, e) =>
                    {
                        if (routingKey != null && !RoutingkeyMatches(routingKey, e.RoutingKey))
                            return;

                        lock (locker)
                        {
                            messageCount++;
                        }

                        channel.BasicPublish(exchangeName, e.RoutingKey, e.BasicProperties, e.Body);
                        Console.Write(".");
                    };


                    var timer = new Timer
                    {
                        Interval = 5,
                        AutoReset = true,
                        Enabled = false
                    };

                    timer.Elapsed += (sender, e) =>
                    {
                        lock (locker)
                        {
                            if (lastMessageCount != messageCount)
                            {
                                lastMessageCount = messageCount;
                                return;
                            }
                        }

                        timer.Stop();
                        channel.BasicCancel(subscription);
                        isRunning = false;
                        Console.WriteLine("Done!");

                    };

                    subscription = channel.BasicConsume(queueName, true, consumer);
                    timer.Start();

                    while (isRunning)
                    {
                        // wait
                        Thread.Sleep(1000);
                    }

                    Console.WriteLine($"Moved {messageCount} messages!");
                }
                catch (Exception ex)
                {
                    WriteError("Error moving messages: " + ex.Message);
                    if (subscription != null)
                    {
                        channel.BasicCancel(subscription);
                    }
                }
            }
        }

        internal static bool RoutingkeyMatches(string routingKeyFilter, string routingKey)
        {
            var filterParts = routingKeyFilter.Split('.');
            var originalParts = routingKey.Split('.');

            for (var i = 0; i < filterParts.Length; i++)
            {
                var filterPart = filterParts[i];
                if (filterPart == "#")
                    return true;
                if (originalParts.Length <= i)
                    return false;
                if (filterPart == "*")
                    continue;

                var originalPart = originalParts[i];
                if (filterPart != originalPart)
                    return false;
            }

            return true;
        }

        private static void SetUserPermissions(RawRabbitConfiguration configuration, string userName, string permConfig, string permWrite, string permRead)
        {
            try
            {
                using (var rabbitClient = new ManagementClient(configuration.Hostnames.First(), configuration.Username, configuration.Password))
                {
                    User user;
                    try
                    {
                        user = rabbitClient.GetUserAsync(userName).Result;
                    }
                    catch
                    {
                        WriteError($"Error: User '{userName}' could not be found. Check spelling.");
                        return;
                    }


                    var vHost = rabbitClient.GetVhostAsync(configuration.VirtualHost).Result;
                    var perm = new PermissionInfo(user, vHost);

                    perm.SetConfigure(permConfig);
                    perm.SetRead(permRead);
                    perm.SetWrite(permWrite);

                    rabbitClient.CreatePermissionAsync(perm).Wait();
                    Console.WriteLine($"User permissions for '{userName}' set successfully.");
                }

            }
            catch (Exception ex)
            {
                WriteError($"Could not set user permissions for '{userName}'. " + ex.Message);
            }
        }
    }
}
