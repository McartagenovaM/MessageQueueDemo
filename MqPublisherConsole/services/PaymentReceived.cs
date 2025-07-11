// File: services/PaymentReceived.cs
using System;
using System.Text.Json;
using MqPublisherConsole.services;  // for GenerateRandomCustomer()

namespace MqPublisherConsole.services
{
    public static class PaymentReceived
    {
        private static readonly Random _rnd = new();

        // Possible payment methods
        private static readonly string[] Methods =
            { "Cash", "WireTransfer", "CreditCard", "PayPal", "BankTransfer" };

        /// <summary>
        /// Generates a random PaymentReceived message as indented JSON,
        /// including a random customer, payment method, amount, dates and a receipt number.
        /// </summary>
        public static string GenerateRandomPaymentReceivedJson()
        {
            // 1) Envelope header
            var header = new
            {
                messageType = "PaymentReceived",
                correlationId = Guid.NewGuid().ToString(),
                sentAt = DateTime.UtcNow.ToString("o"),
                source = "PaymentService"
            };

            // 2) Pick a random payment method
            string method = Methods[_rnd.Next(Methods.Length)];

            // 3) Generate a random amount between 10.00 and 10000.00
            double amount = Math.Round(_rnd.NextDouble() * 9990 + 10, 2);

            // 4) Generate receipt number
            string receiptNumber;
            if (method == "CreditCard")
            {
                // simulate a masked card number
                string last4 = _rnd.Next(1000, 10000).ToString();
                receiptNumber = $"**** **** **** {last4}";
            }
            else
            {
                // use a random numeric receipt
                receiptNumber = _rnd.Next(100000000, 999999999).ToString();
            }

            // 5) Build payload, now using GenerateRandomCustomer()
            var payload = new
            {
                invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}-{_rnd.Next(1000, 9999)}",
                paymentDate = DateTime.UtcNow.ToString("o"),
                amount,
                paymentMethod = method,
                receiptNumber,
                customer = Customers.GenerateRandomCustomer()
            };

            // 6) Combine into envelope
            var envelope = new { header, payload };

            // 7) Serialize to JSON with camel-case and indentation
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Serialize(envelope, options);
        }
    }
}
