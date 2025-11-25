using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.DTOs;
using NotificationService.Models;
using Polly;
using Shared.Infrastructure.Repository;

namespace NotificationService.Services
{
    public class NotificationServiceImpl : INotificationService
    {
        private readonly IRepository<Notification> _notificationRepository;
        private readonly NotificationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private readonly ILogger<NotificationServiceImpl> _logger;

        public NotificationServiceImpl(
            IRepository<Notification> notificationRepository,
            NotificationDbContext context,
            IEmailService emailService,
            ISmsService smsService,
            ILogger<NotificationServiceImpl> logger)
        {
            _notificationRepository = notificationRepository;
            _context = context;
            _emailService = emailService;
            _smsService = smsService;
            _logger = logger;
        }

        public async Task<bool> SendNotificationAsync(SendNotificationRequest request)
        {
            try
            {
                _logger.LogInformation("Sipariş {OrderId} için bildirim işleniyor", request.OrderId);

                var notificationsToSend = new List<Notification>();

                // Email bildirimi
                if (request.Type == "Email" || request.Type == "Both")
                {
                    var emailNotification = new Notification
                    {
                        OrderId = request.OrderId,
                        Recipient = request.Email,
                        Message = request.Message,
                        Type = NotificationType.Email,
                        Status = NotificationStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                        RetryCount = 0
                    };
                    notificationsToSend.Add(emailNotification);
                }

                // SMS bildirimi
                if (request.Type == "SMS" || request.Type == "Both")
                {
                    var smsNotification = new Notification
                    {
                        OrderId = request.OrderId,
                        Recipient = request.Phone,
                        Message = request.Message,
                        Type = NotificationType.SMS,
                        Status = NotificationStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                        RetryCount = 0
                    };
                    notificationsToSend.Add(smsNotification);
                }

                // Bildirimleri kaydet ve gönder (sadece ShouldSendImmediately true ise)
                foreach (var notification in notificationsToSend)
                {
                    await _notificationRepository.AddAsync(notification);
                    
                    if (request.ShouldSendImmediately)
                    {
                        await SendNotificationWithRetryAsync(notification, request.CustomerName);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Bildirim {NotificationId} Pending olarak kaydedildi, gönderilmedi. Sipariş #{OrderId}",
                            notification.Id, notification.OrderId);
                    }
                }

                _logger.LogInformation("Sipariş {OrderId} için bildirimler başarıyla işlendi", request.OrderId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş {OrderId} için bildirim gönderilirken hata oluştu", request.OrderId);
                return false;
            }
        }

        public async Task<bool> SendPendingNotificationsAsync(Guid orderId)
        {
            try
            {
                var pendingNotifications = await _context.Notifications
                    .Where(n => n.OrderId == orderId && n.Status == NotificationStatus.Pending)
                    .ToListAsync();

                if (pendingNotifications.Any())
                {
                    _logger.LogInformation("Sipariş {OrderId} için {Count} adet pending bildirim bulundu, gönderiliyor...", 
                        orderId, pendingNotifications.Count);

                    foreach (var notification in pendingNotifications)
                    {
                        // For pending notifications, we don't have customer name, so pass null
                        // The email will still work without the name
                        await SendNotificationWithRetryAsync(notification, null);
                    }

                    _logger.LogInformation("Sipariş {OrderId} için tüm pending bildirimler gönderildi", orderId);
                    return true;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş {OrderId} için pending bildirimler gönderilirken hata oluştu", orderId);
                return false;
            }
        }

        private async Task SendNotificationWithRetryAsync(Notification notification, string? customerName = null)
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    async (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception,
                            "{RetryCount}/3 denemesi başarısız oldu: {Type} bildirimi {Recipient} alıcısına gönderilemedi. {TimeSpan} sonra tekrar deneniyor.",
                            retryCount, notification.Type, notification.Recipient, timeSpan);

                        notification.RetryCount = retryCount;
                        await _notificationRepository.UpdateAsync(notification);
                    });

            try
            {
                await retryPolicy.ExecuteAsync(async () =>
                {
                    bool success = false;

                    // Gerçek email/sms gönderimi
                    if (notification.Type == NotificationType.Email)
                    {
                        // Determine subject based on message content
                        var emailSubject = GetEmailSubject(notification.Message);
                        success = await _emailService.SendEmailAsync(
                            notification.Recipient,
                            emailSubject,
                            notification.Message,
                            notification.OrderId.ToString(),
                            customerName);
                    }
                    else if (notification.Type == NotificationType.SMS)
                    {
                        success = await _smsService.SendSmsAsync(
                            notification.Recipient,
                            notification.Message);
                    }

                    if (!success)
                    {
                        throw new Exception($"{notification.Type} gönderimi başarısız");
                    }

                    notification.Status = NotificationStatus.Sent;
                    notification.SentAt = DateTime.UtcNow;
                    await _notificationRepository.UpdateAsync(notification);

                    _logger.LogInformation(
                        "{Type} bildirimi başarıyla gönderildi: {Recipient}, Sipariş #{OrderId}",
                        notification.Type, notification.Recipient, notification.OrderId);
                });
            }
            catch (Exception ex)
            {
                notification.Status = NotificationStatus.Failed;
                notification.ErrorMessage = ex.Message;
                await _notificationRepository.UpdateAsync(notification);

                _logger.LogError(ex,
                    "{RetryCount} denemeden sonra {Type} bildirimi {Recipient} alıcısına gönderilemedi",
                    notification.RetryCount, notification.Type, notification.Recipient);
            }
        }

        public async Task<NotificationResponse?> GetNotificationByIdAsync(int id)
        {
            try
            {
                var notification = await _notificationRepository.GetByIdAsync(id);
                return notification != null ? MapToResponse(notification) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Id} bildirimini alırken hata oluştu", id);
                throw;
            }
        }

        public async Task<IEnumerable<NotificationResponse>> GetNotificationsByOrderIdAsync(Guid orderId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.OrderId == orderId)
                    .ToListAsync();

                return notifications.Select(MapToResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş {OrderId} için bildirimleri alırken hata oluştu", orderId);
                throw;
            }
        }

        public async Task<IEnumerable<NotificationResponse>> GetAllNotificationsAsync()
        {
            try
            {
                var notifications = await _notificationRepository.GetAllAsync();
                return notifications.Select(MapToResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tüm bildirimleri alırken hata oluştu");
                throw;
            }
        }

        private string GetEmailSubject(string message)
        {
            var lowerMessage = message.ToLower();
            if (lowerMessage.Contains("iptal") || lowerMessage.Contains("iptal edildi"))
            {
                return "Siparişiniz İptal Edildi";
            }
            else if (lowerMessage.Contains("onaylandı") || lowerMessage.Contains("onaylandi"))
            {
                return "Siparişiniz Onaylandı";
            }
            else if (lowerMessage.Contains("alındı") || lowerMessage.Contains("alindi") || lowerMessage.Contains("bekliyor"))
            {
                return "Siparişiniz Alındı";
            }
            else
            {
                return "Sipariş Bildirimi";
            }
        }

        private NotificationResponse MapToResponse(Notification notification)
        {
            return new NotificationResponse
            {
                Id = notification.Id,
                OrderId = notification.OrderId,
                Recipient = notification.Recipient,
                Message = notification.Message,
                Type = notification.Type.ToString(),
                Status = notification.Status.ToString(),
                CreatedAt = notification.CreatedAt,
                SentAt = notification.SentAt
            };
        }
    }
}
