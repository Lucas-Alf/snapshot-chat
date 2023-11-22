using RabbitMQ.Client;
using RabbitMQ.Client.Events;
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
                string processName = rand.ToString();

                //Input queue of process
                channel.QueueDeclare(
                    queue: processName,
                    durable: false,
                    exclusive: false,
                    autoDelete: true,
                    arguments: null
                );

                channel.ExchangeDeclare(exchange: "processes", type: ExchangeType.Fanout);

                //Write process name on queue of processes
                var body = Encoding.UTF8.GetBytes(processName);
                channel.BasicPublish(
                    exchange: "processes",
                    routingKey: string.Empty,
                    basicProperties: null,
                    body: body
                );

                FancyConsoleWrite("RabbitMQ Connected");
                var receiveHandler = HandleReceive(channel, processName);
                var sendHandler = HandleSend(channel, processName);

                receiveHandler.Start();
                sendHandler.Start();

                receiveHandler.Wait();
                sendHandler.Wait();
            }
        }

        private static Task HandleSend(IModel channel, string name) => new Task(() =>
        {
            var processes = new List<string>();

            channel.ExchangeDeclare(exchange: "processes", type: ExchangeType.Fanout);
            var queueName = channel.QueueDeclare().QueueName;
            channel.QueueBind(
                queue: queueName,
                exchange: "processes",
                routingKey: string.Empty
            );

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                if (!processes.Contains(message) && !name.Equals(message))
                {
                    FancyConsoleWrite($"Process {message} joined the channel");
                    processes.Add(message);

                    //Workaround to if a new process enters it needs the name of the others
                    body = Encoding.UTF8.GetBytes(name);
                    Thread.Sleep(1000);
                    channel.BasicPublish(
                        exchange: "processes",
                        routingKey: string.Empty,
                        basicProperties: null,
                        body: body
                    );
                }
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
                FancyConsoleWrite($"Process {sender}: {msg}");
            };
            //Thread.Sleep(5000);
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

        private static void FancyConsoleWrite(string value)
        {
            Console.WriteLine($"{DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")} - {value}");
        }

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