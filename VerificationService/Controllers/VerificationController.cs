using Microsoft.AspNetCore.Mvc;
using VerificationService.DTOs;
using VerificationService.Services;

namespace VerificationService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VerificationController : ControllerBase
    {
        private readonly IVerificationService _service;
        private readonly ILogger<VerificationController> _logger;

        public VerificationController(
            IVerificationService service,
            ILogger<VerificationController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VerificationResponse>>> GetPendingVerifications()
        {
            var verifications = await _service.GetPendingVerificationsAsync();
            return Ok(verifications);
        }

        [HttpPost("approve/{orderId}")]
        public async Task<IActionResult> ApproveOrder(Guid orderId)
        {
            try
            {
                var success = await _service.ApproveVerificationAsync(orderId);

                if (!success)
                    return NotFound(new { message = "Doğrulama kaydı bulunamadı veya işlem başarısız." });

                return Ok(new { message = "Sipariş başarıyla onaylandı." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hata oluştu: {OrderId}", orderId);
                return StatusCode(500, new { message = "Hata oluştu." });
            }
        }
    }
}