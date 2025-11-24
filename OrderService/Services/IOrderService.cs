using OrderService.DTOs;
using OrderService.Models; 
namespace OrderService.Services
{
    public interface IOrderService
    {
        Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request);
        Task<OrderResponse?> GetOrderByIdAsync(Guid orderId);
        Task<IEnumerable<OrderResponse>> GetAllOrdersAsync();
       Task<OrderResponse?> UpdateOrderStatusAsync(Guid orderId, OrderStatus newStatus);
    }
}