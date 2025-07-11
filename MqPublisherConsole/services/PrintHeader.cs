// File: services/PrintHeader.cs
using System;
using System.Text.Json;

namespace MqPublisherConsole.services
{
    public static class PrintHeader
    {
        /// <summary>
        /// Parses the "header" block from a JSON message and prints it on a single line.
        /// </summary>
        public static void Print(string messageJson)
        {
            using var doc = JsonDocument.Parse(messageJson);

            if (!doc.RootElement.TryGetProperty("header", out JsonElement header))
            {
                Console.WriteLine("⚠️  Header block not found in the JSON message.");
                return;
            }

            string messageType = header.GetProperty("messageType").GetString() ?? "";
            string correlationId = header.GetProperty("correlationId").GetString() ?? "";
            string sentAt = header.GetProperty("sentAt").GetString() ?? "";
            string source = header.GetProperty("source").GetString() ?? "";

            Console.WriteLine(
                $"{messageType}: " +
                $"{correlationId} " +
                $"CreatedAt: {sentAt}"
            );
        }
    }
}
