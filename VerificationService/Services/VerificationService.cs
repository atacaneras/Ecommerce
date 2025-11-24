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
        private readonly ILogger<VerificationServiceImpl> _logger;

        public VerificationServiceImpl(
            IRepository<Verification> repository,
            IMessagePublisher messagePublisher,
            ILogger<VerificationServiceImpl> logger)
        {
            _repository = repository;
            _messagePublisher = messagePublisher;
            _logger = logger;
        }

        public async Task<VerificationResponse> CreateVerificationAsync(Guid orderId, string customerName, decimal totalAmount)
        {
            try
            {
                var exists = await _repository.ExistsAsync(v => v.OrderId == orderId);
                if (exists)
                {
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
                return MapToResponse(verification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Doğrulama hatası: {OrderId}", orderId);
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
                    return false;

                verification.Status = VerificationStatus.Approved;
                verification.VerifiedAt = DateTime.UtcNow;

                await _repository.UpdateAsync(verification);

                await _messagePublisher.PublishAsync(
                    "verification-exchange",
                    "order.approved",
                    new OrderApprovedMessage { OrderId = orderId }
                );

                return true;
            }
            catch
            {
                throw;
            }
        }

        public async Task<IEnumerable<VerificationResponse>> GetPendingVerificationsAsync()
        {
            var pending = await _repository.FindAsync(v => v.Status == VerificationStatus.Pending);
            return pending.Select(MapToResponse);
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

            await _repository.UpdateAsync(item);
            return MapToResponse(item);
        }

        public async Task<IEnumerable<VerificationResponse>> GetAllVerificationsAsync()
        {
            var list = await _repository.GetAllAsync();
            return list.Select(MapToResponse);
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
