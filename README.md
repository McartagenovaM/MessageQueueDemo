# MessageQueueDemo

A .NET 9 demo showing a resilient RabbitMQ publisher & multiple consumers (console app, Worker service & Azure Function) using Polly for retry and circuit-breaker policies.

---

## ⚙️ Architecture

- **Publisher** (`MqPublisherConsole`)  
  - Sends four types of events at random:  
    1. `NewCustomer`  
    2. `InvoiceCreated`  
    3. `PaymentReceived`  
    4. `ProductDelivered`  
  - Publishes both to a direct queue and a fanout exchange  
  - Applies **Polly** resilience to connection and publish operations  

- **Consumer 1** (`MqConsumer1`) – Console App  
  - Binds to the fanout exchange  
  - Parses each message, simulates business logic per event type  
  - Uses Polly for resilient connection & channel creation  

- **Consumer 2** (`MqWorkerConsumer1`) – .NET 9 Worker Service  
  - Identical to Consumer 1 but implemented as a `BackgroundService`  
  - Ideal for long-running or heavy-duty background processing  

- **Azure Function** (`MQTriggerFuncConsumer`)  
  - Triggered by RabbitMQ messages on the direct queue  
  - Wraps message handling in Polly retry & circuit-breaker policies  

---

## 🚀 Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)  
- [Docker](https://www.docker.com/) (to run RabbitMQ locally)  
- (Optional) [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local)  

---

## 🐇 Running RabbitMQ Locally

```bash
docker run -d \
  --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  -e RABBITMQ_DEFAULT_USER=guest \
  -e RABBITMQ_DEFAULT_PASS=guest \
  rabbitmq:3-management


5672: AMQP port

15672: Management UI (http://localhost:15672)

⚙️ Configuration
Publisher & Consumers

ConnectionFactory.HostName → localhost

Azure Function

Edit MQTriggerFuncConsumer/local.settings.json:

jsonc
Copiar
Editar
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "RabbitMQConnection": "amqp://guest:guest@localhost:5672/"
  }
}
▶️ MqPublisherConsole
cd MqPublisherConsole

dotnet build

dotnet run

Sends a random event every 2–4 seconds

Press ESC to exit

▶️ MqConsumer1 (Console App)
cd MqConsumer1

dotnet build

dotnet run

Listens on its fanout-bound queue

Parses and simulates handling per messageType

▶️ MqWorkerConsumer1 (Worker Service)
cd MqWorkerConsumer1

dotnet build

dotnet run

BackgroundService that consumes the same fanout exchange

Suitable for heavy or long-running tasks

▶️ MQTriggerFuncConsumer (Azure Function)
Open in VS 2022 or VS Code

Configure local.settings.json as above

func start (or F5 in IDE)

Function ProcesarMensajeRabbit fires on each direct-queue message

🔧 Polly Resilience Patterns
Connection policies

Retry ×5 (exponential back-off: 2 s, 4 s, 8 s…)

Circuit-breaker: 3 consecutive faults → 30 s open

Channel/publish policies

Retry ×3 (fixed 1 s back-off)

Circuit-breaker: 2 consecutive faults → 15 s open

📂 Folder Structure
Copiar
Editar
MessageQueueDemo/
├─ MqPublisherConsole/
├─ MqConsumer1/
├─ MqWorkerConsumer1/
├─ MQTriggerFuncConsumer/
└─ README.md
🤝 Contributing
Fork the repo

Create your feature branch

Commit your changes

Open a Pull Request

📄 License
This project is licensed under the MIT License.
