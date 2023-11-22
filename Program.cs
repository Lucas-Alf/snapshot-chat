using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace SnapshotChat
{
    partial class SnapshotChat
    {
        private static List<string> CHAT_CHANNELS = new List<string>();
        private static List<string> SNAPSHOT_CHANNELS = new List<string>();
        private static List<string> CHAT_HISTORY = new List<string>();
        private static string CHAT_EXCHANGE = "chat";
        private static string SNAPSHOT_EXCHANGE = "snapshot";

        static void Main(string[] args)
        {
            //Generate random process name
            int rand = new Random(Guid.NewGuid().GetHashCode()).Next();
            string processName = rand.ToString();

            using (var chatChannel = ChannelFactory.OpenConnection(CHAT_EXCHANGE, processName))
            using (var snapshotChannel = ChannelFactory.OpenConnection(SNAPSHOT_EXCHANGE, processName))
            {
                ChatWrite("RabbitMQ Connected");
                var receiveHandler = HandleReceive(chatChannel, processName);
                var sendHandler = HandleSend(chatChannel, processName);
                var receiveSnapshotMarkerHandler = HandleReceiveSnapshotMarker(snapshotChannel, processName);
                var sendSnapshotRequestHandler = HandleRequestSnapshot(snapshotChannel, processName);

                receiveHandler.Start();
                sendHandler.Start();
                receiveSnapshotMarkerHandler.Start();
                sendSnapshotRequestHandler.Start();

                receiveHandler.Wait();
                sendHandler.Wait();
                receiveSnapshotMarkerHandler.Wait();
                sendSnapshotRequestHandler.Wait();
            }
        }

        /// <summary>
        /// Send chat messages to other processes
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="processName"></param>
        /// <returns></returns>
        private static Task HandleSend(IModel channel, string processName) => new Task(() =>
        {
            RegisterProcess(channel, processName, CHAT_EXCHANGE, CHAT_CHANNELS);

            while (true)
            {
                var input = GetUserInput(processName);
                if (!string.IsNullOrEmpty(input))
                {
                    var body = Encoding.UTF8.GetBytes(processName + "/%#%/" + input);
                    foreach (var process in CHAT_CHANNELS)
                    {
                        if (process.ToString() != processName)
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

        /// <summary>
        /// Receive chat messages from other processes
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="processName"></param>
        /// <returns></returns>
        private static Task HandleReceive(IModel channel, string processName) => new Task(() =>
        {
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body).Split("/%#%/");
                var sender = message[0];
                var msg = message[1];
                ChatWrite($"Process {sender}: {msg}");
            };
            //Thread.Sleep(5000);
            channel.BasicConsume(
                queue: processName,//channel.CurrentQueue,
                autoAck: true,
                consumer: consumer
            );
        });

        /// <summary>
        /// Send snapshot requests to other processes
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="processName"></param>
        /// <returns></returns>
        private static Task HandleRequestSnapshot(IModel channel, string processName) => new Task(() =>
        {
            RegisterProcess(channel, processName, SNAPSHOT_EXCHANGE, SNAPSHOT_CHANNELS);
            var rand = new Random();

            // Wait some seconds before starts to send snapshots request
            Thread.Sleep(10000);
            while (true)
            {
                var value = rand.Next(5);
                var guess = rand.Next(5);
                if (guess == value)
                {
                    ChatWrite($"Me ({processName}): requesting snapshot", ConsoleColor.Green);
                    var body = Encoding.UTF8.GetBytes(processName + "/%#%/" + "snapshot");
                    foreach (var process in SNAPSHOT_CHANNELS)
                    {
                        if (process.ToString() != processName)
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

                Thread.Sleep(10000);
            }
        });


        /// <summary>
        /// Receive snapshot request from other processes
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="processName"></param>
        /// <returns></returns>
        private static Task HandleReceiveSnapshotMarker(IModel channel, string processName) => new Task(() =>
        {
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body).Split("/%#%/");
                var sender = message[0];
                var msg = message[1];
                ChatWrite($"Process {sender}: sent a snapshot request", ConsoleColor.Green);
            };
            //Thread.Sleep(5000);
            channel.BasicConsume(
                queue: processName,//channel.CurrentQueue,
                autoAck: true,
                consumer: consumer
            );
        });
    }
}