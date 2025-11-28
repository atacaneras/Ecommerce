using InvoiceService.Data;
using InvoiceService.DTOs;
using InvoiceService.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Repository;

namespace InvoiceService.Services
{


    public class InvoiceServiceImpl : IInvoiceService
    {
        private readonly IRepository<Invoice> _invoiceRepository;
        private readonly InvoiceDbContext _context;
        private readonly ILogger<InvoiceServiceImpl> _logger;

        public InvoiceServiceImpl(
            IRepository<Invoice> invoiceRepository,
            InvoiceDbContext context,
            ILogger<InvoiceServiceImpl> logger)
        {
            _invoiceRepository = invoiceRepository;
            _context = context;
            _logger = logger;
        }

        public async Task<InvoiceResponse> CreateInvoiceAsync(CreateInvoiceRequest request)
        {
            try
            {
                _logger.LogInformation("Sipariş {OrderId} için fatura oluşturuluyor", request.OrderId);

                var existingInvoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.OrderId == request.OrderId);

                if (existingInvoice != null)
                {
                    _logger.LogWarning("Sipariş {OrderId} için fatura zaten mevcut: {InvoiceNumber}",
                        request.OrderId, existingInvoice.InvoiceNumber);
                    return MapToResponse(existingInvoice);
                }

                // Total hesapla
                var subTotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);
                var taxRate = 20m; // KDV %20
                var taxAmount = subTotal * (taxRate / 100);
                var totalAmount = subTotal + taxAmount;

                // Fatura no yarat
                var invoiceNumber = GenerateInvoiceNumber();

                var invoice = new Invoice
                {
                    OrderId = request.OrderId,
                    InvoiceNumber = invoiceNumber,
                    CustomerName = request.CustomerName,
                    CustomerEmail = request.CustomerEmail,
                    CustomerPhone = request.CustomerPhone,
                    SubTotal = subTotal,
                    TaxRate = taxRate,
                    TaxAmount = taxAmount,
                    TotalAmount = totalAmount,
                    Status = InvoiceStatus.Issued,
                    CreatedAt = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(30),
                    Items = request.Items.Select(i => new InvoiceItem
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.Quantity * i.UnitPrice
                    }).ToList()
                };

                await _invoiceRepository.AddAsync(invoice);
                _logger.LogInformation("Fatura oluşturuldu: {InvoiceNumber} - Sipariş {OrderId}",
                    invoiceNumber, request.OrderId);

                return MapToResponse(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş {OrderId} için fatura oluşturulurken hata", request.OrderId);
                throw;
            }
        }

        public async Task<InvoiceResponse?> GetInvoiceByIdAsync(int id)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Items)
                    .FirstOrDefaultAsync(i => i.Id == id);

                return invoice != null ? MapToResponse(invoice) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura {Id} alınırken hata", id);
                throw;
            }
        }

        public async Task<InvoiceResponse?> GetInvoiceByOrderIdAsync(Guid orderId)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Items)
                    .FirstOrDefaultAsync(i => i.OrderId == orderId);

                return invoice != null ? MapToResponse(invoice) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş {OrderId} için fatura alınırken hata", orderId);
                throw;
            }
        }

        public async Task<IEnumerable<InvoiceResponse>> GetAllInvoicesAsync()
        {
            try
            {
                var invoices = await _context.Invoices
                    .Include(i => i.Items) // EKLENDİ: Ürün detaylarını çekmek için gerekli
                    .OrderByDescending(i => i.CreatedAt)
                    .ToListAsync();

                // DEĞİŞTİRİLDİ: MapToListResponse yerine MapToResponse kullanılıyor
                return invoices.Select(MapToResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tüm faturalar alınırken hata");
                throw;
            }
        }
        public async Task<bool> MarkAsPaidAsync(int invoiceId)
        {
            try
            {
                var invoice = await _invoiceRepository.GetByIdAsync(invoiceId);
                if (invoice == null) return false;

                invoice.Status = InvoiceStatus.Paid;
                invoice.PaidAt = DateTime.UtcNow;

                await _invoiceRepository.UpdateAsync(invoice);
                _logger.LogInformation("Fatura ödendi olarak işaretlendi: {InvoiceNumber}", invoice.InvoiceNumber);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura {Id} ödendi olarak işaretlenirken hata", invoiceId);
                return false;
            }
        }

        public async Task<bool> CancelInvoiceAsync(int invoiceId)
        {
            try
            {
                var invoice = await _invoiceRepository.GetByIdAsync(invoiceId);
                if (invoice == null) return false;

                invoice.Status = InvoiceStatus.Cancelled;

                await _invoiceRepository.UpdateAsync(invoice);
                _logger.LogInformation("Fatura iptal edildi: {InvoiceNumber}", invoice.InvoiceNumber);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura {Id} iptal edilirken hata", invoiceId);
                return false;
            }
        }

        private string GenerateInvoiceNumber()
        {
            var year = DateTime.UtcNow.Year;
            var month = DateTime.UtcNow.Month;
            var random = new Random().Next(1000, 9999);
            return $"INV-{year}{month:D2}-{random}";
        }

        private InvoiceResponse MapToResponse(Invoice invoice)
        {
            return new InvoiceResponse
            {
                Id = invoice.Id,
                OrderId = invoice.OrderId,
                InvoiceNumber = invoice.InvoiceNumber,
                CustomerName = invoice.CustomerName,
                CustomerEmail = invoice.CustomerEmail,
                CustomerPhone = invoice.CustomerPhone,
                Items = invoice.Items.Select(i => new InvoiceItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList(),
                SubTotal = invoice.SubTotal,
                TaxRate = invoice.TaxRate,
                TaxAmount = invoice.TaxAmount,
                TotalAmount = invoice.TotalAmount,
                Status = invoice.Status.ToString(),
                CreatedAt = invoice.CreatedAt,
                DueDate = invoice.DueDate,
                Notes = invoice.Notes
            };
        }

        private InvoiceListResponse MapToListResponse(Invoice invoice)
        {
            return new InvoiceListResponse
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                OrderId = invoice.OrderId,
                CustomerName = invoice.CustomerName,
                TotalAmount = invoice.TotalAmount,
                Status = invoice.Status.ToString(),
                CreatedAt = invoice.CreatedAt
            };
        }
    }
}