﻿using Polly;
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
        Console.WriteLine("║       RabbitMQ Async Worker Consumer 2       ║");
        Console.WriteLine("║          (with Polly resilience)             ║");
        Console.WriteLine("╚══════════════════════════════════════════════╝");
        Console.ResetColor();

        // ─── Step 1: Setup ConnectionFactory ─────────────────────────────────
        var factory = new ConnectionFactory { HostName = "localhost" };
        Console.WriteLine("[Info] Preparing to connect to RabbitMQ...");

        // ─── Step 2: Define generic Polly policies for connection ───────────
        // 2a) Retry policy: 3 retries, exponential back-off (2s, 4s, 8s…)
        IAsyncPolicy<IConnection> connRetry = Policy<IConnection>
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
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
                // 1) Parse the JSON and extract messageType
                using var doc = JsonDocument.Parse(message);
                var header = doc.RootElement.GetProperty("header");
                string messageType = header.GetProperty("messageType").GetString();

                // 2) Dispatch based on messageType
                switch (messageType)
                {
                    case "InvoiceCreated":
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("InvoiceCreated event received – generating invoice...");
                        Console.ResetColor();

                        // Extract invoice header
                        var invoicePayload = doc.RootElement.GetProperty("payload");
                        var invoiceHeader = invoicePayload.GetProperty("invoiceHeader");
                        string invNum = invoiceHeader.GetProperty("invoiceNumber").GetString();
                        string invDate = invoiceHeader.GetProperty("invoiceDate").GetString();
                        string customer = invoiceHeader
                                                 .GetProperty("customer")
                                                 .GetProperty("name")
                                                 .GetString();

                        // Extract totals
                        var totals = invoicePayload.GetProperty("totals");
                        double subTotal = totals.GetProperty("subTotal").GetDouble();
                        double tax = totals.GetProperty("tax").GetDouble();
                        double grandTotal = totals.GetProperty("grandTotal").GetDouble();

                        // Simulate printing a small invoice
                        Console.WriteLine("────────────────────────────────");
                        Console.WriteLine($"Invoice #: {invNum}");
                        Console.WriteLine($"Date      : {invDate}");
                        Console.WriteLine($"Customer  : {customer}");
                        Console.WriteLine($"Subtotal  : {subTotal:C}");
                        Console.WriteLine($"Tax (15%) : {tax:C}");
                        Console.WriteLine($"Total     : {grandTotal:C}");
                        Console.WriteLine("────────────────────────────────");
                        Console.WriteLine();
                        break;

                    case "PaymentReceived":
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("PaymentReceived event received – issuing payment voucher...");
                        Console.ResetColor();

                        // Extract payment details
                        var paymentPayload = doc.RootElement.GetProperty("payload");
                        string payInvNum = paymentPayload.GetProperty("invoiceNumber").GetString();
                        string payDate = paymentPayload.GetProperty("paymentDate").GetString();
                        double amountPaid = paymentPayload.GetProperty("amount").GetDouble();
                        string method = paymentPayload.GetProperty("paymentMethod").GetString();
                        string receiptNo = paymentPayload.GetProperty("receiptNumber").GetString();

                        // Simulate printing a payment voucher
                        Console.WriteLine("===== PAYMENT VOUCHER =====");
                        Console.WriteLine($"Invoice #:    {payInvNum}");
                        Console.WriteLine($"Date:         {payDate}");
                        Console.WriteLine($"Amount Paid:  {amountPaid:C}");
                        Console.WriteLine($"Method:       {method}");
                        Console.WriteLine($"Receipt #:    {receiptNo}");
                        Console.WriteLine("===========================");
                        Console.WriteLine();
                        break;

                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Received {messageType}: no special handling configured.");
                        Console.WriteLine();
                        Console.ResetColor();
                        break;
                }
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
            queue: "message_received-2",
            autoAck: false,
            consumer: consumer
        );

        // ─── Keep the application running ─────────────────────────────────────
        Console.ReadLine();
    }
}
