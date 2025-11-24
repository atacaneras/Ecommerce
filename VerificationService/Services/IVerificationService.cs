using Shared.Infrastructure.Messaging.Messages;
using VerificationService.DTOs;

namespace VerificationService.Services
{
    public interface IVerificationService
    {
        Task<VerificationResponse?> ApproveOrderAsync(Guid orderId, string? approvedBy);
        Task<VerificationResponse?> RejectOrderAsync(Guid orderId, string reason);
        Task<IEnumerable<VerificationResponse>> GetPendingVerificationsAsync();
        Task<IEnumerable<VerificationResponse>> GetAllVerificationsAsync();
        Task<VerificationResponse?> GetVerificationByOrderIdAsync(Guid orderId);
        Task CreateVerificationRequestAsync(OrderApprovedMessage message);
        Task<VerificationResponse> CreateVerificationAsync(Guid orderId, string customerName, decimal totalAmount);
        Task<bool> ApproveVerificationAsync(Guid orderId);
    }
}