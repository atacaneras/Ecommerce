using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using System.Text.Json;
using VerificationService.DTOs;
using OrderService.DTOs; // OrderResponse DTO'su için

namespace VerificationService.Services

    public class VerificationServiceImpl : IVerificationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VerificationServiceImpl> _logger;
        private readonly IMessagePublisher _messagePublisher;

        public VerificationServiceImpl(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<VerificationServiceImpl> logger,
            IMessagePublisher messagePublisher)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _messagePublisher = messagePublisher;
        }

        public async Task<bool> ApproveOrderAsync(Guid orderId)
        {
            var orderServiceUrl = _configuration["Services:OrderService"];
            var orderClient = _httpClientFactory.CreateClient();

            // 1. Sipariş detaylarını al
            // OrderService'ten orderId'ye göre OrderResponse alıyoruz
            var orderResponse = await orderClient.GetAsync($"{orderServiceUrl}/api/orders/{orderId}");

            if (!orderResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Sipariş {OrderId} bulunamadı veya OrderService'den alınamadı.", orderId);
                return false;
            }

            var orderContent = await orderResponse.Content.ReadAsStringAsync();
            var order = JsonSerializer.Deserialize<OrderResponse>(orderContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (order == null || order.Status != "StockReserved")
            {
                _logger.LogWarning("Sipariş {OrderId} onaylanmaya uygun değil (Status: {Status})", orderId, order?.Status);
                return false;
            }

            // 2. Stok Düşüm (Finalization) mesajını yayınla
            // StockService'in consume edeceği yeni bir routing key: stock.deduct
            var deductionMessage = new StockUpdateMessage
            {
                OrderId = orderId,
                Items = order.Items.Select(i => new StockItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList()
            };

            await _messagePublisher.PublishAsync("stock-exchange", "stock.deduct", deductionMessage);
            _logger.LogInformation("Sipariş {OrderId} için stok düşümü (stock.deduct) mesajı yayınlandı", orderId);

            // 3. Order Status'u PaymentCompleted olarak güncelle
            var updateStatusRequest = new UpdateOrderStatusRequest { Status = "PaymentCompleted" };
            var content = new StringContent(JsonSerializer.Serialize(updateStatusRequest), System.Text.Encoding.UTF8, "application/json");

            var statusUpdateResponse = await orderClient.PutAsync($"{orderServiceUrl}/api/orders/status/{orderId}", content);

            if (!statusUpdateResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Sipariş {OrderId} durumu PaymentCompleted olarak güncellenemedi.", orderId);
            }
            else
            {
                _logger.LogInformation("Sipariş {OrderId} durumu başarıyla PaymentCompleted olarak güncellendi", orderId);
            }

            // 4. Müşteriye final bildirim mesajını yayınla
            var notificationMessage = new NotificationMessage
            {
                OrderId = orderId,
                CustomerEmail = order.CustomerEmail,
                Message = $"Siparişiniz #{order.Id} onaylandı ve kargoya hazırlanıyor! Toplam: {order.TotalAmount:F2}₺",
                Type = NotificationType.Both
            };
            await _messagePublisher.PublishAsync("notification-exchange", "notification.send", notificationMessage);

            return statusUpdateResponse.IsSuccessStatusCode;
        }
    }
}