using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitCli.Infrastructure
{
    public class SimulationConsumer : ISimulationConsumer
    {
        protected readonly IModel Model;
        protected readonly string Name;
        private readonly string _queueName;
        private readonly string _message;

        public SimulationConsumer(IModel model, string name, string queueName, string message = null, bool isConsumer = true)
        {
            Model = model;
            Name = name;
            _queueName = queueName;
            _message = message;

            Consumer = new EventingBasicConsumer(Model);
            if (isConsumer)
            {
                Console.WriteLine($"Created consumer '{Name}' on '{queueName}'.");
            }

        }

        protected virtual void Consume(object sender, BasicDeliverEventArgs e)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{Name}] Received message from exchange '{e.Exchange}' with routingkey '{e.RoutingKey}'.");
            if (_message != null)
                Console.WriteLine($"[{Name}] {_message}");
            Console.ForegroundColor = color;
        }

        public EventingBasicConsumer Consumer { get; }

        public void Start()
        {
            Consumer.Received += Consume;
            Model.BasicConsume(_queueName, true, consumer: Consumer);
        }

        public void Stop()
        {
            Consumer.Received -= Consume;
        }

    }

    public class SimulationRouter : SimulationConsumer
    {
        private readonly ConsumerPublishConfig _consumerPublish;

        public SimulationRouter(IModel model, string name, string queueName, ConsumerPublishConfig consumerPublish, string message = null)
            : base(model, name, queueName, message, false)
        {
            _consumerPublish = consumerPublish;
            Console.WriteLine($"Created router '{Name}' on '{queueName}'.");
        }

        protected override void Consume(object sender, BasicDeliverEventArgs e)
        {
            base.Consume(sender, e);


            if (_consumerPublish != null)
            {
                var newRoutingKey = TransformRoutingKey(e.RoutingKey, _consumerPublish.RoutingKey);
                Model.BasicPublish(exchange: _consumerPublish.ExchangeName,
                    routingKey: newRoutingKey,
                    basicProperties: e.BasicProperties,
                    body: e.Body);

                var fcolor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[{Name}] Routed message to '{_consumerPublish.ExchangeName}' with key: {newRoutingKey}");
                Console.ForegroundColor = fcolor;
            }

        }

        private string TransformRoutingKey(string originalRoutingKey, string publishRoutingKey)
        {
            var parts = originalRoutingKey.Split('.');
            var newParts = publishRoutingKey.Split('.');
            var newKeyParts = new System.Collections.Generic.List<string>();

            for (var i = 0; i < newParts.Length; i++)
            {
                newKeyParts.Add(newParts[i] == "*" ? parts[i] : newParts[i]);
            }
            return string.Join(".", newKeyParts);
        }


    }
}
