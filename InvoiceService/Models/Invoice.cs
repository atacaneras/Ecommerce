namespace InvoiceService.Models
{
    public class Invoice
    {
        public int Id { get; set; }
        public Guid OrderId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;

        public List<InvoiceItem> Items { get; set; } = new();

        public decimal SubTotal { get; set; }
        public decimal TaxRate { get; set; } = 20; // KDV %20
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public InvoiceStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime DueDate { get; set; }

        public string? Notes { get; set; }
    }

    public class InvoiceItem
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public Invoice Invoice { get; set; } = null!;
    }

    public enum InvoiceStatus
    {
        Draft,
        Issued,
        Paid,
        Cancelled,
        Overdue
    }
}