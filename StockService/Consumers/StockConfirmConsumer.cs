using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using Microsoft.EntityFrameworkCore;
using StockService.Data;
using StockService.Models;
using StockService.Services;
using System.Text;
using System.Text.Json;

namespace StockService.Consumers
{
    public class StockConfirmConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RabbitMQSettings _settings;
        private readonly ILogger<StockConfirmConsumer> _logger;
        private IConnection? _connection;
        private IModel? _channel;

        public StockConfirmConsumer(
            IServiceProvider serviceProvider,
            RabbitMQSettings settings,
            ILogger<StockConfirmConsumer> logger)
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
            _channel.QueueDeclare("stock-confirm-queue", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind("stock-confirm-queue", "stock-exchange", "stock.confirm");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var confirmMessage = JsonSerializer.Deserialize<StockConfirmMessage>(message);

                    if (confirmMessage != null)
                    {
                        _logger.LogInformation("Sipariş {OrderId} için stok konfirmasyon mesajı alındı. Onaylandı mı: {Approved}",
                            confirmMessage.OrderId, confirmMessage.Approved);

                        using var scope = _serviceProvider.CreateScope();
                        var stockService = scope.ServiceProvider.GetRequiredService<IStockService>();

                        var success = await stockService.ConfirmStockAsync(confirmMessage.OrderId, confirmMessage.Approved);

                        if (success)
                        {
                            _channel.BasicAck(ea.DeliveryTag, false);
                            _logger.LogInformation("Sipariş {OrderId} için stok konfirmasyonu başarıyla işlendi", confirmMessage.OrderId);
                        }
                        else
                        {
                            _channel.BasicNack(ea.DeliveryTag, false, true);
                            _logger.LogWarning("Sipariş {OrderId} için stok konfirmasyonu işlenemedi", confirmMessage.OrderId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Stok konfirmasyon mesajı işlenirken hata oluştu");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume("stock-confirm-queue", false, consumer);
            _logger.LogInformation("Stok konfirmasyon tüketici servisi başlatıldı");

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