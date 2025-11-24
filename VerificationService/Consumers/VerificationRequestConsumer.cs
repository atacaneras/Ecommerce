using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using System.Text;
using System.Text.Json;
using VerificationService.Services;

namespace VerificationService.Consumers
{
    public class VerificationRequestConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RabbitMQSettings _settings;
        private readonly ILogger<VerificationRequestConsumer> _logger;
        private IConnection? _connection;
        private IModel? _channel;

        public VerificationRequestConsumer(
            IServiceProvider serviceProvider,
            RabbitMQSettings settings,
            ILogger<VerificationRequestConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _settings = settings;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Servislerin ve RabbitMQ'nun tam olarak ayağa kalkmasını bekle
            await Task.Delay(5000, stoppingToken);

            var factory = new ConnectionFactory
            {
                HostName = _settings.Host,
                UserName = _settings.Username,
                Password = _settings.Password,
                Port = _settings.Port,
                AutomaticRecoveryEnabled = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare("verification-exchange", ExchangeType.Direct, durable: true);
            _channel.QueueDeclare("verification-queue", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind("verification-queue", "verification-exchange", "verification.create");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = JsonSerializer.Deserialize<VerificationRequestMessage>(Encoding.UTF8.GetString(body));

                    if (message != null)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        // Repository yerine Service kullanıyoruz
                        var verificationService = scope.ServiceProvider.GetRequiredService<IVerificationService>();

                        await verificationService.CreateVerificationAsync(
                            message.OrderId,
                            message.CustomerName,
                            message.TotalAmount
                        );
                    }
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Doğrulama talebi işlenirken hata oluştu");
                    _channel.BasicNack(ea.DeliveryTag, false, false);
                }
            };

            _channel.BasicConsume("verification-queue", false, consumer);

            // BackgroundService'in çalışmaya devam etmesi için
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}