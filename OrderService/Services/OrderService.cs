using Microsoft.Extensions.Logging;
using OrderService.DTOs;
using OrderService.Models;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Messaging.Messages;
using Shared.Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;

namespace OrderService.Services
{
    public class OrderServiceImpl : IOrderService
    {
        private readonly IRepository<Order> _orderRepository;
        private readonly IMessagePublisher _messagePublisher;
        private readonly ILogger<OrderServiceImpl> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly OrderDbContext _context;

        public OrderServiceImpl(
            IRepository<Order> orderRepository,
            OrderDbContext context,
            IMessagePublisher messagePublisher,
            ILogger<OrderServiceImpl> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _orderRepository = orderRepository;
            _context = context;
            _messagePublisher = messagePublisher;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
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
                    Status = OrderStatus.Pending,
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

                // 5. Bildirim mesajını yayınla (Hemen gönder - sipariş alındı bildirimi)
                var notificationMessage = new NotificationMessage
                {
                    OrderId = order.Id,
                    CustomerName = order.CustomerName,
                    CustomerEmail = order.CustomerEmail,
                    CustomerPhone = order.CustomerPhone,
                    Message = $"Siparişiniz #{order.Id} alındı ve onay bekliyor. Toplam: {order.TotalAmount:F2}₺",
                    Type = NotificationType.Both,
                    ShouldSendImmediately = true // Sipariş alındı bildirimi hemen gönderilmeli
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
                var order = await _context.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

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
                var orders = await _context.Orders
                    .Include(o => o.Items)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

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
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null) return null;

            order.Status = newStatus;
            order.UpdatedAt = DateTime.UtcNow;
            await _orderRepository.UpdateAsync(order);

            // YENİ: İptal durumu için stok iptali ve bildirim gönder
            if (newStatus == OrderStatus.Cancelled)
            {
                // Stok rezervasyonunu iptal et
                var stockServiceUrl = _configuration["Services:StockService"] ?? "http://stock-service";
                var httpClient = _httpClientFactory.CreateClient();

                try
                {
                    var stockCancelResponse = await httpClient.PostAsync(
                        $"{stockServiceUrl}/api/stock/cancel/{order.Id}",
                        null);

                    if (stockCancelResponse.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Sipariş {OrderId} için stok rezervasyonu iptal edildi.", order.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Sipariş {OrderId} için stok iptali başarısız oldu: {Error}",
                            order.Id, await stockCancelResponse.Content.ReadAsStringAsync());
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Sipariş {OrderId} için stok iptali sırasında hata oluştu", order.Id);
                }

                // Verification durumunu güncelle (Rejected olarak işaretle) - ÖNCE BUNU YAP
                var verificationServiceUrl = _configuration["Services:VerificationService"] ?? "http://verification-service";
                try
                {
                    var verificationCancelResponse = await httpClient.PostAsync(
                        $"{verificationServiceUrl}/api/verification/cancel/{order.Id}",
                        null);

                    if (verificationCancelResponse.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Sipariş {OrderId} için doğrulama kaydı iptal edildi.", order.Id);
                    }
                    else
                    {
                        var errorContent = await verificationCancelResponse.Content.ReadAsStringAsync();
                        _logger.LogWarning("Sipariş {OrderId} için doğrulama iptali başarısız oldu: {Error}",
                            order.Id, errorContent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Sipariş {OrderId} için doğrulama iptali sırasında hata oluştu: {Message}",
                        order.Id, ex.Message);
                }

                // İptal bildirimi gönder
                var notificationMessage = new NotificationMessage
                {
                    OrderId = order.Id,
                    CustomerName = order.CustomerName,
                    CustomerEmail = order.CustomerEmail,
                    CustomerPhone = order.CustomerPhone,
                    // İstenen mesaj: "siparişiniz iptal edildi"
                    Message = "Siparişiniz iptal edildi.",
                    Type = NotificationType.Both,
                    ShouldSendImmediately = true // İptal bildirimi hemen gönderilmeli
                };

                await _messagePublisher.PublishAsync("notification-exchange", "notification.send", notificationMessage);
                _logger.LogInformation("Sipariş {OrderId} için iptal bildirimi yayınlandı.", order.Id);
            }

            return MapToResponse(order);
        }

private OrderResponse MapToResponse(Order order)
        {
            return new OrderResponse
                   {
                       Id = order.Id,
                       CustomerName = order.CustomerName,
                       CustomerEmail = order.CustomerEmail,

                       CustomerPhone = order.CustomerPhone,

                       TotalAmount = order.TotalAmount,
                       Status = order.Status.ToString(),
                       CreatedAt = order.CreatedAt,

                       Items = order.Items?.Select(i => new OrderItemDto
                       {
                           ProductId = i.ProductId,
                           ProductName = i.ProductName,
                           Quantity = i.Quantity,
                           UnitPrice = i.UnitPrice
                       }).ToList() ?? new List<OrderItemDto>()
                   };
        }
    }
}