using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using StockService.Data;
using StockService.Models;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace StockService.Consumers
{
    public class StockOrderApprovedConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RabbitMQSettings _settings;
        private readonly ILogger<StockOrderApprovedConsumer> _logger;
        private IConnection? _connection;
        private IModel? _channel;

        public StockOrderApprovedConsumer(IServiceProvider serviceProvider, RabbitMQSettings settings, ILogger<StockOrderApprovedConsumer> logger)
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

            _channel.ExchangeDeclare("verification-exchange", ExchangeType.Direct, durable: true);
            _channel.QueueDeclare("stock-verification-queue", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind("stock-verification-queue", "verification-exchange", "order.approved");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<StockDbContext>();

                try
                {
                    var body = ea.Body.ToArray();
                    var message = JsonSerializer.Deserialize<OrderApprovedMessage>(Encoding.UTF8.GetString(body));

                    if (message != null)
                    {
                        _logger.LogInformation("Sipariş {OrderId} için onay alındı, stok kesinleştiriliyor", message.OrderId);

                        // Bu sipariş için daha önce yapılan rezervasyonları bul
                        var transactions = await dbContext.StockTransactions
                            .Where(t => t.OrderId == message.OrderId && t.Type == StockTransactionType.Sale)
                            .ToListAsync();

                        if (transactions.Any())
                        {
                            foreach (var tx in transactions)
                            {
                                var product = await dbContext.Products.FindAsync(tx.ProductId);
                                if (product != null)
                                {

                                    if (product.ReservedQuantity >= tx.Quantity)
                                    {
                                        product.ReservedQuantity -= tx.Quantity;
                                        product.StockQuantity -= tx.Quantity;
                                        product.UpdatedAt = DateTime.UtcNow;

                                        _logger.LogInformation(
                                            "Ürün {ProductId} için stok kesinleştirildi. " +
                                            "Düşülen miktar: {Quantity}, " +
                                            "Yeni Stok: {Stock}, Yeni Rezerve: {Reserved}",
                                            product.Id, tx.Quantity, product.StockQuantity, product.ReservedQuantity);
                                    }
                                    else
                                    {
                                        _logger.LogWarning(
                                            "Ürün {ProductId} için rezerve stok yetersiz. " +
                                            "Beklenen: {Expected}, Mevcut: {Available}",
                                            product.Id, tx.Quantity, product.ReservedQuantity);
                                    }
                                }
                            }
                            await dbContext.SaveChangesAsync();
                            _logger.LogInformation("Sipariş {OrderId} için stok başarıyla kesinleştirildi", message.OrderId);
                        }
                        else
                        {
                            _logger.LogWarning("Sipariş {OrderId} için stok transaction bulunamadı", message.OrderId);
                        }
                    }
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Stok onayı işlenirken hata oluştu.");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume("stock-verification-queue", false, consumer);
            _logger.LogInformation("Stok onay tüketicisi başlatıldı");

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