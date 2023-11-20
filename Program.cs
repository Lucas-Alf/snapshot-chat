using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace SnapshotChat
{
    class SnapshotChat
    {
        static void Main(string[] args)
        {
            using (var channel = ChannelFactory.OpenConnection("msgs"))
            {
                Console.WriteLine("RabbitMQ Connected");
                var receiveHandler = HandleReceive(channel);
                var sendHandler = HandleSend(channel);

                receiveHandler.Start();
                sendHandler.Start();

                receiveHandler.Wait();
                sendHandler.Wait();
            }
        }

        private static Task HandleSend(IModel channel) => new Task(() =>
        {
            while (true)
            {
                var input = GetUserInput();
                if (!string.IsNullOrEmpty(input))
                {
                    var body = Encoding.UTF8.GetBytes(input);
                    channel.BasicPublish(
                        exchange: string.Empty,
                        routingKey: channel.CurrentQueue,
                        basicProperties: null,
                        body: body
                    );
                }
            }
        });

        private static Task HandleReceive(IModel channel) => new Task(() =>
        {
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($"{DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")} - Process ???: {message}");
            };

            channel.BasicConsume(
                queue: channel.CurrentQueue,
                autoAck: true,
                consumer: consumer
            );
        });


        // private static void WriteColorfulLine(string text, ConsoleColor color)
        // {
        //     Console.ForegroundColor = color;
        //     Console.WriteLine(text);
        //     Console.ResetColor();
        // }

        private static string? GetUserInput()
        {
            var input = Console.ReadLine();
            int currentCursorLine = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.WriteLine($"{DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")} - Me: {input}");
            Console.SetCursorPosition(0, currentCursorLine);
            return input;
        }
    }
}