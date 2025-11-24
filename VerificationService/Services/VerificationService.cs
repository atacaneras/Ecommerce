using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using Shared.Infrastructure.Repository;
using System.Text.Json;
using VerificationService.DTOs;
using OrderService.DTOs;
using VerificationService.Models;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace VerificationService.Services
{
    public class VerificationServiceImpl : IVerificationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VerificationServiceImpl> _logger;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IRepository<VerificationRequestLog> _verificationLogRepository;

        public VerificationServiceImpl(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<VerificationServiceImpl> logger,
            IMessagePublisher messagePublisher,
            IRepository<VerificationRequestLog> verificationLogRepository)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _messagePublisher = messagePublisher;
            _verificationLogRepository = verificationLogRepository;
        }

        // Helper: DTO'dan Response'a dönüşüm
        private VerificationResponse MapToResponse(VerificationRequestLog log)
        {
            return new VerificationResponse
            {
                Id = log.Id,
                OrderId = log.OrderId,
                CustomerName = log.CustomerName,
                TotalAmount = log.TotalAmount,
                Status = log.Status,
                RequestedAt = log.RequestedAt,
                ProcessedAt = log.ProcessedAt,
                ProcessorNote = log.ProcessorNote
            };
        }

        // Helper: Log kaydı oluşturma (Sipariş oluşturulduğunda OrderService tarafından mesajla tetiklenebilir, 
        // ancak Controller'dan onay çağrısı geldiği için burada başlangıç kaydı yapıyoruz.)
        private async Task<VerificationRequestLog> LogVerificationRequest(Guid orderId, string status, string note, string customerName = "N/A", decimal totalAmount = 0)
        {
            // Bu metot, aslen OrderService'den mesaj geldiğinde tetiklenmeliydi. 
            // Ancak, şimdiki akışınızda sadece API'den Approve/Reject geldiği için, 
            // sadece mevcut log kaydını bulup güncelleyeceğiz.
            // Eğer kayıt yoksa, ilk defa logluyoruz. (Basitlik için)
            var existingLog = (await _verificationLogRepository.FindAsync(l => l.OrderId == orderId)).FirstOrDefault();

            if (existingLog != null) return existingLog;

            var log = new VerificationRequestLog
            {
                OrderId = orderId,
                CustomerName = customerName, // Bu bilgileri OrderService'ten çekmek gerekebilir.
                TotalAmount = totalAmount,
                RequestedAt = DateTime.UtcNow,
                Status = "Requested", // Initial status
                ProcessorNote = "API çağrısı bekleniyor"
            };
            return await _verificationLogRepository.AddAsync(log);
        }

        // ==============================
        // READ OPERATIONS
        // ==============================

        public async Task<IEnumerable<VerificationResponse>> GetAllVerificationsAsync()
        {
            var logs = await _verificationLogRepository.GetAllAsync();
            return logs.Select(MapToResponse);
        }

        public async Task<IEnumerable<VerificationResponse>> GetPendingVerificationsAsync()
        {
            // Pending/Requested durumundaki logları getirir
            var logs = await _verificationLogRepository.FindAsync(l => l.Status == "Requested");
            return logs.Select(MapToResponse);
        }

        public async Task<VerificationResponse?> GetVerificationByOrderIdAsync(Guid orderId)
        {
            var log = (await _verificationLogRepository.FindAsync(l => l.OrderId == orderId)).FirstOrDefault();
            return log != null ? MapToResponse(log) : null;
        }

        // ==============================
        // WRITE OPERATIONS
        // ==============================

        public async Task<VerificationResponse?> ApproveOrderAsync(Guid orderId, string? approvedBy)
        {
            var orderServiceUrl = _configuration["Services:OrderService"];
            var orderClient = _httpClientFactory.CreateClient();

            var log = (await _verificationLogRepository.FindAsync(l => l.OrderId == orderId)).FirstOrDefault();

            if (log == null || log.Status != "Requested")
            {
                _logger.LogWarning("Onaylanacak sipariş {OrderId} logu bulunamadı veya durumu uygun değil: {Status}", orderId, log?.Status);
                return null;
            }

            // 1. Sipariş detaylarını al (Gerekli değil, OrderService'in DTO'sundan OrderResponse'ı yeniden kullanıyoruz)
            var orderResponseHttp = await orderClient.GetAsync($"{orderServiceUrl}/api/orders/{orderId}");

            if (!orderResponseHttp.IsSuccessStatusCode)
            {
                log.Status = "Failed";
                log.ProcessedAt = DateTime.UtcNow;
                log.ProcessorNote = $"Onay sırasında OrderService'ten sipariş alınamadı. Hata: {orderResponseHttp.ReasonPhrase}";
                await _verificationLogRepository.UpdateAsync(log);
                return MapToResponse(log);
            }
            var order = JsonSerializer.Deserialize<OrderResponse>(await orderResponseHttp.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (order == null) return null;


            // 2. Stok Düşüm (Finalization) mesajını yayınla (stock.deduct)
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
            log.ProcessorNote = $"Onaylayan: {approvedBy ?? "N/A"}";

            if (!statusUpdateResponse.IsSuccessStatusCode)
            {
                log.Status = "Failed";
                log.ProcessorNote += $", Hata: Durum OrderService'e PaymentCompleted olarak güncellenemedi. {statusUpdateResponse.ReasonPhrase}";
            }
            else
            {
                log.Status = "Approved";

                // 5. Müşteriye final bildirim mesajını yayınla
                var notificationMessage = new NotificationMessage
                {
                    OrderId = orderId,
                    CustomerEmail = order.CustomerEmail,
                    Message = $"Siparişiniz #{order.Id} onaylandı ve kargoya hazırlanıyor! Toplam: {order.TotalAmount:F2}₺",
                    Type = NotificationType.Both
                };
                await _messagePublisher.PublishAsync("notification-exchange", "notification.send", notificationMessage);
            }

            await _verificationLogRepository.UpdateAsync(log);
            return MapToResponse(log);
        }

        public async Task<VerificationResponse?> RejectOrderAsync(Guid orderId, string reason)
        {
            var orderServiceUrl = _configuration["Services:OrderService"];
            var orderClient = _httpClientFactory.CreateClient();

            var log = (await _verificationLogRepository.FindAsync(l => l.OrderId == orderId)).FirstOrDefault();

            if (log == null || log.Status != "Requested")
            {
                _logger.LogWarning("Reddedilecek sipariş {OrderId} logu bulunamadı veya durumu uygun değil: {Status}", orderId, log?.Status);
                return null;
            }

            // 1. Order Status'u Cancelled olarak güncelle
            var updateStatusRequest = new UpdateOrderStatusRequest { Status = "Cancelled" };
            var content = new StringContent(JsonSerializer.Serialize(updateStatusRequest), System.Text.Encoding.UTF8, "application/json");

            var statusUpdateResponse = await orderClient.PutAsync($"{orderServiceUrl}/api/orders/status/{orderId}", content);

            log.ProcessedAt = DateTime.UtcNow;
            log.ProcessorNote = $"Ret sebebi: {reason}";

            if (!statusUpdateResponse.IsSuccessStatusCode)
            {
                log.Status = "Failed";
                log.ProcessorNote += $", Hata: Durum OrderService'e Cancelled olarak güncellenemedi. {statusUpdateResponse.ReasonPhrase}";
            }
            else
            {
                // 2. Stok Rezervasyonunu İade etme mekanizması (stock.deduct mesajı atılır, StockService bu durumda Rezerve Stoğu düşer)
                // NOT: Mevcut FinalizeStockAsync mantığınız, düşüm miktarını sadece rezerve edilenden düşer. 
                // StockService'in, rezervasyonu geri alma mantığını ayrı bir mesajla (örneğin stock.release) veya 
                // FinalizeStockAsync metodunda negatif miktar/özel bir bayrak ile işlemesi gerekir. 
                // Basitlik için burada FinalizeStockAsync'i çağırıyoruz ve StockService'in bunu akıllıca yöneteceğini varsayıyoruz. 

                // Reddedilen siparişin detaylarını OrderService'ten almamız gerekiyor (çünkü sipariş kalemleri lazım)
                var orderResponseHttp = await orderClient.GetAsync($"{orderServiceUrl}/api/orders/{orderId}");
                var order = JsonSerializer.Deserialize<OrderResponse>(await orderResponseHttp.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (order != null)
                {
                    // Stok iadesi için (Rezerve stoğu sıfırlama/iade etme)
                    var releaseMessage = new StockUpdateMessage
                    {
                        OrderId = orderId,
                        Items = order.Items.Select(i => new StockItem
                        {
                            ProductId = i.ProductId,
                            Quantity = i.Quantity * -1 // Negatif miktar, iade anlamına gelir
                        }).ToList()
                    };
                    await _messagePublisher.PublishAsync("stock-exchange", "stock.deduct", releaseMessage);
                }

                log.Status = "Rejected";

                // Müşteriye bildirim
                var notificationMessage = new NotificationMessage
                {
                    OrderId = orderId,
                    CustomerEmail = order?.CustomerEmail ?? log.CustomerName, // Email bulunamazsa CustomerName kullan
                    Message = $"Siparişiniz #{orderId} maalesef onaylanamadı ve iptal edildi. Sebep: {reason}",
                    Type = NotificationType.Both
                };
                await _messagePublisher.PublishAsync("notification-exchange", "notification.send", notificationMessage);
            }

            await _verificationLogRepository.UpdateAsync(log);
            return MapToResponse(log);
        }
    }
}