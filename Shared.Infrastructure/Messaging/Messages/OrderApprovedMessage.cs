namespace Shared.Infrastructure.Messaging.Messages
{
    public class OrderApprovedMessage
    {
        public Guid OrderId { get; set; }
        public string? CustomerName { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
