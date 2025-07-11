// File: services/InvoiceCreated.cs
using System;
using System.Linq;
using System.Text.Json;
using MqPublisherConsole.services;   // ← bring in GenerateRandomInvoiceCustomer

namespace MqPublisherConsole.services
{
    public static class InvoiceCreated
    {
        private static readonly Random _rnd = new();
        private static readonly (string Id, string Description, double UnitPrice, string Brand)[] Products =
        {
            ("PROD-001", "UltraBook 14\" Laptop",           1200.00, "Contoso"),
            ("PROD-002", "Ergonomic Wireless Mouse",         25.50, "Fabrikam"),
            ("PROD-003", "24\" Full HD LED Monitor",        180.75, "Tailspin"),
            ("PROD-004", "Mechanical Keyboard",             85.00, "AdventureWorks"),
            ("PROD-005", "USB-C Docking Station",          150.25, "Northwind"),
            ("PROD-006", "Noise-Cancelling Headphones",    220.40, "Litware"),
            ("PROD-007", "Webcam 1080p",                    45.99, "Coho"),
            ("PROD-008", "Portable SSD 1TB",               130.00, "Humongous"),
            ("PROD-009", "Wireless Charger Pad",            30.00, "Wingtip"),
            ("PROD-010", "Smartphone Stand",                15.75, "Proseware")
        };

        public static string GenerateRandomInvoiceCreatedJson()
        {
            // 1) Envelope header
            var header = new
            {
                messageType = "InvoiceCreated",
                correlationId = Guid.NewGuid().ToString(),
                sentAt = DateTime.UtcNow.ToString("o"),
                source = "BillingService"
            };

            // 2) Build a random invoiceHeader, using our new customer generator
            var invoiceHeader = new
            {
                invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}-{_rnd.Next(1000, 9999)}",
                invoiceDate = DateTime.UtcNow.ToString("o"),
                customer = Customers.GenerateRandomCustomer()
            };

            // 3) Pick 1–5 random products
            int itemCount = _rnd.Next(1, 6);
            var lineItems = Products
                .OrderBy(_ => _rnd.Next())
                .Take(itemCount)
                .Select(p =>
                {
                    int qty = _rnd.Next(1, 11);
                    double lineTotal = Math.Round(p.UnitPrice * qty, 2);
                    return new
                    {
                        productId = p.Id,
                        description = p.Description,
                        brand = p.Brand,
                        quantity = qty,
                        unitPrice = p.UnitPrice,
                        lineTotal
                    };
                })
                .ToArray();

            // 4) Compute totals (15% tax)
            double subTotal = Math.Round(lineItems.Sum(x => x.lineTotal), 2);
            double tax = Math.Round(subTotal * 0.15, 2);
            double grandTotal = Math.Round(subTotal + tax, 2);
            var totals = new { subTotal, tax, grandTotal };

            // 5) Full payload + envelope
            var payload = new { invoiceHeader, lineItems, totals };
            var envelope = new { header, payload };

            // 6) Serialize
            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Serialize(envelope, opts);
        }
    }
}
