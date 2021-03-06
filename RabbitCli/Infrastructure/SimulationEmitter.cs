﻿using System;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace RabbitCli.Infrastructure
{
    public class SimulationEmitter : ISimulationEmitter
    {
        private readonly IModel _model;
        private readonly string _exchange;
        private readonly string _routingKey;

        public SimulationEmitter(IModel model, PublisherConfig publisher) :
            this(model, publisher.ExchangeName, publisher.RoutingKey)
        {
        }

        public SimulationEmitter(IModel model, string exchange, string routingKey)
        {
            _model = model;
            _exchange = exchange;
            _routingKey = routingKey;
            Console.WriteLine($"Created emitter to exchange '{exchange}' with routingkey '{routingKey}'.");

        }

        public void Start()
        {
            var message = new BaseMessage
            {
                Message = $"{_exchange}-{_routingKey}",
            };
            var jsonMsg = JsonConvert.SerializeObject(message);
            var body = Encoding.UTF8.GetBytes(jsonMsg);
            _model.BasicPublish(exchange: _exchange,
                routingKey: _routingKey,
                basicProperties: null,
                body: body);
            Console.WriteLine($"Sent message: {jsonMsg}");
        }
    }
}