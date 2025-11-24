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

            // Kuyrukları tanımla ve bağla
            _channel.QueueDeclare("stock-reservation-queue", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind("stock-reservation-queue", "stock-exchange", "verification.reserve"); // Rezervasyon mesajları

            _channel.QueueDeclare("stock-finalization-queue", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind("stock-finalization-queue", "stock-exchange", "order.approved"); // Onay mesajları

            _logger.LogInformation("Stok Tüketici Servisi Başlatılıyor: Rezervasyon ve Kesinleştirme kuyrukları dinleniyor.");


            // 1. Rezervasyon Tüketicisi (verification.reserve)
            var reserveConsumer = new EventingBasicConsumer(_channel);
            reserveConsumer.Received += async (model, ea) =>
            {
                if (ea.RoutingKey != "verification.reserve") return;

                await ProcessMessage(ea, isFinalize: false);
            };
            _channel.BasicConsume("stock-reservation-queue", false, reserveConsumer);

            // 2. Kesinleştirme Tüketicisi (order.approved)
            var finalizeConsumer = new EventingBasicConsumer(_channel);
            finalizeConsumer.Received += async (model, ea) =>
            {
                if (ea.RoutingKey != "order.approved") return;

                await ProcessMessage(ea, isFinalize: true);
            };
            _channel.BasicConsume("stock-finalization-queue", false, finalizeConsumer);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task ProcessMessage(BasicDeliverEventArgs ea, bool isFinalize)
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            StockUpdateMessage? stockMessage = null;

            try
            {
                // Mesaj tipine göre deserialization yapın
                if (isFinalize)
                {
                    var approvedMessage = JsonSerializer.Deserialize<OrderApprovedMessage>(message);
                    if (approvedMessage == null) throw new ArgumentNullException("OrderApprovedMessage is null");

                    stockMessage = new StockUpdateMessage
                    {
                        OrderId = approvedMessage.OrderId,
                        Items = approvedMessage.Items.Select(i => new Shared.Infrastructure.Messaging.Messages.StockItem { ProductId = i.ProductId, Quantity = i.Quantity }).ToList()
                    };
                }
                else
                {
                    // VerificationMessage yapısı StockUpdateMessage ile aynı Item yapısını kullanmıyor. 
                    // Ancak VerificationService mesajı StockUpdateMessage'a çevirip gönderiyorsa bu satır çalışır.
                    // Varsayılan olarak VerificationMessage'ı StockUpdateMessage'a dönüştürmek için StockService.DTOs kullanılabilir.
                    var verificationMessage = JsonSerializer.Deserialize<VerificationMessage>(message);
                    if (verificationMessage == null) throw new ArgumentNullException("VerificationMessage is null");

                    stockMessage = new StockUpdateMessage
                    {
                        OrderId = verificationMessage.OrderId,
                        Items = verificationMessage.Items.Select(i => new Shared.Infrastructure.Messaging.Messages.StockItem { ProductId = i.ProductId, Quantity = i.Quantity }).ToList()
                    };
                }

                if (stockMessage == null) throw new ArgumentNullException("stockMessage conversion failed");

                _logger.LogInformation("Sipariş {OrderId} için stok mesajı alındı. İşlem: {Action}", stockMessage.OrderId, isFinalize ? "Kesinleştirme" : "Rezervasyon");

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

                bool success = isFinalize
                    ? await stockService.FinalizeStockAsync(request)
                    : await stockService.ReserveStockAsync(request);

                if (success)
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                    _logger.LogInformation("Sipariş {OrderId} için stok {Action} başarıyla tamamlandı", stockMessage.OrderId, isFinalize ? "kesinleştirme" : "rezervasyon");
                }
                else
                {
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                    _logger.LogWarning("Sipariş {OrderId} için stok {Action} başarısız oldu. Mesaj kuyruğa tekrar eklendi.", stockMessage.OrderId, isFinalize ? "kesinleştirme" : "rezervasyon");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stok mesajı işlenirken hata oluştu");
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
        }


        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}