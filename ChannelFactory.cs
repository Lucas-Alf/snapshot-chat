using RabbitMQ.Client;

namespace SnapshotChat
{
    public class ChannelFactory
    {
        public static IModel OpenConnection(string queueName)
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "user",
                Password = "bitnami"
            };

            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            
            channel.QueueDeclare(
                queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            return channel;
        }
    }
}