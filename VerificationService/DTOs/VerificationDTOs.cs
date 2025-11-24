namespace VerificationService.DTOs
{
    public class UpdateOrderStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    // YENİ: Approve için kullanılan Request
    public class ApproveRequest
    {
        public string ApprovedBy { get; set; } = "System/Manual";
    }

    // YENİ: Reject için kullanılan Request
    public class RejectRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    // YENİ: Frontend'e döndürülen Onay Log detayları
    public class VerificationResponse
    {
        public int Id { get; set; }
        public Guid OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty; // Requested, Approved, Rejected, Failed
        public DateTime RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? ProcessorNote { get; set; }
    }
}