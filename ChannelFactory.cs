using System.Text;
using RabbitMQ.Client;

namespace SnapshotChat
{
    public class ChannelFactory
    {
        public static IModel OpenConnection(string exchange, string processName)
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
                queue: exchange,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            channel.QueueDeclare(
                queue: $"{exchange}-{processName}",
                durable: false,
                exclusive: false,
                autoDelete: true,
                arguments: null
            );

            channel.ExchangeDeclare(exchange: exchange, type: ExchangeType.Fanout);

            var body = Encoding.UTF8.GetBytes(processName);
            channel.BasicPublish(
                exchange: exchange,
                routingKey: string.Empty,
                basicProperties: null,
                body: body
            );

            return channel;
        }
    }
}