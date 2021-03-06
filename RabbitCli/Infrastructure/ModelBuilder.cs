#region Using

using System;
using System.Collections.Generic;
using RabbitMQ.Client;
using RawRabbit;
using RawRabbit.Configuration.Exchange;
using RawRabbit.Configuration.Queue;

#endregion

namespace RabbitCli.Infrastructure
{

    /// <summary>
    /// A simple, hard-wired factory to create <see cref="IBusClient{TMessageContext}"/>s.
    /// </summary>
    public class ModelBuilder : IDisposable
    {
        private readonly IModel _model;
        private readonly List<ISimulationEmitter> _simulationPublishers;
        private readonly List<ISimulationConsumer> _simulationConsumers;


        /// <summary>
        /// Builds a model according to the setup.json file
        /// </summary>
        /// <remarks>
        /// This component replaces RawRabbit.vNext.BusClientFactory in order to avoid heavy dependency "pollution", but at
        /// the cost of inflexible, hard-wired setup.
        /// </remarks>
        /// <param name="connection"></param>
        /// <returns>The bus client.</returns>
        public ModelBuilder(IConnection connection)
        {
            _model = connection.CreateModel();
            _simulationPublishers = new List<ISimulationEmitter>();
            _simulationConsumers = new List<ISimulationConsumer>();
        }

        public IModel Model => _model;

        public ModelBuilder CreateExchange(ExchangeConfiguration exchangeConfig)
        {
            try
            {
                Console.Write($"Creating exchange '{exchangeConfig.ExchangeName}'... ");

                _model.ExchangeDeclare(exchangeConfig.ExchangeName, exchangeConfig.ExchangeType.ToLower(), exchangeConfig.Durable, exchangeConfig.AutoDelete, exchangeConfig.Arguments);
                Console.WriteLine("Done!");
                return this;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed!\n{ex.Message}");
                throw;
            }
        }
        public ModelBuilder DeleteExchange(string exchangeName)
        {
            Console.Write($"Deleting exchange '{exchangeName}'... ");
            try
            {
                _model.ExchangeDeclarePassive(exchangeName);
            }
            catch (Exception)
            {
                Console.WriteLine("Unknown exchange!");
                return this;
            }
            _model.ExchangeDelete(exchangeName);
            Console.WriteLine("Done!");
            return this;

        }

        public ModelBuilder CreateQueue(QueueConfiguration queueConfig)
        {
            try
            {
                Console.Write($"Creating queue '{queueConfig.QueueName}'...");

                _model.QueueDeclare(queueConfig.QueueName, queueConfig.Durable, false, queueConfig.AutoDelete, queueConfig.Arguments);

                Console.WriteLine(" Done!");
                return this;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        public ModelBuilder BindExchange(string exchangeName, IEnumerable<ExchangeBindingConfiguration> bindings)
        {
            if (bindings == null)
                return this;

            try
            {
                foreach (var binding in bindings)
                {
                    if (binding.ExchangeName == null)
                        continue;

                    if (binding.RoutingKeys != null)
                    {
                        foreach (var routingKey in binding.RoutingKeys)
                        {
                            Console.Write(
                                $"Add binding from exchange '{exchangeName}' to exchange '{binding.ExchangeName}' with routingKey '{routingKey}' ...");
                            _model.ExchangeBind(binding.ExchangeName, exchangeName, routingKey, binding.Arguments);
                            Console.WriteLine(" Done!");

                        }
                    }
                    else if (binding.Arguments != null)
                    {
                        Console.Write(
                            $"Add binding from exchange '{exchangeName}' to exchange '{binding.ExchangeName}' with arguments: ");
                        foreach (var argument in binding.Arguments)
                        {
                            Console.Write($"'{argument.Key} : {argument.Value}'; ");
                            _model.ExchangeBind(binding.ExchangeName, exchangeName, "", binding.Arguments);
                        }
                        Console.WriteLine("Done!");
                    }
                    else 
                    {
                        WriteError($"Can not add binding from exchange '{exchangeName}' to exchange '{binding.ExchangeName}' without routingkey and arguments! ");
                    }
                }
                return this;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        public ModelBuilder UnbindExchange(string exchangeName, ExchangeBindingConfiguration[] bindings)
        {
            if (bindings == null)
                return this;
            try
            {
                foreach (var binding in bindings)
                {
                    if (binding.ExchangeName == null)
                        continue;

                    if (binding.RoutingKeys != null)
                    {
                        foreach (var routingKey in binding.RoutingKeys)
                        {
                            Console.Write(
                                $"Removing binding from exchange '{exchangeName}' to exchange '{binding.ExchangeName}' with routingKey '{routingKey}' ...");
                            _model.ExchangeUnbind(binding.ExchangeName, exchangeName, routingKey, binding.Arguments);
                            Console.WriteLine(" Done!");

                        }
                    }
                    else if (binding.Arguments != null)
                    {
                        Console.Write(
                            $"Remove binding from exchange '{exchangeName}' to exchange '{binding.ExchangeName}' with arguments: ");
                        foreach (var argument in binding.Arguments)
                        {
                            Console.Write($"'{argument.Key} : {argument.Value}'; ");
                            _model.ExchangeUnbind(binding.ExchangeName, exchangeName, "", binding.Arguments);
                        }
                        Console.WriteLine("Done!");
                    }
                }
                return this;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        public void Dispose()
        {

            foreach (var consumer in _simulationConsumers)
            {
                consumer.Stop();
            }

            _model?.Close();
        }

        public void AddConsumer(ISimulationConsumer consumer)
        {
            _simulationConsumers.Add(consumer);
        }


        public void AddPublisher(ISimulationEmitter simulationEmitter)
        {
            _simulationPublishers.Add(simulationEmitter);
        }

        public void StartSimulation()
        {
            foreach (var consumer in _simulationConsumers)
            {
                consumer.Start();
            }

            foreach (var publisher in _simulationPublishers)
            {
                publisher.Start();
            }
        }
        private static void WriteError(string msg)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(msg);
            Console.ForegroundColor = color;
        }
    }
}