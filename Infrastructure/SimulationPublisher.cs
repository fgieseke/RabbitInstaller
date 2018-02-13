using System;
using System.Text;
using System.Timers;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace RabbitInstaller.Infrastructure
{
    public class SimulationPublisher : ISimulationPublisher
    {
        private readonly IModel _model;
        private readonly string _name;
        private readonly int _messageCount;
        private readonly Timer _timer;
        private int _counter;

        public SimulationPublisher(IModel model, PublisherConfig publisher) :
            this(model, publisher.Name, publisher.ExchangeName, publisher.RoutingKey, publisher.MessageCount, publisher.Period)
        {
        }

        public SimulationPublisher(IModel model, string name, string exchange, string routingKey, int messageCount = 10, int periodInMs = 1000)
        {
            _model = model;
            _name = name;
            _messageCount = messageCount;
            _timer = new Timer(periodInMs);
            _counter = 0;
            _timer.Elapsed += (sender, args) => Publish(exchange, routingKey);
            Console.WriteLine($"Created publisher '{_name}' on '{exchange}'.");

        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void Publish(string exchangeName, string routingKey) 
        {
            if (_counter == _messageCount)
            {
                Stop();
                return;
            }

            var message = new BaseMessage
            {
                Id = _counter.ToString(),
                Message = $"{exchangeName}-{routingKey}",
                //Created = DateTime.Now.ToUniversalTime()
            };
            _counter++;
            var jsonMsg = JsonConvert.SerializeObject(message);
            var body = Encoding.UTF8.GetBytes(jsonMsg);
            _model.BasicPublish(exchange: exchangeName,
                routingKey: routingKey,
                basicProperties: null,
                body: body);
            Console.WriteLine($"[{_name}] Sent message: {jsonMsg}");
        }
    }
}