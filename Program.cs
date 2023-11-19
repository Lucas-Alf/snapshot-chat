﻿using System.Net;
using MPI;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace SnapshotChat
{
    class SnapshotChat
    {
        static void Main(string[] args)
        {
            MPI.Environment.Run(comm =>
            {
                Console.WriteLine($"########## Process: {comm.Rank} ##########");
                var receiveHandler = HandleReceive(comm);
                var sendHandler = HandleSend(comm);
                //var snapshotHandler = HandleSnapshot(comm);

                receiveHandler.Start();
                sendHandler.Start();
                //snapshotHandler.Start();

                receiveHandler.Wait();
                sendHandler.Wait();
                //snapshotHandler.Wait();
            });
        }

        private static Task HandleSend(Intracommunicator comm) => new Task(() =>
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: "msgs",
                    durable: false,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

            while (true)
            {
                var input = Console.ReadLine();
                int currentCursorLine = Console.CursorTop;
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine($"{DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")} - Me: {input}");
                Console.SetCursorPosition(0, currentCursorLine);
                if (!string.IsNullOrEmpty(input))
                {
                    /*
                    for (int i = 0; i < comm.Size; i++)
                        if (i != comm.Rank)
                            comm.Send((input, comm.Rank), i, 0);
                    */
                    var body = Encoding.UTF8.GetBytes(input);

                    channel.BasicPublish(exchange: string.Empty,
                                        routingKey: "msgs",
                                        basicProperties: null,
                                        body: body);
                }
            }
        });

        private static Task HandleReceive(Intracommunicator comm) => new Task(() =>
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: "msgs",
                    durable: false,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

            var consumer = new EventingBasicConsumer(channel);

            while (true)
            {
                /*
                var (msg, sender) = comm.Receive<(string, int)>(Communicator.anySource, 0);
                if (!string.IsNullOrEmpty(msg) && sender != comm.Rank)
                    Console.WriteLine($"{DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")} - Process {sender}: {msg}");
                */
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    Console.WriteLine($" [x] Received {message}");
                };
                channel.BasicConsume(queue: "msgs",
                    autoAck: true,
                    consumer: consumer);
            }
        });


        private static Task HandleSnapshot(Intracommunicator comm) => new Task(() =>
        {
            while (true)
            {
                if (comm.Rank == 0)
                {
                    // Waits 10 seconds
                    Thread.Sleep(10000);
                    WriteColorfulLine($"{DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")} - Starting Snapshot", ConsoleColor.Green);
                    for (int i = 0; i < comm.Size; i++)
                    {
                        if (i != comm.Rank)
                            comm.Send(("StartSnapshot", comm.Rank), i, 1);
                    }
                }
                else
                {
                    var (msg, sender) = comm.Receive<(string, int)>(Communicator.anySource, 1);
                    if (msg == "StartSnapshot")
                        WriteColorfulLine($"{DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")} - Snapshot request received from process {sender}", ConsoleColor.Green);
                }
            }
        });


        private static void WriteColorfulLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}