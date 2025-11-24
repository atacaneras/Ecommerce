using OrderService.Models;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using VerificationService.DTOs;

namespace VerificationService.Services
{
    public class VerificationServiceImpl : IVerificationService
    {
        private static readonly Dictionary<Guid, Order> _pendingOrders = new();
        private readonly IMessagePublisher _messagePublisher;
        private readonly ILogger<VerificationServiceImpl> _logger;

        public VerificationServiceImpl(IMessagePublisher messagePublisher, ILogger<VerificationServiceImpl> logger)
        {
            _messagePublisher = messagePublisher;
            _logger = logger;
        }

        public Task AddPendingOrder(Order order)
        {
            _pendingOrders[order.Id] = order;
            _logger.LogInformation("Sipariş {OrderId} onaylama havuzuna eklendi.", order.Id);
            return Task.CompletedTask;
        }

        public Task<Order?> GetPendingOrderAsync(Guid orderId)
        {
            _pendingOrders.TryGetValue(orderId, out var order);
            return Task.FromResult(order);
        }

        public async Task<bool> ApproveOrderAsync(Guid orderId)
        {
            if (!_pendingOrders.TryGetValue(orderId, out var order))
            {
                _logger.LogWarning("Onaylanacak sipariş {OrderId} havuzda bulunamadı.", orderId);
                return false;
            }

            // Siparişi gönderilmeye hazır olarak işaretle (OrderService'e mesaj gönderecek)
            var orderApprovedMessage = new OrderApprovedMessage
            {
                OrderId = order.Id,
                // Onaylanmış miktarı StockService'e gönder
                Items = order.Items.Select(i => new OrderApprovedItem { ProductId = i.ProductId, Quantity = i.Quantity }).ToList()
            };

            await _messagePublisher.PublishAsync("stock-exchange", "order.approved", orderApprovedMessage);
            _logger.LogInformation("Sipariş {OrderId} onaylandı. Stok kesinleştirme mesajı yayınlandı.", orderId);
            _pendingOrders.Remove(orderId);

            return true;
        }
    }
}