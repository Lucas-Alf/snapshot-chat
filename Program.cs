using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections;
using System.Text;

namespace SnapshotChat
{
    class SnapshotChat
    {
        static void Main(string[] args)
        {
            using (var channel = ChannelFactory.OpenConnection("processes"))
            {
                //Generate random process name
                int rand = new Random(Guid.NewGuid().GetHashCode()).Next();
                string randStr = rand.ToString();

                //Input queue of process
                channel.QueueDeclare(
                    queue: randStr,
                    durable: false,
                    exclusive: false,
                    autoDelete: true,
                    arguments: null
                );

                channel.ExchangeDeclare(exchange: "processes", type: ExchangeType.Fanout);

                //Write process name on queue of processe
                var body = Encoding.UTF8.GetBytes(randStr);
                channel.BasicPublish(
                    exchange: "processes",
                    routingKey: string.Empty,
                    basicProperties: null,
                    body: body
                );

                Console.WriteLine("RabbitMQ Connected");
                var receiveHandler = HandleReceive(channel, randStr);
                var sendHandler = HandleSend(channel, randStr);

                receiveHandler.Start();
                sendHandler.Start();

                receiveHandler.Wait();
                sendHandler.Wait();
            }
        }

        private static Task HandleSend(IModel channel, string name) => new Task(() =>
        {
            var processes = new ArrayList();

            channel.ExchangeDeclare(exchange: "processes", type: ExchangeType.Fanout);
            var queueName = channel.QueueDeclare().QueueName;
            channel.QueueBind(queue: queueName,
                exchange: "processes",
                routingKey: string.Empty);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($"{DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")} - Process ??????: {message}");
                processes.Add(body);
            };

            channel.BasicConsume(
                queue: queueName,//channel.CurrentQueue,
                autoAck: false,
                consumer: consumer
            );


            while (true)
            {
                var input = GetUserInput();
                if (!string.IsNullOrEmpty(input))
                {
                    var body = Encoding.UTF8.GetBytes(name + "/%#%/" + input);
                    foreach (var process in processes)
                    {
                        if (process.ToString() != name)
                        {
                            channel.BasicPublish(
                                exchange: string.Empty,
                                routingKey: process.ToString(),
                                basicProperties: null,
                                body: body
                            );
                        }
                    }
                }
            }
        });

        private static Task HandleReceive(IModel channel, string name) => new Task(() =>
        {
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body).Split("/%#%/");
                var sender = message[0];
                var msg = message[1];
                Console.WriteLine($"{DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")} - Process {sender}: {msg}");
            };

            channel.BasicConsume(
                queue: name,//channel.CurrentQueue,
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