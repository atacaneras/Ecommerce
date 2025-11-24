using Microsoft.AspNetCore.Connections;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using StockService.DTOs;
using StockService.Services;
using System.Text;
using System.Text.Json;

namespace StockService.Consumers
{
    public class StockUpdateConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RabbitMQSettings _settings;
        private readonly ILogger<StockUpdateConsumer> _logger;
        private IConnection? _connection;
        private IModel? _channel;

        public StockUpdateConsumer(
            IServiceProvider serviceProvider,
            RabbitMQSettings settings,
            ILogger<StockUpdateConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _settings = settings;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(5000, stoppingToken); // RabbitMQ'nun hazır olmasını bekle

            var factory = new ConnectionFactory
            {
                HostName = _settings.Host,
                UserName = _settings.Username,
                Password = _settings.Password,
                AutomaticRecoveryEnabled = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare("stock-exchange", ExchangeType.Direct, durable: true);
            _channel.QueueDeclare("stock-queue", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind("stock-queue", "stock-exchange", "stock.update");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var stockMessage = JsonSerializer.Deserialize<StockUpdateMessage>(message);

                    if (stockMessage != null)
                    {
                        _logger.LogInformation("Sipariş {OrderId} için stok güncelleme mesajı alındı", stockMessage.OrderId);

                        using var scope = _serviceProvider.CreateScope();
                        var stockService = scope.ServiceProvider.GetRequiredService<IStockService>();

                        var request = new UpdateStockRequest
                        {
                            OrderId = stockMessage.OrderId,
                            Items = stockMessage.Items.Select(i => new StockItemRequest
                            {
                                ProductId = i.ProductId,
                                Quantity = i.Quantity
                            }).ToList()
                        };

                        var success = await stockService.UpdateStockAsync(request);

                        if (success)
                        {
                            _channel.BasicAck(ea.DeliveryTag, false);
                            _logger.LogInformation("Sipariş {OrderId} için stok başarıyla güncellendi", stockMessage.OrderId);
                        }
                        else
                        {
                            _channel.BasicNack(ea.DeliveryTag, false, true);
                            _logger.LogWarning("Sipariş {OrderId} için stok güncellenemedi. Mesaj tekrar kuyruğa eklendi.", stockMessage.OrderId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Stok güncelleme mesajı işlenirken hata oluştu");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume("stock-queue", false, consumer);
            _logger.LogInformation("Stok güncelleme tüketici servisi başlatıldı");

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
