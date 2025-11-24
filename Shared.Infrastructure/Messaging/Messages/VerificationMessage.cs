namespace Shared.Infrastructure.Messaging.Messages
{
    // OrderService'ten VerificationService'e gönderilecek ilk mesaj
    public class VerificationMessage
    {
        public Guid OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public List<VerificationItem> Items { get; set; } = new();
    }

    public class VerificationItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    // VerificationService'ten StockService'e gönderilecek final onay mesajı
    public class OrderApprovedMessage
    {
        public Guid OrderId { get; set; }
        public List<OrderApprovedItem> Items { get; set; } = new();
    }

    public class OrderApprovedItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}