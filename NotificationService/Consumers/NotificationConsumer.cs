using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using NotificationService.DTOs;
using NotificationService.Services;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Connections;

namespace NotificationService.Consumers
{
    public class NotificationConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RabbitMQSettings _settings;
        private readonly ILogger<NotificationConsumer> _logger;
        private IConnection? _connection;
        private IModel? _channel;

        public NotificationConsumer(
            IServiceProvider serviceProvider,
            RabbitMQSettings settings,
            ILogger<NotificationConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _settings = settings;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(5000, stoppingToken); // RabbitMQ'nun başlamasını bekle

            var factory = new ConnectionFactory
            {
                HostName = _settings.Host,
                UserName = _settings.Username,
                Password = _settings.Password,
                AutomaticRecoveryEnabled = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare("notification-exchange", ExchangeType.Direct, durable: true);
            _channel.QueueDeclare("notification-queue", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind("notification-queue", "notification-exchange", "notification.send");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var notificationMessage = JsonSerializer.Deserialize<NotificationMessage>(message);

                    if (notificationMessage != null)
                    {
                        _logger.LogInformation("{OrderId} siparişi için bildirim mesajı alındı", notificationMessage.OrderId);

                        using var scope = _serviceProvider.CreateScope();
                        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                        var request = new SendNotificationRequest
                        {
                            OrderId = notificationMessage.OrderId,
                            CustomerName = notificationMessage.CustomerName,
                            Email = notificationMessage.CustomerEmail,
                            Phone = notificationMessage.CustomerPhone,
                            Message = notificationMessage.Message,
                            Type = notificationMessage.Type.ToString(),
                            ShouldSendImmediately = notificationMessage.ShouldSendImmediately
                        };

                        var success = await notificationService.SendNotificationAsync(request);

                        if (success)
                        {
                            _channel.BasicAck(ea.DeliveryTag, false);
                            _logger.LogInformation("{OrderId} siparişi için bildirim başarıyla gönderildi", notificationMessage.OrderId);
                        }
                        else
                        {
                            _channel.BasicNack(ea.DeliveryTag, false, true);
                            _logger.LogWarning("{OrderId} siparişi için bildirim gönderilemedi. Mesaj kuyruğa tekrar eklendi.", notificationMessage.OrderId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Bildirim mesajı işlenirken hata oluştu");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume("notification-queue", false, consumer);
            _logger.LogInformation("Bildirim tüketici servisi başlatıldı");

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
