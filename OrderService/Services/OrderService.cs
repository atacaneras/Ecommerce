using Microsoft.Extensions.Logging;
using OrderService.DTOs;
using OrderService.Models;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using Shared.Infrastructure.Repository;

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

                // 1. Sipariş entity'si oluştur (Status: Pending)
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

                // 2. Veritabanına kaydet
                await _orderRepository.AddAsync(order);
                _logger.LogInformation("Sipariş {OrderId} veritabanına başarıyla kaydedildi", order.Id);

                // 3. Stok güncelleme mesajını yayınla (Rezerve işlemi için)
                var stockMessage = new StockUpdateMessage
                {
                    OrderId = order.Id,
                    Items = order.Items.Select(i => new StockItem
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity
                    }).ToList()
                };

                await _messagePublisher.PublishAsync("stock-exchange", "stock.update", stockMessage);
                _logger.LogInformation("Sipariş {OrderId} için stok rezervasyon mesajı yayınlandı", order.Id);

                // 4. Onay servisi için DOĞRULAMA TALEBİ mesajı yayınla
                var verificationMessage = new VerificationRequestMessage
                {
                    OrderId = order.Id,
                    CustomerName = order.CustomerName,
                    CustomerEmail = order.CustomerEmail,
                    TotalAmount = order.TotalAmount,
                    CreatedAt = order.CreatedAt
                };

                await _messagePublisher.PublishAsync("verification-exchange", "verification.create", verificationMessage);
                _logger.LogInformation("Sipariş {OrderId} için onay talebi mesajı yayınlandı", order.Id);

                // 5. Bildirim mesajını yayınla
                var notificationMessage = new NotificationMessage
                {
                    OrderId = order.Id,
                    CustomerEmail = order.CustomerEmail,
                    CustomerPhone = order.CustomerPhone,
                    Message = $"Siparişiniz #{order.Id} alındı ve onay bekliyor. Toplam: {order.TotalAmount:F2}₺",
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

        public async Task<OrderResponse?> UpdateOrderStatusAsync(Guid orderId, OrderStatus newStatus)
        {
            try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                if (order == null)
                {
                    _logger.LogWarning("Durumu güncellenecek sipariş {OrderId} bulunamadı", orderId);
                    return null;
                }

                _logger.LogInformation("Sipariş {OrderId} durum değişikliği: {OldStatus} -> {NewStatus}",
                    orderId, order.Status, newStatus);

                order.Status = newStatus;
                order.UpdatedAt = DateTime.UtcNow;

                await _orderRepository.UpdateAsync(order);

                return MapToResponse(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş durumu güncellenirken hata oluştu: {OrderId}", orderId);
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