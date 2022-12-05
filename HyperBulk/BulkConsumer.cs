using Newtonsoft.Json;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace HyperBulk
{
    public abstract class BulkConsumer<T>
    {
        private ConsumerSettings _settings;
        private int ReconnectValue = 0;
        private ulong Firsttag = 0;
        private ulong Lasttag = 0;
        private static object LockObj = new object();
        private List<T> BulkMessage = new List<T>();
        public BulkConsumer(ConsumerSettings settings)
        {
            _settings = settings;
            StartConsumer();
        }

        private async void StartConsumer()
        {
            var factory = new ConnectionFactory() { HostName = _settings.RabbitMQSettings.HostName, Port = _settings.RabbitMQSettings.Port, UserName = _settings.RabbitMQSettings.UserName, Password = _settings.RabbitMQSettings.Password, RequestedHeartbeat = TimeSpan.FromSeconds(_settings.RabbitMQSettings.HeartBeat) };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                connection.ConnectionShutdown += Connection_ConnectionShutdown;

                string message;
                T? payload;
                uint QueueMessageCount = 0;
                KeyValuePair<ushort, ushort> pair;

                var ConsumerPolicy = Policy.Handle<Exception>().Or<HttpRequestException>(response => response.StatusCode != System.Net.HttpStatusCode.OK).WaitAndRetry(_settings.RetrySettings.Count, attemptcount => TimeSpan.FromSeconds(attemptcount * _settings.RetrySettings.Delay));

                ChannelQueueBind(channel);

                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };
                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (model, ea) =>
                {
                    lock (LockObj)
                    {
                        message = string.Empty;
                        payload = default(T);
                        message = Encoding.UTF8.GetString(ea.Body.ToArray());
                        try
                        {
                            Firsttag = ea.DeliveryTag;
                            payload = JsonConvert.DeserializeObject<T>(message, settings);
                            if (payload != null)
                                BulkMessage.Add(payload);
                            else
                                channel.BasicNack(ea.DeliveryTag, false, false);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                            if (channel.IsOpen)
                                channel.BasicNack(ea.DeliveryTag, false, false);
                        }
                    }
                };
                channel.BasicConsume(queue: _settings.RabbitMQSettings.QueueName,
                autoAck: false,
                consumer: consumer);


                var timer = new PeriodicTimer(TimeSpan.FromSeconds(_settings.AppSettings.ConsumeDelay));
                while (await timer.WaitForNextTickAsync())
                {
                    lock (LockObj)
                    {
                        if (Lasttag != Firsttag && BulkMessage.Count() > 0)
                        {
                            try
                            {
                                ConsumerPolicy.Execute(() =>
                                {
                                    if (channel.IsOpen)
                                        Received(BulkMessage);
                                });
                                if (channel.IsOpen)
                                    channel.BasicAck(Firsttag, true);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                if (channel.IsOpen)
                                    channel.BasicNack(Firsttag, true, false);
                            }
                            if (channel.IsClosed || channel == null)
                            {
                                timer.Dispose();
                                Firsttag = 0;
                                break;
                            }
                            BulkMessage.Clear();

                            if (_settings.RabbitMQSettings.PrefetchOrigins.Count > 0)
                            {
                                QueueMessageCount = channel.MessageCount(_settings.RabbitMQSettings.QueueName);
                                pair = _settings.RabbitMQSettings.PrefetchOrigins.Where(a => a.Key < 0).OrderByDescending(x => x.Key).FirstOrDefault();
                                if (pair.Key == 0)
                                    channel.BasicQos(0, 10, true);
                                else
                                    channel.BasicQos(0, pair.Value, true);
                            }
                            Lasttag = Firsttag;
                        }
                    }
                }
            }
        }

        private void ChannelQueueBind(IModel channel)
        {
            channel.ExchangeDeclare(_settings.RabbitMQSettings.QueueName + "_Exchange_dlx", ExchangeType.Direct, _settings.RabbitMQSettings.Durable, _settings.RabbitMQSettings.AutoDelete);
            channel.QueueDeclare(_settings.RabbitMQSettings.QueueName + "_dlq", _settings.RabbitMQSettings.Durable, _settings.RabbitMQSettings.Exclusive, _settings.RabbitMQSettings.AutoDelete, null);
            channel.QueueBind(_settings.RabbitMQSettings.QueueName + "_dlq", _settings.RabbitMQSettings.QueueName + "_Exchange_dlx", "");
            var arguments = new Dictionary<string, object>()
                {
                    { "x-dead-letter-exchange", _settings.RabbitMQSettings.QueueName + "_Exchange_dlx" },
                    { "x-dead-letter-routing-key", "" }
                };
            channel.BasicQos(0, _settings.RabbitMQSettings.PrefectCount, true);
            channel.QueueDeclare(queue: _settings.RabbitMQSettings.QueueName,
                                 durable: _settings.RabbitMQSettings.Durable,
                                 exclusive: _settings.RabbitMQSettings.Exclusive,
                                 autoDelete: _settings.RabbitMQSettings.AutoDelete,
                                 arguments: arguments);
            channel.ExchangeDeclare(_settings.RabbitMQSettings.QueueName + "_Exchange", ExchangeType.Direct, _settings.RabbitMQSettings.Durable, _settings.RabbitMQSettings.AutoDelete);
            channel.QueueBind(_settings.RabbitMQSettings.QueueName, _settings.RabbitMQSettings.QueueName + "_Exchange", "");
        }

        private void Connection_ConnectionShutdown(object? sender, ShutdownEventArgs e)
        {
            BulkMessage.Clear();
            if (_settings.AppSettings.ReconnectCount == 0)
            {
                Thread.Sleep(_settings.AppSettings.ReconnectDelay * 1000);
                StartConsumer();
            }
            else
            {
                ReconnectValue = ReconnectValue + 1;
                if (ReconnectValue == _settings.AppSettings.ReconnectCount)
                    Environment.Exit(0);
                Thread.Sleep(_settings.AppSettings.ReconnectDelay * 1000);
                StartConsumer();
            }
        }

        protected virtual HttpResponseMessage Received(List<T> source)
        {
            return new HttpResponseMessage();
        }
    }
}
