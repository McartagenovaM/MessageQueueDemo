using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using MqPublisherConsole.services;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using Polly.Wrap;

class Program
{
    static async Task Main(string[] args)
    {
        // ─── ASCII banner ──────────────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("╔══════════════════════════════════════════════╗");
        Console.WriteLine("║         RabbitMQ Async Publisher             ║");
        Console.WriteLine("║         (with Polly resilience)              ║");
        Console.WriteLine("╚══════════════════════════════════════════════╝");
        Console.ResetColor();

        // ─── Step 1: Setup ConnectionFactory ─────────────────────────────────
        var factory = new ConnectionFactory { HostName = "localhost" };
        Console.WriteLine("[Info] Connecting to RabbitMQ...");

        // ─── Step 2: Define Polly policies for connection ────────────────────
        // 2a) Retry policy: 5 retries, exponential back-off (2s, 4s, 8s…)
        AsyncRetryPolicy<IConnection> retryPolicy = Policy<IConnection>
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (result, wait, retry, ctx) =>
                    Console.WriteLine($"[Conn Retry {retry}] after {wait.TotalSeconds}s due to: {result.Exception?.Message}")
            );

        // 2b) Circuit-breaker: break after 3 consecutive faults, stay open 30s
        AsyncCircuitBreakerPolicy breakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (ex, breakDelay) =>
                    Console.WriteLine($"[Conn Circuit Open] for {breakDelay.TotalSeconds}s: {ex.Message}"),
                onReset: () =>
                    Console.WriteLine("[Conn Circuit Reset] Connection attempts will resume.")
            );

        // Wrap retry and breaker policies
        AsyncPolicyWrap<IConnection> connectionPolicy = Policy.WrapAsync<IConnection>(
            retryPolicy,
            breakerPolicy.AsAsyncPolicy<IConnection>()
        );

        // 2d) Establish the connection under the resilience policy
        IConnection connection;
        try
        {
            connection = await connectionPolicy.ExecuteAsync(() =>
                factory.CreateConnectionAsync());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Fatal] Could not connect to RabbitMQ: {ex.Message}");
            return;
        }

        // ─── Step 3: Create a channel ────────────────────────────────────────
        using var channel = await connection.CreateChannelAsync();

        // ─── Step 4: Declare exchange & queue ────────────────────────────────
        Console.WriteLine("[Info] Declaring exchange & queue...");
        await channel.ExchangeDeclareAsync(
            exchange: "message_queue",
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false
        );
        await channel.QueueDeclareAsync(
            queue: "message_queue-3",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        // ─── Step 5: Prepare event generators ────────────────────────────────
        var rnd = new Random();
        var eventGenerators = new List<Func<string>>
        {
            Customers.GenerateRandomCustomerJson,
            InvoiceCreated.GenerateRandomInvoiceCreatedJson,
            PaymentReceived.GenerateRandomPaymentReceivedJson,
            ProductDelivered.GenerateRandomProductDeliveredJson
        };

        // ─── Step 6: Define Polly policies for publishing ────────────────────
        // Retry on AlreadyClosedException
        AsyncRetryPolicy publishRetry = Policy
            .Handle<AlreadyClosedException>()
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (ex, wait, retry, ctx) =>
                    Console.WriteLine($"[Pub Retry {retry}] {ex.GetType().Name}: {ex.Message}")
            );

        // Circuit-breaker on AlreadyClosedException
        AsyncCircuitBreakerPolicy publishBreaker = Policy
            .Handle<AlreadyClosedException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(15),
                onBreak: (ex, breakDelay) =>
                    Console.WriteLine($"[Pub Circuit Open] for {breakDelay.TotalSeconds}s: {ex.Message}"),
                onReset: () =>
                    Console.WriteLine("[Pub Circuit Reset] Publishing will resume.")
            );

        // Wrap publish retry + breaker
        AsyncPolicyWrap publishPolicy = Policy.WrapAsync(publishRetry, publishBreaker);

        // ─── Step 7: Publish loop ─────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Press ESC to stop publishing random events...");
        Console.ResetColor();

        while (true)
        {
            // Exit on ESC
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                break;

            // 7a) Pick & generate a random event JSON
            int idx = rnd.Next(eventGenerators.Count);
            string messageJson = eventGenerators[idx]();
            byte[] body = Encoding.UTF8.GetBytes(messageJson);

            // 7b) Publish under publish-policy
            await publishPolicy.ExecuteAsync(async () =>
            {
                //  If the channel was closed, reconnect
                if (channel == null || !channel.IsOpen)
                {
                    Console.WriteLine("[Info] Channel closed, reconnecting...");
                    connection = await connectionPolicy.ExecuteAsync(() => factory.CreateConnectionAsync());
                    using var channel = await connection.CreateChannelAsync();
                }

                // Publish to direct queue (for trigger function)
                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: "message_queue-3",
                    mandatory: true,
                    basicProperties: new BasicProperties { Persistent = true },
                    body: body
                );

                // Publish to fanout exchange
                await channel.BasicPublishAsync(
                    exchange: "message_queue",
                    routingKey: string.Empty,
                    mandatory: true,
                    basicProperties: new BasicProperties { Persistent = true },
                    body: body
                );
            });

            // 7c) Print the header of the message
            PrintHeader.Print(messageJson);

            // 7d) Random delay between 2–4 seconds
            await Task.Delay(rnd.Next(2000, 4001));
        }
    }
}
