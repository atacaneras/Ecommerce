using NotificationService.DTOs;
using NotificationService.Models;

namespace NotificationService.Services
{
    public interface INotificationService
    {
        Task<bool> SendNotificationAsync(SendNotificationRequest request);
        Task<bool> SendPendingNotificationsAsync(Guid orderId);
        Task<NotificationResponse?> GetNotificationByIdAsync(int id);
        Task<IEnumerable<NotificationResponse>> GetNotificationsByOrderIdAsync(Guid orderId);
        Task<IEnumerable<NotificationResponse>> GetAllNotificationsAsync();
    }
}