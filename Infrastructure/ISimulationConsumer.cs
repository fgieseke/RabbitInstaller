using RabbitMQ.Client.Events;

namespace RabbitInstaller.Infrastructure
{
    public interface ISimulationConsumer
    {
        EventingBasicConsumer Consumer { get; }

        void Start();
        void Stop();
    }
}