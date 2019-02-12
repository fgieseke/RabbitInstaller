using RabbitMQ.Client;
using RawRabbit.Configuration;

namespace RabbitCli.Infrastructure
{
    public class RabbitConnector : RawRabbitConfiguration
    {
        private bool _isConnected;
        private string _env;

        public new string Password
        {
            get => base.Password;
            set
            {
                base.Password = value;
                _isConnected = false;
            }
        }

        public new string Username
        {
            get => base.Username;
            set
            {
                base.Username = value;
                _isConnected = false;
            }
        }

        public new string VirtualHost
        {
            get => base.VirtualHost;
            set
            {
                base.VirtualHost = value;
                _isConnected = false;
            }
        }

        public string Env
        {
            get => _env;
            set
            {
                _env = value;
                _isConnected = false;
            }
        }

        public bool IsConnected => _isConnected;

        public IConnection Connect()
        {
            _isConnected = true;
            return BusClientFactory.CreateConnection(this);
        }

    }
}
