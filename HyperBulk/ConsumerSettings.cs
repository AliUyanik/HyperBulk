using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperBulk
{
    public class ConsumerSettings
    {
        public RabbitMQSettings RabbitMQSettings { get; set; }
        public AppSettings AppSettings { get; set; }
        public RetrySettings RetrySettings { get; set; }
    }

    public class RabbitMQSettings
    {
        public string QueueName { get; set; }
        public string HostName { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public int HeartBeat { get; set; }
        public bool Exclusive { get; set; }
        public bool Durable { get; set; }
        public bool AutoDelete { get; set; }
        public ushort PrefectCount { get; set; }

        public Dictionary<ushort, ushort> PrefetchOrigins = new Dictionary<ushort, ushort>();
    }

    public class AppSettings 
    {
        public int ReconnectDelay { get; set; }
        public int ReconnectCount { get; set; }
        public int ConsumeDelay { get; set; }
    }

    public class RetrySettings
    {
        public int Count { get; set; }
        public int Delay { get; set; }
    }
}
