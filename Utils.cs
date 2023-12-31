using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SnapshotChat
{
    partial class SnapshotChat
    {
        private static void RegisterProcess(IModel channel, string processName, string exchange, List<string> processes)
        {
            channel.ExchangeDeclare(exchange: exchange, type: ExchangeType.Fanout);
            var queueName = channel.QueueDeclare().QueueName;
            channel.QueueBind(
                queue: queueName,
                exchange: exchange,
                routingKey: string.Empty
            );

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                if (!processes.Contains(message) && !processName.Equals(message))
                {
                    if (exchange != SNAPSHOT_EXCHANGE)
                        ChatWrite($"Process {message} joined the channel");

                    processes.Add(message);

                    //Workaround to if a new process enters it needs the name of the others
                    body = Encoding.UTF8.GetBytes(processName);
                    Thread.Sleep(1000);
                    channel.BasicPublish(
                        exchange: exchange,
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
        }

        private static void ChatWrite(string value, ConsoleColor? color = null)
        {
            var text = $"{DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")} - {value}";
            CURRENT_STATE.Add(text);
            if (color.HasValue)
            {
                Console.ForegroundColor = color.Value;
                Console.WriteLine(text);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(text);
            }
        }

        private static string? GetUserInput(string processName)
        {
            var input = Console.ReadLine();
            int currentCursorLine = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            ChatWrite($"Me: {input}");
            Console.SetCursorPosition(0, currentCursorLine);
            return input;
        }

        private static void SaveMarker(string marker, string initiatorProcess)
        {
            if (!SNAPSHOT_STORAGE.ContainsKey(marker))
                SNAPSHOT_STORAGE.Add(
                    key: marker,
                    value: new SnapshotState
                    {
                        Status = SnapshotStatus.InProgress,
                        InitiatorProcess = initiatorProcess
                    }
                );
        }

        private static string SaveState(string maker, string processName, List<string> state)
        {
            var snapshotFile = $"{maker}.txt";
            Directory.CreateDirectory("snapshots");
            File.AppendAllText($"snapshots/{snapshotFile}", $"------------ PROCESS: {processName} ------------\n");
            File.AppendAllLines($"snapshots/{snapshotFile}", state);

            return snapshotFile;
        }
    }
}