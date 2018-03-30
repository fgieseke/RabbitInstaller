using System;
using System.Text;
using log4net;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SampleService
{
    public class SimulationConsumer
    {
        private readonly ILog _logger;
        private readonly IModel _model;
        private readonly string _name;
        private readonly string _queueName;
        private readonly string _publishToExchange;
        private readonly string _publishToRoutingKey;

        public SimulationConsumer(ILog logger, IModel model, string name, string queueName, string publishToExchange = null, string publishToRoutingKey=null)
        {
            _logger = logger;
            _model = model;
            _name = name;
            _queueName = queueName;
            _publishToExchange = publishToExchange;
            _publishToRoutingKey = publishToRoutingKey;

            Consumer = new EventingBasicConsumer(_model);
            _logger.Debug($"Created consumer '{_name}' on '{queueName}'.");

        }

        private void Consume(object sender, BasicDeliverEventArgs e)
        {
            var body = e.Body;
            var message = Encoding.UTF8.GetString(body);
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            _logger.Debug($"[{_name}] Received message from exchange '{e.Exchange}' with routingkey '{e.RoutingKey}' : {message}.");
            Console.ForegroundColor = color;

            if (_publishToExchange != null)
            {
                var newRoutingKey = TransformRoutingKey(e.RoutingKey, _publishToRoutingKey);
                _model.BasicPublish(exchange: _publishToExchange,
                    routingKey: newRoutingKey,
                    basicProperties: e.BasicProperties,
                    body: body);
                var fcolor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Blue;
                _logger.Debug($"[{_name}] Routed message to '{_publishToExchange}' with key: {newRoutingKey}");
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