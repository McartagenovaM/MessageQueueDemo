using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

var factory = new ConnectionFactory
{
    HostName = "localhost"
};

using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

// ASCII banner
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("╔═════════════════════════════════════════════════╗");
Console.WriteLine("║         RabbitMQ Async Worker Consumer 2        ║");
Console.WriteLine("║                                                 ║");
Console.WriteLine("║          Reading messages from queue!           ║");
Console.WriteLine("╚═════════════════════════════════════════════════╝");
Console.WriteLine();
Console.ResetColor();

//Add exchange declaration for Fanout reading from a queue
await channel.ExchangeDeclareAsync(
    exchange: "message_queue",
    durable: true,
    autoDelete: false,
    type: ExchangeType.Fanout
);



await channel.QueueDeclareAsync(
    queue: "message_received-2",
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: null
);

await channel.QueueBindAsync(
    queue: "message_received-2",
    exchange: "message_queue",
    routingKey: string.Empty
);

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("Waiting for messages...");
Console.ResetColor();
Console.WriteLine();

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += async (sender, eventArgs) =>
{
    var body = eventArgs.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);
    Console.WriteLine($"Received: {message}");
    // Simulate processing time
    await Task.Delay(500);
    // Acknowledge the message
    await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
};

await channel.BasicConsumeAsync(
    queue: "message_received-2",
    autoAck: false,
    consumer: consumer
);

Console.ReadLine(); // Keep the application running to listen for messages