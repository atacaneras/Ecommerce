namespace VerificationService.DTOs
{
    public class VerificationResponse
    {
        public Guid OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}