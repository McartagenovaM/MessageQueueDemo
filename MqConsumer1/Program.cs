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
using System.Text.Json;
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

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("[Info] Waiting for messages...");
        Console.WriteLine();
        Console.ResetColor();

        // ─── Step 6: Configure consumer and start consuming ─────────────────
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                // 1) Parse JSON and get messageType
                using var doc = JsonDocument.Parse(message);
                var header = doc.RootElement.GetProperty("header");
                string messageType = header.GetProperty("messageType").GetString();

                // 2) Print a fancy event banner
                Console.WriteLine();  // blank line
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"╔════════════ {messageType.ToUpper()} EVENT ════════════╗");
                Console.ResetColor();

                // 3) Dispatch based on messageType
                switch (messageType)
                {
                    case "NewCustomer":
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("✔ New Customer Request Received");
                            Console.ResetColor();

                            var p = doc.RootElement.GetProperty("payload");
                            string first = p.GetProperty("FirstName").GetString();
                            string last = p.GetProperty("LastName").GetString();
                            string to = $"{first.ToLower()}.{last.ToLower()}@example.com";
                            string subj = $"Welcome, {first} {last}!";

                            Console.WriteLine($"  • Name   : {first} {last}");
                            Console.WriteLine($"  • Email  : {to}");
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("📧 Sending Welcome Email...");
                            Console.ResetColor();
                            Console.WriteLine($"    → To     : {to}");
                            Console.WriteLine($"    → Subject: {subj}");
                        }
                        break;

                    case "ProductDelivered":
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("✔ Product Delivery Event Received");
                            Console.ResetColor();

                            var p = doc.RootElement.GetProperty("payload");
                            string pid = p.GetProperty("productId").GetString();
                            string carrier = p.GetProperty("carrier").GetString();
                            string delivered = p.GetProperty("deliveryDate").GetString();

                            Console.WriteLine($"  • Product ID  : {pid}");
                            Console.WriteLine($"  • Carrier     : {carrier}");
                            Console.WriteLine($"  • Delivered At: {delivered}");
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("📧 Sending Delivery Confirmation Email...");
                            Console.ResetColor();
                            Console.WriteLine($"    → To     : customer@example.com");
                            Console.WriteLine($"    → Subject: Delivery Confirmation for {pid}");
                            Console.WriteLine($"    → Body   : Your product {pid} was delivered by {carrier} at {delivered}.");
                        }
                        break;

                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"⚠ No handler for messageType '{messageType}'");
                        Console.ResetColor();
                        break;
                }

                // 4) Footer line and spacing
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"╚════════════════════════════════════════════════╝");
                Console.ResetColor();
                Console.WriteLine();

                // simulate processing time and ack
                await Task.Delay(500);
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
