namespace VerificationService.Models
{
    public class VerificationRequestLog
    {
        public int Id { get; set; }
        public Guid OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string Status { get; set; } = "Requested"; // Requested, Approved, Rejected, Failed
        public string? ProcessorNote { get; set; }
    }
}