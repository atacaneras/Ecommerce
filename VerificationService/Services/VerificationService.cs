using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using Shared.Infrastructure.Repository;
using System.Text.Json;
using VerificationService.DTOs;
using OrderService.DTOs;
using VerificationService.Models;

namespace VerificationService.Services
{
    public class VerificationServiceImpl : IVerificationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VerificationServiceImpl> _logger;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IRepository<VerificationRequestLog> _verificationLogRepository; // LOG REPOSITORY EKLENDİ

        public VerificationServiceImpl(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<VerificationServiceImpl> logger,
            IMessagePublisher messagePublisher,
            IRepository<VerificationRequestLog> verificationLogRepository) // LOG REPOSITORY ENJEKTE EDİLDİ
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _messagePublisher = messagePublisher;
            _verificationLogRepository = verificationLogRepository;
        }

        public async Task<bool> ApproveOrderAsync(Guid orderId)
        {
            var orderServiceUrl = _configuration["Services:OrderService"];
            var orderClient = _httpClientFactory.CreateClient();
            var success = false;

            // 1. Sipariş detaylarını al
            var orderResponse = await orderClient.GetAsync($"{orderServiceUrl}/api/orders/{orderId}");

            if (!orderResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Sipariş {OrderId} bulunamadı veya OrderService'den alınamadı.", orderId);
                // 1.1. Loglama (Başarısız)
                await LogVerificationRequest(orderId, "OrderNotFound", $"OrderService'den sipariş alınamadı: {orderResponse.ReasonPhrase}");
                return false;
            }

            var orderContent = await orderResponse.Content.ReadAsStringAsync();
            var order = JsonSerializer.Deserialize<OrderResponse>(orderContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (order == null || order.Status != "StockReserved")
            {
                _logger.LogWarning("Sipariş {OrderId} onaylanmaya uygun değil (Status: {Status})", orderId, order?.Status);
                // 1.1. Loglama (Uygun Değil)
                await LogVerificationRequest(orderId, "NotEligible", $"Onay için uygun değil, mevcut durum: {order?.Status}");
                return false;
            }

            // 1.1. Loglama (Başarılı İstek - Başlangıç)
            var log = await LogVerificationRequest(orderId, "Requested", $"Onay işlemi başlatıldı. Toplam Tutar: {order.TotalAmount} ₺", order.CustomerName, order.TotalAmount);

            // 2. Stok Düşüm (Finalization) mesajını yayınla
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

            // 4. Log kaydını güncelle
            log.ProcessedAt = DateTime.UtcNow;

            if (!statusUpdateResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Sipariş {OrderId} durumu PaymentCompleted olarak güncellenemedi.", orderId);
                log.Status = "Failed";
                log.ProcessorNote = $"Durum OrderService'e PaymentCompleted olarak güncellenemedi. Hata: {statusUpdateResponse.ReasonPhrase}";
            }
            else
            {
                _logger.LogInformation("Sipariş {OrderId} durumu başarıyla PaymentCompleted olarak güncellendi", orderId);
                log.Status = "Approved";
                success = true;
            }

            // 5. Müşteriye final bildirim mesajını yayınla
            var notificationMessage = new NotificationMessage
            {
                OrderId = orderId,
                CustomerEmail = order.CustomerEmail,
                Message = $"Siparişiniz #{order.Id} onaylandı ve kargoya hazırlanıyor! Toplam: {order.TotalAmount:F2}₺",
                Type = NotificationType.Both
            };
            await _messagePublisher.PublishAsync("notification-exchange", "notification.send", notificationMessage);

            // 6. Log kaydını kaydet
            await _verificationLogRepository.UpdateAsync(log);

            return success;
        }

        private async Task<VerificationRequestLog> LogVerificationRequest(Guid orderId, string status, string note, string customerName = "N/A", decimal totalAmount = 0)
        {
            var log = new VerificationRequestLog
            {
                OrderId = orderId,
                CustomerName = customerName,
                TotalAmount = totalAmount,
                RequestedAt = DateTime.UtcNow,
                Status = status,
                ProcessorNote = note
            };

            return await _verificationLogRepository.AddAsync(log);
        }
    }
}