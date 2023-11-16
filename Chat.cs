using System.Threading.Channels;

public static class Chat
{
    public static Task New(Channel<string> channel) => new Task(async () =>
    {
        var channel = Channel.CreateUnbounded<string>();
        await foreach (var message in channel.Reader.ReadAllAsync())
            Console.WriteLine(message);

        while (true)
        {
            var userInput = Console.ReadLine();
            if (!String.IsNullOrEmpty(userInput))
                await channel.Writer.WriteAsync(userInput);
        }
    });
}