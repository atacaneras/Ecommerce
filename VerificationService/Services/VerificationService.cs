using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using Shared.Infrastructure.Repository;
using VerificationService.DTOs;
using VerificationService.Models;

namespace VerificationService.Services
{
    public class VerificationServiceImpl : IVerificationService
    {
        private readonly IRepository<Verification> _repository;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VerificationServiceImpl> _logger;

        public VerificationServiceImpl(
            IRepository<Verification> repository,
            IMessagePublisher messagePublisher,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<VerificationServiceImpl> logger)
        {
            _repository = repository;
            _messagePublisher = messagePublisher;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<VerificationResponse> CreateVerificationAsync(Guid orderId, string customerName, decimal totalAmount)
        {
            try
            {
                var exists = await _repository.ExistsAsync(v => v.OrderId == orderId);
                if (exists)
                {
                    _logger.LogInformation("Doğrulama zaten mevcut: {OrderId}", orderId);
                    var existing = (await _repository.FindAsync(v => v.OrderId == orderId)).FirstOrDefault();
                    return MapToResponse(existing!);
                }

                var verification = new Verification
                {
                    OrderId = orderId,
                    CustomerName = customerName,
                    TotalAmount = totalAmount,
                    Status = VerificationStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                await _repository.AddAsync(verification);
                _logger.LogInformation("Yeni doğrulama kaydı oluşturuldu: {OrderId}", orderId);

                return MapToResponse(verification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Doğrulama oluşturulurken hata: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<bool> ApproveVerificationAsync(Guid orderId)
        {
            try
            {
                var verifications = await _repository.FindAsync(v => v.OrderId == orderId);
                var verification = verifications.FirstOrDefault();

                if (verification == null)
                {
                    _logger.LogWarning("Doğrulama kaydı bulunamadı: {OrderId}", orderId);
                    return false;
                }

                if (verification.Status != VerificationStatus.Pending)
                {
                    _logger.LogWarning("Sipariş zaten işlenmiş: {OrderId}, Durum: {Status}", orderId, verification.Status);
                    return false;
                }

                verification.Status = VerificationStatus.Approved;
                verification.VerifiedAt = DateTime.UtcNow;
                verification.ApprovedBy = "Admin";

                await _repository.UpdateAsync(verification);
                _logger.LogInformation("Doğrulama onaylandı: {OrderId}", orderId);

                // 1. OrderService'e Approved mesajı gönder
                await _messagePublisher.PublishAsync(
                    "verification-exchange",
                    "order.approved",
                    new OrderApprovedMessage
                    {
                        OrderId = orderId,
                        CustomerName = verification.CustomerName,
                        TotalAmount = verification.TotalAmount
                    }
                );
                _logger.LogInformation("OrderService'e onay mesajı gönderildi: {OrderId}", orderId);

                // 2. OrderService HTTP API ile durumu güncelle
                var orderServiceUrl = _configuration["Services:OrderService"] ?? "http://order-service";
                var httpClient = _httpClientFactory.CreateClient();

                var updateStatusRequest = new
                {
                    Status = "Approved"
                };

                var content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(updateStatusRequest),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                try
                {
                    var response = await httpClient.PutAsync(
                        $"{orderServiceUrl}/api/orders/status/{orderId}",
                        content
                    );

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("OrderService sipariş durumu güncellendi: {OrderId} -> Approved", orderId);
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning("OrderService sipariş durumu güncellenemedi: {OrderId}, Hata: {Error}",
                            orderId, errorContent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OrderService'e HTTP isteği başarısız: {OrderId}", orderId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Doğrulama onaylanırken hata: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<IEnumerable<VerificationResponse>> GetPendingVerificationsAsync()
        {
            var pending = await _repository.FindAsync(v => v.Status == VerificationStatus.Pending);
            return pending.Select(MapToResponse).OrderByDescending(v => v.CreatedAt);
        }

        public async Task<VerificationResponse?> ApproveOrderAsync(Guid orderId, string? approvedBy)
        {
            var item = (await _repository.FindAsync(v => v.OrderId == orderId)).FirstOrDefault();
            if (item == null) return null;

            item.Status = VerificationStatus.Approved;
            item.VerifiedAt = DateTime.UtcNow;
            item.ApprovedBy = approvedBy;

            await _repository.UpdateAsync(item);
            return MapToResponse(item);
        }

        public async Task<VerificationResponse?> RejectOrderAsync(Guid orderId, string reason)
        {
            var item = (await _repository.FindAsync(v => v.OrderId == orderId)).FirstOrDefault();
            if (item == null) return null;

            item.Status = VerificationStatus.Rejected;
            item.RejectReason = reason;
            item.VerifiedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(item);
            return MapToResponse(item);
        }

        public async Task<bool> CancelOrderAsync(Guid orderId)
        {
            try
            {
                var item = (await _repository.FindAsync(v => v.OrderId == orderId)).FirstOrDefault();
                if (item == null)
                {
                    _logger.LogWarning("Doğrulama kaydı bulunamadı: {OrderId}", orderId);
                    return false;
                }

                item.Status = VerificationStatus.Rejected;
                item.RejectReason = "Sipariş iptal edildi";
                item.VerifiedAt = DateTime.UtcNow;

                await _repository.UpdateAsync(item);
                _logger.LogInformation("Sipariş {OrderId} için doğrulama iptal edildi", orderId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş {OrderId} iptal edilirken hata oluştu", orderId);
                return false;
            }
        }

        public async Task<IEnumerable<VerificationResponse>> GetAllVerificationsAsync()
        {
            var list = await _repository.GetAllAsync();
            return list.Select(MapToResponse).OrderByDescending(v => v.CreatedAt);
        }

        public async Task<VerificationResponse?> GetVerificationByOrderIdAsync(Guid orderId)
        {
            var result = (await _repository.FindAsync(v => v.OrderId == orderId)).FirstOrDefault();
            return result == null ? null : MapToResponse(result);
        }

        public async Task CreateVerificationRequestAsync(OrderApprovedMessage message)
        {
            await CreateVerificationAsync(message.OrderId, message.CustomerName ?? "Unknown", message.TotalAmount);
        }

        private VerificationResponse MapToResponse(Verification v)
        {
            return new VerificationResponse
            {
                Id = v.Id,
                OrderId = v.OrderId,
                CustomerName = v.CustomerName,
                TotalAmount = v.TotalAmount,
                Status = v.Status.ToString(),
                CreatedAt = v.CreatedAt
            };
        }
    }
}