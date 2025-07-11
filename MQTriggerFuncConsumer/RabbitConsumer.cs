using System.Text;
using Microsoft.Azure.Functions.Worker;
//using Microsoft.Azure.WebJobs.Extensions.RabbitMQ;  // ← aquí
using Microsoft.Extensions.Logging;
//using RabbitMQ.Client.Events;  // sólo si vas a recibir BasicDeliverEventArgs

namespace MQTriggerFuncConsumer
{
    public class RabbitConsumer
    {
        private readonly ILogger _logger;
        public RabbitConsumer(ILoggerFactory factory)
            => _logger = factory.CreateLogger<RabbitConsumer>();

        [Function("ProcesarMensajeRabbit")]
        public void Run(
            [RabbitMQTrigger(
                queueName: "message_queue-3",                    // tu cola real
                ConnectionStringSetting = "RabbitMQConnection"  // coincide con tu local.settings.json
            )]
            string mensaje,
            FunctionContext context)
        {
            _logger.LogInformation($"🔔 Mensaje recibido: {mensaje}");
        }

        //// Si necesitas cabeceras, propiedades, etc.:
        //[Function("ProcesarConArgs")]
        //public void RunWithArgs(
        //    [RabbitMQTrigger("mi-queue", ConnectionStringSetting = "RabbitMQConnection")]
        //    BasicDeliverEventArgs args,
        //    FunctionContext context)
        //{
        //    var cuerpo = Encoding.UTF8.GetString(args.Body.ToArray());
        //    _logger.LogInformation($"🐇 Con Args: {cuerpo}");
        //}
    }
}
