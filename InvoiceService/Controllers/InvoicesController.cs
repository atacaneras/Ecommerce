using Microsoft.AspNetCore.Mvc;
using InvoiceService.DTOs;
using InvoiceService.Services;

namespace InvoiceService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly ILogger<InvoicesController> _logger;

        public InvoicesController(IInvoiceService invoiceService, ILogger<InvoicesController> logger)
        {
            _invoiceService = invoiceService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<InvoiceResponse>> CreateInvoice([FromBody] CreateInvoiceRequest request)
        {
            try
            {
                var invoice = await _invoiceService.CreateInvoiceAsync(request);
                return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura oluşturulurken hata");
                return StatusCode(500, new { message = "Fatura oluşturulurken bir hata oluştu" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InvoiceResponse>> GetInvoice(int id)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
                if (invoice == null)
                    return NotFound(new { message = $"Fatura {id} bulunamadı" });

                return Ok(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura {Id} alınırken hata", id);
                return StatusCode(500, new { message = "Fatura alınırken bir hata oluştu" });
            }
        }

        [HttpGet("order/{orderId}")]
        public async Task<ActionResult<InvoiceResponse>> GetInvoiceByOrder(Guid orderId)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceByOrderIdAsync(orderId);
                if (invoice == null)
                    return NotFound(new { message = $"Sipariş {orderId} için fatura bulunamadı" });

                return Ok(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş {OrderId} için fatura alınırken hata", orderId);
                return StatusCode(500, new { message = "Fatura alınırken bir hata oluştu" });
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InvoiceListResponse>>> GetAllInvoices()
        {
            try
            {
                var invoices = await _invoiceService.GetAllInvoicesAsync();
                return Ok(invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tüm faturalar alınırken hata");
                return StatusCode(500, new { message = "Faturalar alınırken bir hata oluştu" });
            }
        }

        [HttpPost("{id}/pay")]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            try
            {
                var result = await _invoiceService.MarkAsPaidAsync(id);
                if (!result)
                    return NotFound(new { message = $"Fatura {id} bulunamadı" });

                return Ok(new { message = "Fatura ödendi olarak işaretlendi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura {Id} ödendi olarak işaretlenirken hata", id);
                return StatusCode(500, new { message = "Fatura güncellenirken bir hata oluştu" });
            }
        }

        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelInvoice(int id)
        {
            try
            {
                var result = await _invoiceService.CancelInvoiceAsync(id);
                if (!result)
                    return NotFound(new { message = $"Fatura {id} bulunamadı" });

                return Ok(new { message = "Fatura iptal edildi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura {Id} iptal edilirken hata", id);
                return StatusCode(500, new { message = "Fatura iptal edilirken bir hata oluştu" });
            }
        }
    }
}