using Microsoft.Extensions.Logging;
using OrderService.DTOs;
using OrderService.Models;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using Shared.Infrastructure.Repository;
using System.Linq;

namespace OrderService.Services
{
    public class OrderServiceImpl : IOrderService
    {
        private readonly IRepository<Order> _orderRepository;
        private readonly IMessagePublisher _messagePublisher;
        private readonly ILogger<OrderServiceImpl> _logger;

        public OrderServiceImpl(
            IRepository<Order> orderRepository,
            IMessagePublisher messagePublisher,
            ILogger<OrderServiceImpl> logger)
        {
            _orderRepository = orderRepository;
            _messagePublisher = messagePublisher;
            _logger = logger;
        }

        public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
        {
            try
            {
                _logger.LogInformation("Müşteri için yeni sipariş oluşturuluyor: {CustomerName}", request.CustomerName);

                // Sipariş entity'si oluştur
                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerName = request.CustomerName,
                    CustomerEmail = request.CustomerEmail,
                    CustomerPhone = request.CustomerPhone,
                    Status = OrderStatus.Pending, // Başlangıç durumu Pending
                    CreatedAt = DateTime.UtcNow,
                    Items = request.Items.Select(i => new OrderItem
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.Quantity * i.UnitPrice
                    }).ToList()
                };

                order.TotalAmount = order.Items.Sum(i => i.TotalPrice);

                // Veritabanına kaydet
                await _orderRepository.AddAsync(order);
                _logger.LogInformation("Sipariş {OrderId} veritabanına başarıyla kaydedildi", order.Id);

                // Verification Service'e mesaj gönder (Öncelikle stok rezerve edilecek)
                var verificationMessage = new VerificationMessage
                {
                    OrderId = order.Id,
                    CustomerName = order.CustomerName,
                    CustomerEmail = order.CustomerEmail,
                    Items = order.Items.Select(i => new VerificationItem
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity
                    }).ToList()
                };

                // Stok rezervasyonu için mesajı VerificationService'e gönder
                await _messagePublisher.PublishAsync("verification-exchange", "verification.reserve", verificationMessage);
                _logger.LogInformation("Sipariş {OrderId} için doğrulama (rezervasyon) mesajı yayınlandı", order.Id);

                // Bildirim mesajını yayınla (Sipariş alındı)
                var notificationMessage = new NotificationMessage
                {
                    OrderId = order.Id,
                    CustomerEmail = order.CustomerEmail,
                    CustomerPhone = order.CustomerPhone,
                    Message = $"Siparişiniz #{order.Id} alındı ve onayınızı bekliyor. Toplam: {order.TotalAmount:F2}₺",
                    Type = NotificationType.Both
                };

                await _messagePublisher.PublishAsync("notification-exchange", "notification.send", notificationMessage);
                _logger.LogInformation("Sipariş {OrderId} için bildirim mesajı yayınlandı", order.Id);

                return MapToResponse(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müşteri için sipariş oluşturulurken hata oluştu: {CustomerName}", request.CustomerName);
                throw;
            }
        }

        public async Task<OrderResponse?> GetOrderByIdAsync(Guid orderId)
        {
            try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                return order != null ? MapToResponse(order) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ID ile sipariş alınırken hata oluştu: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<IEnumerable<OrderResponse>> GetAllOrdersAsync()
        {
            try
            {
                var orders = await _orderRepository.GetAllAsync();
                return orders.Select(MapToResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tüm siparişler alınırken hata oluştu");
                throw;
            }
        }

        private OrderResponse MapToResponse(Order order)
        {
            return new OrderResponse
            {
                Id = order.Id,
                CustomerName = order.CustomerName,
                CustomerEmail = order.CustomerEmail,
                TotalAmount = order.TotalAmount,
                Status = order.Status.ToString(),
                CreatedAt = order.CreatedAt,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };
        }
    }
}