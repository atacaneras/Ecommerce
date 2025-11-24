using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using VerificationService.Services;
using System.Text;
using System.Text.Json;
using OrderService.Models;
using Microsoft.AspNetCore.Connections;

namespace VerificationService.Consumers
{
    public class VerificationConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RabbitMQSettings _settings;
        private readonly ILogger<VerificationConsumer> _logger;
        private IConnection? _connection;
        private IModel? _channel;

        public VerificationConsumer(
            IServiceProvider serviceProvider,
            RabbitMQSettings settings,
            ILogger<VerificationConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _settings = settings;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(5000, stoppingToken);

            var factory = new ConnectionFactory
            {
                HostName = _settings.Host,
                UserName = _settings.Username,
                Password = _settings.Password,
                AutomaticRecoveryEnabled = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // OrderService tarafından kullanılan exchange'i tanımla
            _channel.ExchangeDeclare("verification-exchange", ExchangeType.Direct, durable: true);
            _channel.QueueDeclare("verification-queue", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind("verification-queue", "verification-exchange", "verification.reserve");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var verificationMessage = JsonSerializer.Deserialize<VerificationMessage>(message);

                    if (verificationMessage != null)
                    {
                        _logger.LogInformation("Sipariş {OrderId} için doğrulama mesajı alındı", verificationMessage.OrderId);

                        using var scope = _serviceProvider.CreateScope();
                        var verificationService = scope.ServiceProvider.GetRequiredService<IVerificationService>();

                        // Stok rezervasyonunu tetikle (StockService'e mesaj gönder)
                        var stockReservationMessage = new StockUpdateMessage
                        {
                            OrderId = verificationMessage.OrderId,
                            Items = verificationMessage.Items.Select(i => new StockItem { ProductId = i.ProductId, Quantity = i.Quantity }).ToList()
                        };

                        var messagePublisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
                        await messagePublisher.PublishAsync("stock-exchange", "verification.reserve", stockReservationMessage);

                        // OrderService'te durumu StockReserved olarak güncelleyelim.
                        // OrderService'e durum güncelleme API çağrısı veya mesajı gereklidir.
                        // Şimdilik OrderService'in API'sini simüle edelim.

                        // OrderService'i doğrudan çağırmak yerine bir mesaj atıyoruz (OrderService'te bu consumer olmalı)

                        // Simülasyon: OrderService'in durumu StockReserved olarak güncellediğini varsayıyoruz.
                        // Onaylama havuzuna ekle
                        var order = new Order
                        {
                            Id = verificationMessage.OrderId,
                            CustomerName = verificationMessage.CustomerName,
                            CustomerEmail = verificationMessage.CustomerEmail,
                            Status = OrderStatus.StockReserved, // Varsayılan durum
                            Items = verificationMessage.Items.Select(i => new OrderItem
                            {
                                ProductId = i.ProductId,
                                Quantity = i.Quantity,
                                ProductName = "" // Eksik veriler simülasyon amacıyla boş bırakıldı
                            }).ToList()
                        };
                        await verificationService.AddPendingOrder(order);

                        _channel.BasicAck(ea.DeliveryTag, false);
                        _logger.LogInformation("Sipariş {OrderId} için rezervasyon başlatıldı ve onay havuzuna eklendi", verificationMessage.OrderId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Doğrulama mesajı işlenirken hata oluştu");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume("verification-queue", false, consumer);
            _logger.LogInformation("Doğrulama tüketici servisi başlatıldı");

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