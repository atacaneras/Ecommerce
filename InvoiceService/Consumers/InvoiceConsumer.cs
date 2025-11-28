using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using InvoiceService.DTOs;
using InvoiceService.Services;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Connections;

namespace InvoiceService.Consumers
{
    public class InvoiceConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RabbitMQSettings _settings;
        private readonly ILogger<InvoiceConsumer> _logger;
        private IConnection? _connection;
        private IModel? _channel;

        public InvoiceConsumer(
            IServiceProvider serviceProvider,
            RabbitMQSettings settings,
            ILogger<InvoiceConsumer> logger)
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

            _channel.ExchangeDeclare("verification-exchange", ExchangeType.Direct, durable: true);
            _channel.QueueDeclare("invoice-queue", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind("invoice-queue", "verification-exchange", "order.approved");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var approvedMessage = JsonSerializer.Deserialize<OrderApprovedMessage>(message);

                    if (approvedMessage != null)
                    {
                        _logger.LogInformation("Sipariş {OrderId} onaylandı, fatura oluşturuluyor", approvedMessage.OrderId);

                        using var scope = _serviceProvider.CreateScope();
                        var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

                        // Get order details from OrderService
                        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
                        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                        var httpClient = httpClientFactory.CreateClient();
                        var orderServiceUrl = configuration["Services:OrderService"] ?? "http://order-service";

                        try
                        {
                            var orderResponse = await httpClient.GetAsync($"{orderServiceUrl}/api/orders/{approvedMessage.OrderId}");

                            if (orderResponse.IsSuccessStatusCode)
                            {
                                var orderJson = await orderResponse.Content.ReadAsStringAsync();
                                var orderData = JsonSerializer.Deserialize<OrderData>(orderJson, new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                });

                                if (orderData != null)
                                {
                                    var createInvoiceRequest = new CreateInvoiceRequest
                                    {
                                        OrderId = approvedMessage.OrderId,
                                        CustomerName = orderData.CustomerName,
                                        CustomerEmail = orderData.CustomerEmail,
                                        CustomerPhone = orderData.CustomerPhone ?? "",
                                        Items = orderData.Items.Select(i => new InvoiceItemDto
                                        {
                                            ProductId = i.ProductId,
                                            ProductName = i.ProductName,
                                            Quantity = i.Quantity,
                                            UnitPrice = i.UnitPrice
                                        }).ToList()
                                    };

                                    var invoice = await invoiceService.CreateInvoiceAsync(createInvoiceRequest);
                                    _logger.LogInformation("Sipariş {OrderId} için fatura oluşturuldu: {InvoiceNumber}",
                                        approvedMessage.OrderId, invoice.InvoiceNumber);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Sipariş {OrderId} detayları alınamadı", approvedMessage.OrderId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Sipariş {OrderId} detayları alınırken hata", approvedMessage.OrderId);
                        }
                    }

                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fatura oluşturulurken hata oluştu");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume("invoice-queue", false, consumer);
            _logger.LogInformation("Fatura tüketici servisi başlatıldı");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }

        private class OrderData
        {
            public Guid Id { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public string CustomerEmail { get; set; } = string.Empty;
            public string? CustomerPhone { get; set; }
            public List<OrderItemData> Items { get; set; } = new();
        }

        private class OrderItemData
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }
    }
}