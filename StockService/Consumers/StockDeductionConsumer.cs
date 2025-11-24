// StockService/Consumers/StockDeductionConsumer.cs (CORRECTED)

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using StockService.DTOs;
using StockService.Services;
using System.Text;
using System.Text.Json;
using System.Linq;
using System;

namespace StockService.Consumers
{
    public class StockDeductionConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RabbitMQSettings _settings;
        private readonly ILogger<StockDeductionConsumer> _logger;
        private IConnection? _connection;
        private IModel? _channel;

        public StockDeductionConsumer(
            IServiceProvider serviceProvider,
            RabbitMQSettings settings,
            ILogger<StockDeductionConsumer> logger)
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

            _channel.ExchangeDeclare("stock-exchange", ExchangeType.Direct, durable: true);
            _channel.QueueDeclare("stock-deduct-queue", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind("stock-deduct-queue", "stock-exchange", "stock.deduct");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    // CRITICAL: Doğru mesaj tipi StockUpdateMessage kullanılmalı
                    var stockMessage = JsonSerializer.Deserialize<StockUpdateMessage>(message);

                    if (stockMessage != null)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var stockService = scope.ServiceProvider.GetRequiredService<IStockService>();

                        // FinalizeStockAsync için UpdateStockRequest DTO'su oluşturuldu
                        var request = new UpdateStockRequest
                        {
                            OrderId = stockMessage.OrderId,
                            Items = stockMessage.Items.Select(i => new StockItemRequest
                            {
                                ProductId = i.ProductId,
                                Quantity = i.Quantity
                            }).ToList()
                        };

                        // CRITICAL: FinalizeStockAsync metodu çağrıldı. CS1501 hatasını çözer.
                        var success = await stockService.FinalizeStockAsync(request);

                        if (success)
                        {
                            _channel.BasicAck(ea.DeliveryTag, false);
                            _logger.LogInformation("Sipariş {OrderId} için stok düşümü başarıyla işlendi", stockMessage.OrderId);
                        }
                        else
                        {
                            _channel.BasicNack(ea.DeliveryTag, false, true);
                            _logger.LogWarning("Sipariş {OrderId} için stok düşümü işlenemedi. Mesaj tekrar kuyruğa eklendi.", stockMessage.OrderId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Stok düşüm mesajı işlenirken hata oluştu");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume("stock-deduct-queue", false, consumer);
            _logger.LogInformation("Stok düşüm tüketici servisi başlatıldı");

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