using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace SnapshotChat
{
    partial class SnapshotChat
    {
        private static List<string> CHAT_CHANNELS = new List<string>();
        private static List<string> SNAPSHOT_CHANNELS = new List<string>();
        private static List<string> CURRENT_STATE = new List<string>();
        private static Dictionary<string, SnapshotState> SNAPSHOT_STORAGE = new Dictionary<string, SnapshotState>();
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
                var receiveSnapshotMarkerHandler = HandleReceiveSnapshotRequest(snapshotChannel, processName);
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
                        if (process != processName)
                        {
                            channel.BasicPublish(
                                exchange: string.Empty,
                                routingKey: $"{CHAT_EXCHANGE}-{process}",
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
                var rawData = Encoding.UTF8.GetString(body).Split("/%#%/");
                var sender = rawData[0];
                var message = rawData[1];
                ChatWrite($"Process {sender}: {message}");

                // Snapshot the received message if has a snapshot in progress
                // and if the sender process not is the snapshot initiator
                var inProgressSnapshots = SNAPSHOT_STORAGE
                    .Where(x =>
                        x.Value.Status == SnapshotStatus.InProgress &&
                        x.Value.InitiatorProcess != sender
                    );

                foreach (var snapshot in inProgressSnapshots)
                    SnapshotMessage(snapshot.Key, sender, message);
            };

            channel.BasicConsume(
                queue: $"{CHAT_EXCHANGE}-{processName}",
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
                    // Create a new snapshot marker
                    int randMarker = new Random(Guid.NewGuid().GetHashCode()).Next();
                    string snapshotMarker = randMarker.ToString();

                    // Notify that a snapshot is being started
                    ChatWrite($"(Snapshot): Starting snapshot with marker {snapshotMarker}.", ConsoleColor.Green);

                    // Save current state on snapshot
                    SnapshotCurrentState(snapshotMarker, processName);

                    // Send a marker message to all output channels
                    var body = Encoding.UTF8.GetBytes(processName + "/%#%/" + snapshotMarker);
                    foreach (var process in SNAPSHOT_CHANNELS)
                    {
                        channel.BasicPublish(
                            exchange: string.Empty,
                            routingKey: $"{SNAPSHOT_EXCHANGE}-{process}",
                            basicProperties: null,
                            body: body
                        );
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
        private static Task HandleReceiveSnapshotRequest(IModel channel, string processName) => new Task(() =>
        {
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var rawData = Encoding.UTF8.GetString(body).Split("/%#%/");
                var sender = rawData[0];
                var message = rawData[1];
                ChatWrite($"(Snapshot): Received marker {message} from process {sender}.", ConsoleColor.Green);

                // If the process never has seen this snapshot marker
                if (!SNAPSHOT_STORAGE.ContainsKey(message))
                {
                    // Snapshot current state
                    SnapshotCurrentState(message, sender);

                    // Reply the snapshot marker to all output channels
                    var replyBody = Encoding.UTF8.GetBytes(processName + "/%#%/" + message);
                    foreach (var process in SNAPSHOT_CHANNELS)
                    {
                        channel.BasicPublish(
                            exchange: string.Empty,
                            routingKey: $"{SNAPSHOT_EXCHANGE}-{process}",
                            basicProperties: null,
                            body: replyBody
                        );
                    }
                }
                else
                {
                    // Save snapshot file
                    var snapshotFile = $"{processName}-{DateTime.Now.ToString("dd-MM-yyyy-HH-mm-ss")}.txt";
                    ChatWrite($"(Snapshot): Marker {message} done. ({snapshotFile})", ConsoleColor.Green);
                    Directory.CreateDirectory("snapshots");
                    File.WriteAllLines($"snapshots/{snapshotFile}", SNAPSHOT_STORAGE[message].Values);

                    // Mark snapshot as done
                    SNAPSHOT_STORAGE[message].Status = SnapshotStatus.Done;
                }
            };

            channel.BasicConsume(
                queue: $"{SNAPSHOT_EXCHANGE}-{processName}",
                autoAck: true,
                consumer: consumer
            );
        });
    }
}