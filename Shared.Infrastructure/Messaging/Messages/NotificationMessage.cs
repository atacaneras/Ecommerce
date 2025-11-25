namespace Shared.Infrastructure.Messaging.Messages
{
    public class NotificationMessage
    {
        public Guid OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public bool ShouldSendImmediately { get; set; } = true; // Default to true for backward compatibility
    }

    public enum NotificationType
    {
        Email,
        SMS,
        Both
    }
}