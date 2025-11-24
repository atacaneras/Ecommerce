namespace VerificationService.Models
{
    public class Verification
    {
        public int Id { get; set; }
        public Guid OrderId { get; set; }

        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }

        public VerificationStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? VerifiedAt { get; set; }

        public string? ApprovedBy { get; set; }
        public string? RejectReason { get; set; }
    }

    public enum VerificationStatus
    {
        Pending,
        Approved,
        Rejected
    }
}
