using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;

namespace VideoConverter
{
    public class RabbitMqService : IRabbitMqService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public RabbitMqService()
        {
            var factory = new ConnectionFactory()
            {
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST")
                           ?? throw new ArgumentNullException("RABBITMQ_HOST", "Environment variable 'RABBITMQ_HOST' is not set."),
                UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER")
                           ?? throw new ArgumentNullException("RABBITMQ_USER", "Environment variable 'RABBITMQ_USER' is not set."),
                Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD")
                           ?? throw new ArgumentNullException("RABBITMQ_PASSWORD", "Environment variable 'RABBITMQ_PASSWORD' is not set."),
                VirtualHost = Environment.GetEnvironmentVariable("RABBITMQ_VHOST")
                              ?? throw new ArgumentNullException("RABBITMQ_VHOST", "Environment variable 'RABBITMQ_VHOST' is not set."),
                Port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out int port)
                    ? port
                    : throw new ArgumentNullException("RABBITMQ_PORT", "Environment variable 'RABBITMQ_PORT' is not set or is invalid.")
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: "task_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
        }

        public void ListenForMessages(Func<string, Task<bool>> messageHandler)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    // Process the message in a background task
                    await Task.Run(async () =>
                    {
                        await messageHandler(message);
                    });
                    
                    // Acknowledge the message after processing
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    // Log the error or handle it as needed
                    Console.WriteLine($"Error processing message: {ex.Message}");
                    // Optionally, requeue or nack the message if necessary
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(queue: "task_queue", autoAck: false, consumer: consumer);

            Console.WriteLine(" [*] Waiting for messages. To exit press CTRL+C");
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }

    public interface IRabbitMqService : IDisposable
    {
        void ListenForMessages(Func<string, Task<bool>> messageHandler);
    }
}
