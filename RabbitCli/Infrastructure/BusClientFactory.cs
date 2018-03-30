#region Using

using System.Linq;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RawRabbit;
using RawRabbit.Channel;
using RawRabbit.Common;
using RawRabbit.Configuration;
using RawRabbit.Consumer.Eventing;
using RawRabbit.Context;
using RawRabbit.Context.Enhancer;
using RawRabbit.Context.Provider;
using RawRabbit.ErrorHandling;
using RawRabbit.Operations;
using RawRabbit.Operations.Abstraction;

#endregion

namespace RabbitCli.Infrastructure
{

    /// <summary>
    /// A simple, hard-wired factory to create <see cref="IBusClient{TMessageContext}"/>s.
    /// </summary>
    public class BusClientFactory
    {
        /// <summary>
        /// Creates a default <see cref="IBusClient{TMessageContext}"/> according to config.
        /// </summary>
        /// <remarks>
        /// This component replaces RawRabbit.vNext.BusClientFactory in order to avoid heavy dependency "pollution", but at
        /// the cost of inflexible, hard-wired setup.
        /// </remarks>
        /// <typeparam name="TMessageContext">The type of the context.</typeparam>
        /// <param name="config">The raw rabbit configuration.</param>
        /// <returns>The bus client.</returns>
        public static IBusClient<TMessageContext> CreateDefault<TMessageContext>(RawRabbitConfiguration config)
            where TMessageContext : MessageContext
        {
            var namingConventions = new NamingConventions();
            var configurationEvaluator = new ConfigurationEvaluator(config, namingConventions);
            var connectionFactory = CreateConnectionFactory(config);


            var channelConfig = new ChannelFactoryConfiguration();
            var channelFactory = new ChannelFactory(connectionFactory, config, channelConfig);
            var consumerFactory = new EventingBasicConsumerFactory();
            var topologyProvider = new TopologyProvider(channelFactory);
            var jsonSerializer = new JsonSerializer();
            var serializer = new MessageSerializer(jsonSerializer);
            IMessageContextProvider<TMessageContext> contextProvider =
                new MessageContextProvider<TMessageContext>(jsonSerializer);

            var contextEnhancer = new ContextEnhancer(channelFactory, namingConventions);
            var propertiesProvider = new BasicPropertiesProvider(config);
            var errorHandling = new DefaultStrategy(
                serializer, 
                namingConventions, 
                propertiesProvider, 
                topologyProvider, 
                channelFactory);
            ISubscriber<TMessageContext> subscriber = new Subscriber<TMessageContext>(
                channelFactory, 
                consumerFactory, 
                topologyProvider, 
                serializer, 
                contextProvider, 
                contextEnhancer, 
                errorHandling, 
                config);

            var acknowledger = new NoAckAcknowledger();
            var publisher = new Publisher<TMessageContext>(
                channelFactory, 
                topologyProvider, 
                serializer, 
                contextProvider, 
                acknowledger, 
                propertiesProvider, 
                config);
            var responder = new Responder<TMessageContext>(
                channelFactory, 
                topologyProvider, 
                consumerFactory, 
                serializer, 
                contextProvider, 
                contextEnhancer, 
                propertiesProvider, 
                errorHandling, 
                config);
            var requester = new Requester<TMessageContext>(
                channelFactory, 
                consumerFactory, 
                serializer, 
                contextProvider, 
                errorHandling, 
                propertiesProvider, 
                topologyProvider, 
                config);
            var client = new BaseBusClient<TMessageContext>(
                configurationEvaluator, 
                subscriber, 
                publisher, 
                responder, 
                requester);
            return client;
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