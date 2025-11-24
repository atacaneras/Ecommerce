using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using OrderService.Data;
using OrderService.Models;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrderService.Consumers
{
    public class OrderApprovedConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RabbitMQSettings _settings;
        private readonly ILogger<OrderApprovedConsumer> _logger;
        private IConnection? _connection;
        private IModel? _channel;

        public OrderApprovedConsumer(IServiceProvider serviceProvider, RabbitMQSettings settings, ILogger<OrderApprovedConsumer> logger)
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

            // Verification exchange'ini dinle
            _channel.ExchangeDeclare("verification-exchange", ExchangeType.Direct, durable: true);
            _channel.QueueDeclare("order-verification-queue", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind("order-verification-queue", "verification-exchange", "order.approved");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

                try
                {
                    var body = ea.Body.ToArray();
                    var message = JsonSerializer.Deserialize<OrderApprovedMessage>(Encoding.UTF8.GetString(body));

                    if (message != null)
                    {
                        var order = await dbContext.Orders.FindAsync(message.OrderId);
                        if (order != null && order.Status == OrderStatus.Pending || order.Status == OrderStatus.StockReserved)
                        {
                            order.Status = OrderStatus.Approved;
                            order.UpdatedAt = DateTime.UtcNow;
                            await dbContext.SaveChangesAsync();
                            _logger.LogInformation("Sipariş {OrderId} durumu APPROVED olarak güncellendi.", message.OrderId);
                        }
                    }
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Sipariş onayı işlenirken hata oluştu.");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume("order-verification-queue", false, consumer);
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