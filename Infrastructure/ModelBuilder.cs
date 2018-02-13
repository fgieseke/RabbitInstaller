#region Using

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RawRabbit;
using RawRabbit.Common;
using RawRabbit.Configuration;
using RawRabbit.Configuration.Exchange;
using RawRabbit.Configuration.Queue;

#endregion

namespace RabbitInstaller.Infrastructure
{

    /// <summary>
    /// A simple, hard-wired factory to create <see cref="IBusClient{TMessageContext}"/>s.
    /// </summary>
    public class ModelBuilder : IDisposable
    {
        private readonly IModel _model;
        private readonly List<ISimulationPublisher> _simulationPublishers;
        private readonly List<ISimulationConsumer> _simulationConsumers;

        /// <summary>
        /// Builds a modlel according to the setup.json file
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
            _simulationPublishers = new List<ISimulationPublisher>();
            _simulationConsumers = new List<ISimulationConsumer>();
        }

        public IModel Model => _model;

        public ModelBuilder CreateExchange(ExchangeConfiguration exchangeConfig)
        {
            try
            {
                Console.Write($"Creating exchange '{exchangeConfig.ExchangeName}'...");

                _model.ExchangeDeclare(exchangeConfig.ExchangeName, exchangeConfig.ExchangeType.ToLower(), exchangeConfig.Durable, exchangeConfig.AutoDelete, exchangeConfig.Arguments);

                Console.WriteLine(" Done!");
                return this;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
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

        public ModelBuilder BindExchange(string exchangeName, IEnumerable<BindingModelConfig> bindings)
        {
            try
            {
                foreach (var binding in bindings)
                {
                    foreach (var routingKey in binding.RoutingKeys)
                    {
                        Console.Write($"Bind routingKey '{routingKey}' to ");

                        if (binding.QueueName != null)
                        {
                            Console.Write($"queue '{binding.QueueName}' on exchange '{exchangeName}' ...");
                            _model.QueueBind(binding.QueueName, exchangeName, routingKey, binding.Arguments);
                        }
                        if (binding.ExchangeName != null)
                        {
                            Console.Write($"exchange '{binding.ExchangeName}' on exchange '{exchangeName}' ...");
                            _model.ExchangeBind(binding.ExchangeName, exchangeName, routingKey, binding.Arguments);
                        }
                        Console.WriteLine(" Done!");
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

            foreach (var publisher in _simulationPublishers)
            {
                publisher.Stop();
            }

            _model?.Close();
        }

        public void AddConsumer(ISimulationConsumer consumer)
        {
            _simulationConsumers.Add(consumer);
        }


        public void AddPublisher(ISimulationPublisher simulationPublisher)
        {
            _simulationPublishers.Add(simulationPublisher);
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

    }
}