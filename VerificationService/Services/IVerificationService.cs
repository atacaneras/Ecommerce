{
    public interface IVerificationService
{
    Task<bool> ApproveOrderAsync(Guid orderId);
}
