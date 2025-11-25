namespace NotificationService.DTOs
{
    public class SendNotificationRequest
    {
        public Guid OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "Both"; // Email, SMS, Both
        public bool ShouldSendImmediately { get; set; } = true; // If false, notification will be saved as Pending without sending
    }

    public class NotificationResponse
    {
        public int Id { get; set; }
        public Guid OrderId { get; set; }
        public string Recipient { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
    }
}