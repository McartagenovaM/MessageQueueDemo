using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace MQTriggerFuncConsumer
{
    public class RabbitConsumer
    {
        private readonly ILogger _logger;
        public RabbitConsumer(ILoggerFactory factory)
            => _logger = factory.CreateLogger<RabbitConsumer>();

        [Function("ProcesarMensajeRabbit")]
        public void Run([RabbitMQTrigger(queueName: "message_queue-3", ConnectionStringSetting = "RabbitMQConnection")] string rcvdMessage, FunctionContext context)
        {
            // 1) Parse the JSON and extract header.messageType
            using var doc = JsonDocument.Parse(rcvdMessage);
            string messageType = doc.RootElement
                                    .GetProperty("header")
                                    .GetProperty("messageType")
                                    .GetString();

            // 2) Your normal processing…
            _logger.LogInformation($"Message reveived...");

            _logger.LogInformation($"🔔 Processing: {messageType}");

            // 3) After persisting to DB (simulated here), log the insert
            //    Replace this comment with your actual DB call…
            //    await _myRepo.InsertAsync(myRecord);

            _logger.LogInformation($"Record of {messageType} added to database");
        }
    }
}
