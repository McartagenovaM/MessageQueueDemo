// File: services/ProductDelivered.cs
using System;
using System.Text.Json;

namespace MqPublisherConsole.services
{
    public static class ProductDelivered
    {
        private static readonly Random _rnd = new();

        // Pools of sample data
        private static readonly string[] ProductIds =
        {
            "PROD-001", "PROD-002", "PROD-003", "PROD-004", "PROD-005",
            "PROD-006", "PROD-007", "PROD-008", "PROD-009", "PROD-010"
        };

        private static readonly string[] DeliveryPersons =
        {
            "John Doe", "Jane Smith", "Carlos Perez", "Maria Gomez", "Luis Torres"
        };

        private static readonly string[] Carriers =
        {
            "DHL", "UPS", "FedEx", "TNT", "USPS"
        };

        private static readonly string[] Streets =
        {
            "Main St", "Oak Ave", "Pine Rd", "Maple Dr", "Cedar Ln", "Elm St"
        };

        private static readonly string[] Cities =
        {
            "Quito", "Guayaquil", "Cuenca", "Ambato", "Manta"
        };

        private static readonly string[] Countries =
        {
            "Ecuador", "Colombia", "Peru", "Chile", "Brazil"
        };

        /// <summary>
        /// Generates a random ProductDelivered message as indented JSON,
        /// including orderId, productId, quantity, delivery info, carrier and location.
        /// </summary>
        public static string GenerateRandomProductDeliveredJson()
        {
            // 1) Envelope header
            var header = new
            {
                messageType = "ProductDelivered",
                correlationId = Guid.NewGuid().ToString(),
                sentAt = DateTime.UtcNow.ToString("o"),
                source = "DeliveryService"
            };

            // 2) Build payload fields
            string orderId = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{_rnd.Next(1000, 9999)}";
            string productId = Pick(ProductIds);
            int quantity = _rnd.Next(1, 6);
            string deliveryDate = DateTime.UtcNow.ToString("o");
            string deliveredBy = Pick(DeliveryPersons);
            string carrier = Pick(Carriers);
            string trackingNumber = $"TRACK-{_rnd.Next(100000, 999999)}";

            var location = new
            {
                address = $"{_rnd.Next(100, 9999)} {Pick(Streets)}",
                city = Pick(Cities),
                country = Pick(Countries)
            };

            var payload = new
            {
                orderId,
                productId,
                quantity,
                deliveryDate,
                deliveredBy,
                carrier,
                trackingNumber,
                location
            };

            // 3) Combine into full envelope
            var envelope = new { header, payload };

            // 4) Serialize to JSON with camel-case and indentation
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Serialize(envelope, options);
        }

        // Helper to pick a random element from an array
        private static T Pick<T>(T[] array) => array[_rnd.Next(array.Length)];
    }
}
