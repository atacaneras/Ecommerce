namespace Shared.Infrastructure.Messaging.Messages
{
    public class VerificationMessage
    {
        public Guid OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
    }

    public class StockConfirmMessage
    {
        public Guid OrderId { get; set; }
        public bool Approved { get; set; }
    }
}