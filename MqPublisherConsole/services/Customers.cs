// File: services/Customers.cs
using System;
using System.Text.Json;

namespace MqPublisherConsole.services
{
    // DTOs
    public class Address
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
    }

    public class NewCustomer
    {
        public string CustomerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string DocumentNumber { get; set; }
        public string PhoneNumber { get; set; }
        public Address Address { get; set; }
    }

    // Service que arma el envelope completo
    public static class Customers
    {
        private static readonly Random _rnd = new();

        private static readonly string[] FirstNames =
            { "Alice", "Bob", "Charlie", "Diana", "Ethan", "Fiona", "George", "Hannah" };
        private static readonly string[] LastNames =
            { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis" };
        private static readonly string[] Streets =
            { "Main St", "Oak Ave", "Pine Rd", "Maple Dr", "Cedar Ln", "Elm St" };
        private static readonly string[] Cities =
            { "Quito", "Guayaquil", "Cuenca", "Ambato", "Manta" };
        private static readonly string[] Countries =
            { "Ecuador", "Colombia", "Peru", "Chile", "Brazil" };

        /// <summary>
        /// Genera un NewCustomer aleatorio y lo envuelve en
        /// { header: {...}, payload: {...} } serializado a JSON.
        /// </summary>
        public static string GenerateRandomCustomerJson()
        {
            // 1) Genera el payload
            var customer = new NewCustomer
            {
                CustomerId = Guid.NewGuid().ToString(),
                FirstName = Pick(FirstNames),
                LastName = Pick(LastNames),
                DocumentNumber = _rnd.Next(10000000, 99999999).ToString(),
                PhoneNumber = $"{_rnd.Next(100, 999)}-{_rnd.Next(100, 999)}-{_rnd.Next(1000, 9999)}",
                Address = new Address
                {
                    Street = $"{_rnd.Next(100, 9999)} {Pick(Streets)}",
                    City = Pick(Cities),
                    Country = Pick(Countries)
                }
            };

            // 2) Crea el envelope con header
            var envelope = new
            {
                header = new
                {
                    messageType = "NewCustomer",
                    correlationId = Guid.NewGuid().ToString(),
                    sentAt = DateTime.UtcNow.ToString("o"),
                    source = "CustomerManagement"
                },
                payload = customer
            };

            // 3) Serializa todo el envelope
            return JsonSerializer.Serialize(
                envelope,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }

        /// <summary>
        /// Returns an object suitable for the invoiceHeader.customer block,
        /// with random id, name, email, address and postalCode.
        /// </summary>
        public static object GenerateRandomCustomer()
        {
            string first = Pick(FirstNames);
            string last = Pick(LastNames);
            string customerId = Guid.NewGuid().ToString();
            string name = $"{first} {last}";
            string email = $"{first.ToLower()}.{last.ToLower()}@example.com";

            var address = new
            {
                street = $"{_rnd.Next(100, 9999)} {Pick(Streets)}",
                city = Pick(Cities),
                country = Pick(Countries),
                postalCode = _rnd.Next(10000, 99999).ToString()
            };

            return new
            {
                customerId,
                name,
                email,
                address
            };
        }

        

        // Helper para escoger un valor aleatorio de un array
        private static T Pick<T>(T[] array)
            => array[_rnd.Next(array.Length)];
    }
}
