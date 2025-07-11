using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using MqPublisherConsole.services;

// RabbitMQ Publisher with ASCII UI, random delay, GUID + timestamp, exit on ESC key
class Program
{
    static async Task Main(string[] args)
    {
        // ASCII banner
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("╔══════════════════════════════════════════════╗");
        Console.WriteLine("║         RabbitMQ Async Publisher             ║");
        Console.WriteLine("║                                              ║");
        Console.WriteLine("║        Sending messages to queue!            ║");
        Console.WriteLine("╚══════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.ResetColor();


        // Step 1: Setup ConnectionFactory
        var factory = new ConnectionFactory { HostName = "localhost" };
        Console.WriteLine("[Connecting to RabbitMQ...]");

        // Step 2: Establish connection and channel asynchronously
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        // Step 3: Declare the durable queue
        Console.WriteLine("[Declaring queue 'message_queue'...]");
        Console.WriteLine();
        // Declare the exchange with Fanout type - Sends messages to all bound queues
        await channel.ExchangeDeclareAsync(
            exchange: "message_queue",
            durable: true,
            autoDelete: false,
            type: ExchangeType.Fanout
        );

        //declare the single queue with durable settings
        await channel.QueueDeclareAsync(
         queue: "message_queue-3",
         durable: true,
         exclusive: false,
         autoDelete: false,
         arguments: null
        );




        // Initialize random delay generator
        var rnd = new Random();
        // --- PREPARE EVENT GENERATORS ---

        var eventGenerators = new List<Func<string>>
        {
            Customers.GenerateRandomCustomerJson,
            InvoiceCreated.GenerateRandomInvoiceCreatedJson

            // stub placeholders – implement these in services/PaymentReceived.cs, services/ProductDelivered.cs
            //PaymentReceived.GenerateRandomPaymentReceivedJson,
            //ProductDelivered.GenerateRandomProductDeliveredJson
        };




        // Step 4: Publish messages until ESC is pressed
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Sending messages, Press ESC to stop publishing.");
        Console.WriteLine();
        Console.ResetColor();
        // Loop until Escape key is pressed
        while (true)
        {
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                break;

            //// Prepare message content with GUID and timestamp
            //var guid = Guid.NewGuid();
            //var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            //var message = $"{guid} - {timestamp}";
            //var body = Encoding.UTF8.GetBytes(message);

            // Generate random customer JSON message
            //string messageJson = Customers.GenerateRandomCustomerJson();

            // 1) pick a random generator
            int idx = rnd.Next(eventGenerators.Count);
            Func<string> gen = eventGenerators[idx];

            // 2) invoke it
            string messageJson = gen();
               
            byte[] body = Encoding.UTF8.GetBytes(messageJson);

            // Publish message - to basic queue
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: "message_queue-3",
                mandatory: true,
                basicProperties: new BasicProperties { Persistent = true },
                body: body
            );

            // Publish message to fanout exchange   
            await channel.BasicPublishAsync(
                exchange: "message_queue",
                routingKey: string.Empty,
                mandatory: true,
                basicProperties: new BasicProperties { Persistent = true },
                body: body
            );



            //Console.WriteLine($"Message Sent: {messageJson}");
            // Print the header of the message
            PrintHeader.Print(messageJson);


            // Random delay between 1 and 4 seconds
            int delayMs = rnd.Next(500, 2001);
            //Console.WriteLine($"Waiting {delayMs}ms before next message...");
            await Task.Delay(delayMs);
        }
    }
}
