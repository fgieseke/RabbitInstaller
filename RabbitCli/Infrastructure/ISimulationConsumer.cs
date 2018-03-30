using RabbitMQ.Client.Events;

namespace RabbitCli.Infrastructure
{
    public interface ISimulationConsumer
    {
        EventingBasicConsumer Consumer { get; }

        void Start();
        void Stop();
    }
}