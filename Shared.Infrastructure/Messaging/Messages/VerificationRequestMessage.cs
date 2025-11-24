namespace Shared.Infrastructure.Messaging.Messages
{
    public class VerificationRequestMessage
    {
        public Guid OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}