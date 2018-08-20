using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabbitCli.Infrastructure
{
    public class SimulationDialog
    {
        public static SimulationConfig CreateConfig(IConnection connection, string vHost, string env, string exchange, string referenceFile = null)
        {
            var fileName = Utils.GetSimulationFileName(env, exchange);
            var envConfig = Utils.LoadJson<SimulationConfig>(fileName, silent:true) ?? new SimulationConfig();
            var exists = envConfig.ExchangeName != null;


            if (exists)
            {
                var overWrite = Utils.ReadFromConsole($"Environment for '{exchange}' already exists. Overwrite (Y)es (N)o?", "N", new[] { "Y", "N" });
                if (overWrite.ToLower() == "n")
                    throw new Exception("User cancelled.");

                envConfig = new SimulationConfig();
            }

            if (referenceFile != null)
                return UpdateByReference(connection, vHost, exchange, referenceFile);

            Console.WriteLine($"Creating environment for '{exchange}'.");
            try
            {
                var defaultVal = $"{exchange}-In";
                envConfig.ExchangeName = Utils.ReadFromConsole("Exchangename", defaultVal);

                var createNew = true;
                var consumerList = new List<ConsumerElement>();
                var routerList = new List<RouterElement>();
                do
                {
                    var actorType = Utils.ReadFromConsole("ActorType: (C)onsumer, (R)outer", null, new[] { "C", "R" });
                    if (actorType.ToUpper() == "C")
                    {
                        var consumer = new ConsumerElement
                        {
                            Executable = "simulation",
                            QueueName = Utils.ReadFromConsole("Bind queue (will be postfixed -Queue)"),
                            RoutingKey = Utils.ReadFromConsole("... with routingkey. PlaceHolder * and # are allowed."),
                            Message = Utils.ReadFromConsole("Message", "")
                        };
                        consumerList.Add(consumer);
                    }
                    else if (actorType.ToUpper() == "R")
                    {
                        var router = new RouterElement
                        {
                            Executable = "simulation",
                            QueueName = Utils.ReadFromConsole("Bind queue (will be postfixed -Queue)"),
                            RoutingKey = Utils.ReadFromConsole("... with routingkey. PlaceHolder * and # are allowed."),
                            ExchangeName = Utils.ReadFromConsole("Route to exchange", $"{exchange}-Out"),
                            To = Utils.ReadFromConsole("...with routingkey. PlaceHolder {origin} and * are allowed.")
                        };
                        routerList.Add(router);
                    }

                    createNew = Utils.ReadFromConsole($"Create another actor? (Y)es (N)o?", "Y", new[] { "Y", "N" }).ToLower() == "y";
                }
                while (createNew);

                envConfig.Consumer = consumerList.ToArray();
                envConfig.Router = routerList.ToArray();
            }
            catch (Exception)
            {
                return null;
            }

            return envConfig;
        }

        private static SimulationConfig UpdateByReference(IConnection connection, string vHost, string exchange, string referenceFile)
        {
            var envConfig = new SimulationConfig();
            var fullFile = Utils.GetExportFile(referenceFile);
            Console.WriteLine($"Updating environment for '{exchange}' from '{fullFile}'.");
            var refConfigAll = Utils.LoadJson<ReferenceConfig>(fullFile);
            if (refConfigAll == null)
                return null;

            Console.WriteLine("Configuring bindings for exchange...");
            var exchangeName = Utils.ReadFromConsole("ExchangeName", $"{exchange}-In");
            var consumerList = new List<ConsumerElement>();
            var routerList = new List<RouterElement>();
            var ignoreList = new List<PublishElement>();
            var deleteList = new List<PublishElement>();
            var unbindList = new List<PublishElement>();

            var virtualHostInConfig = vHost == "/" ? null : vHost;

            var bindings = refConfigAll.Bindings.Where(b =>
                (b.VHost == virtualHostInConfig || b.VHost == vHost) && b.DestinationType?.ToLower() == "queue" && b.ExchangeName == exchangeName)
                .OrderBy(b => b.RoutingKey);

            string lastRoutingKey = null;
            string lastExchange = null;
            foreach (var binding in bindings)
            {
                Console.WriteLine($"Define ActorType for queue '{binding.QueueName}' and routing key '{binding.RoutingKey}'");
                var actorType = Utils.ReadFromConsole("ActorType: (C)onsumer, (R)outer, (I)gnore, (D)elete, (U)nbind",
                        null, new[] {"C", "R", "I", "D", "U"});

                var publishConfig = new PublishElement
                {
                    QueueName = binding.QueueName,
                    RoutingKey = binding.RoutingKey
                };

                if (actorType.ToUpper() == "C")
                {
                    var consumer = new ConsumerElement
                    {
                        Executable = "simulation",
                        QueueName = binding.QueueName,
                        RoutingKey = binding.RoutingKey,
                        Message = Utils.ReadFromConsole("Message. (Describes the action taken.)", "")
                    };
                    consumerList.Add(consumer);
                }
                else if (actorType.ToUpper() == "R")
                {
                    var router = new RouterElement
                    {
                        Executable = "simulation",
                        QueueName = binding.QueueName,
                        RoutingKey = binding.RoutingKey,
                        Message = Utils.ReadFromConsole("Message. (Describes the action taken.)", ""),
                        ExchangeName = Utils.ReadFromConsole("Route to exchange", lastExchange ?? $"{exchange}-Out"),
                        To = Utils.ReadFromConsole("...with routingkey. (Placeholder * is allowed.)", lastRoutingKey)
                    };

                    lastRoutingKey = router.To;
                    lastExchange = router.ExchangeName;
                    routerList.Add(router);
                }
                else if (actorType.ToUpper() == "I")
                {
                    ignoreList.Add(publishConfig);
                }
                else if (actorType.ToUpper() == "D")
                {
                    deleteList.Add(publishConfig);
                }
                else if (actorType.ToUpper() == "U")
                {
                    unbindList.Add(publishConfig);
                }
            }

            envConfig.ExchangeName = exchangeName;
            envConfig.Consumer = consumerList.ToArray();
            envConfig.Router = routerList.ToArray();
            envConfig.Ignored = ignoreList.ToArray();
            envConfig.Deleted = deleteList.ToArray();
            envConfig.Unbind = unbindList.ToArray();
            return envConfig;
        }
    }
}