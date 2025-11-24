using OrderService.Models;
using VerificationService.DTOs;

namespace VerificationService.Services
{
    public interface IVerificationService
    {
        Task<bool> ApproveOrderAsync(Guid orderId);
        Task AddPendingOrder(Order order);
        Task<Order?> GetPendingOrderAsync(Guid orderId);
    }
}