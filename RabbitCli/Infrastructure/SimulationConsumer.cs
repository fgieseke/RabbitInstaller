using System;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitCli.Infrastructure
{
    public class SimulationConsumer : ISimulationConsumer
    {
        private readonly IModel _model;
        private readonly string _name;
        private readonly string _queueName;
        private readonly ConsumerPublishConfig _consumerPublish;

        public SimulationConsumer(IModel model, string name, string queueName, ConsumerPublishConfig consumerPublish = null)
        {
            _model = model;
            _name = name;
            _queueName = queueName;
            _consumerPublish = consumerPublish;

            Consumer = new EventingBasicConsumer(_model);
            Console.WriteLine($"Created consumer '{_name}' on '{queueName}'.");

        }

        private void Consume(object sender, BasicDeliverEventArgs e)
        {
            var body = e.Body;
            var message = Encoding.UTF8.GetString(body);
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{_name}] Received message from exchange '{e.Exchange}' with routingkey '{e.RoutingKey}' : {message}.");
            Console.ForegroundColor = color;

            if (_consumerPublish != null)
            {
                var newRoutingKey = TransformRoutingKey(e.RoutingKey, _consumerPublish.RoutingKey);
                _model.BasicPublish(exchange: _consumerPublish.ExchangeName,
                    routingKey: newRoutingKey,
                    basicProperties: e.BasicProperties,
                    body: body);
                var fcolor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"[{_name}] Routed message to '{_consumerPublish.ExchangeName}' with key: {newRoutingKey}");
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
            return string.Join(".",  newKeyParts);
        }

        public EventingBasicConsumer Consumer { get; }

        public void Start()
        {
            Consumer.Received += Consume;
            _model.BasicConsume(_queueName, autoAck: true, consumer: Consumer);
        }

        public void Stop()
        {
            Consumer.Received -= Consume;
        }

    }
}