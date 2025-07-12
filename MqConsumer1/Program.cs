using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Wrap;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // ─── ASCII banner ──────────────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("╔══════════════════════════════════════════════╗");
        Console.WriteLine("║         RabbitMQ Async Consumer 1            ║");
        Console.WriteLine("║          (with Polly resilience)             ║");
        Console.WriteLine("╚══════════════════════════════════════════════╝");
        Console.ResetColor();

        // ─── Step 1: Setup ConnectionFactory ─────────────────────────────────
        var factory = new ConnectionFactory { HostName = "localhost" };
        Console.WriteLine("[Info] Preparing to connect to RabbitMQ...");

        // ─── Step 2: Define generic Polly policies for connection ───────────
        // 2a) Retry policy: 5 retries, exponential back-off (2s, 4s, 8s…)
        IAsyncPolicy<IConnection> connRetry = Policy<IConnection>
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, wait, retry, ctx) =>
                {
                    Console.WriteLine($"[Conn Retry {retry}] waiting {wait.TotalSeconds:N1}s due to: {outcome.Exception?.Message}");
                }
            );

        // 2b) Your existing non-generic breaker
        AsyncCircuitBreakerPolicy nonGenericBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (ex, breakDelay) =>
                    Console.WriteLine($"[Conn Circuit Open] for {breakDelay.TotalSeconds:N1}s: {ex.Message}"),
                onReset: () =>
                    Console.WriteLine("[Conn Circuit Reset] Connection attempts will resume.")
            );

        // 2c) Lift it and wrap
        AsyncPolicyWrap<IConnection> connectionPolicy = Policy.WrapAsync<IConnection>(
            connRetry,
            nonGenericBreaker.AsAsyncPolicy<IConnection>()
        );


        // ─── Step 3: Establish connection under resilience policy ─────────────
        IConnection connection;
        try
        {
            connection = await connectionPolicy.ExecuteAsync(
                () => factory.CreateConnectionAsync()
            );
            Console.WriteLine("[Info] Connected to RabbitMQ.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Fatal] Could not connect to RabbitMQ: {ex.Message}");
            return;
        }

        // ─── Step 4: Create channel normally (no Polly here) ────────────────
        using var channel = await connection.CreateChannelAsync();

        // ─── Step 5: Declare exchange & queue (unchanged) ────────────────────
        await channel.ExchangeDeclareAsync(
            exchange: "message_queue",
            durable: true,
            autoDelete: false,
            type: ExchangeType.Fanout
        );
        await channel.QueueDeclareAsync(
            queue: "message_received-1",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );
        await channel.QueueBindAsync(
            queue: "message_received-1",
            exchange: "message_queue",
            routingKey: string.Empty
        );

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("[Info] Waiting for messages...");
        Console.ResetColor();

        // ─── Step 6: Configure consumer and start consuming ─────────────────
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                // Process the message
                Console.WriteLine($"Received: {message}");
                await Task.Delay(500); // simulate work

                // Acknowledge on success
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                // Log any processing errors
                Console.WriteLine($"[Error] Processing failed: {ex.Message}");
                // Optionally you could NACK and requeue:
                // await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await channel.BasicConsumeAsync(
            queue: "message_received-1",
            autoAck: false,
            consumer: consumer
        );

        // ─── Keep the application running ─────────────────────────────────────
        Console.ReadLine();
    }
}
