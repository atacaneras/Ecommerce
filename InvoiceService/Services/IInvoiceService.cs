using InvoiceService.DTOs;

namespace InvoiceService.Services
{
    public interface IInvoiceService
    {
        Task<InvoiceResponse> CreateInvoiceAsync(CreateInvoiceRequest request);
        Task<InvoiceResponse?> GetInvoiceByIdAsync(int id);
        Task<InvoiceResponse?> GetInvoiceByOrderIdAsync(Guid orderId);
        Task<IEnumerable<InvoiceListResponse>> GetAllInvoicesAsync();
        Task<bool> MarkAsPaidAsync(int invoiceId);
        Task<bool> CancelInvoiceAsync(int invoiceId);
    }
}
