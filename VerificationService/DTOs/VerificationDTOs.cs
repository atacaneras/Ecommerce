namespace VerificationService.DTOs
{
    public class VerificationResponse
    {
        public int Id { get; set; }
        public Guid OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}