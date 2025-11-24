using Microsoft.AspNetCore.Mvc;
using VerificationService.DTOs;
using VerificationService.Services;

namespace VerificationService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VerificationController : ControllerBase
    {
        private readonly IVerificationService _verificationService;
        private readonly ILogger<VerificationController> _logger;

        public VerificationController(
            IVerificationService verificationService,
            ILogger<VerificationController> logger)
        {
            _verificationService = verificationService;
            _logger = logger;
        }

        [HttpPost("approve/{orderId}")]
        public async Task<IActionResult> ApproveOrder(Guid orderId, [FromBody] ApproveRequest? request)
        {
            try
            {
                // ApprovedBy bilgisini request'ten al veya varsayılan değer kullan
                var approvedBy = request?.ApprovedBy;

                var result = await _verificationService.ApproveOrderAsync(orderId, approvedBy);

                if (result == null)
                    return NotFound(new { message = $"Onay talebi bulunamadı veya durumu uygun değil: {orderId}" });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş onaylanırken hata: {OrderId}", orderId);
                return StatusCode(500, new { message = "Sipariş onaylanırken bir hata oluştu" });
            }
        }

        [HttpPost("reject/{orderId}")]
        public async Task<IActionResult> RejectOrder(Guid orderId, [FromBody] RejectRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Reason))
                    return BadRequest(new { message = "Ret sebebi belirtilmelidir" });

                var result = await _verificationService.RejectOrderAsync(orderId, request.Reason);

                if (result == null)
                    return NotFound(new { message = $"Onay talebi bulunamadı veya durumu uygun değil: {orderId}" });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş reddedilirken hata: {OrderId}", orderId);
                return StatusCode(500, new { message = "Sipariş reddedilirken bir hata oluştu" });
            }
        }

        [HttpGet("order/{orderId}")]
        public async Task<ActionResult<VerificationResponse>> GetVerificationByOrderId(Guid orderId)
        {
            try
            {
                var verification = await _verificationService.GetVerificationByOrderIdAsync(orderId);

                if (verification == null)
                    return NotFound(new { message = $"Onay talebi bulunamadı: {orderId}" });

                return Ok(verification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Onay talebi alınırken hata: {OrderId}", orderId);
                return StatusCode(500, new { message = "Onay talebi alınırken bir hata oluştu" });
            }
        }

        [HttpGet("pending")]
        public async Task<ActionResult<IEnumerable<VerificationResponse>>> GetPendingVerifications()
        {
            try
            {
                var verifications = await _verificationService.GetPendingVerificationsAsync();
                return Ok(verifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bekleyen onay talepleri alınırken hata");
                return StatusCode(500, new { message = "Bekleyen onay talepleri alınırken bir hata oluştu" });
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VerificationResponse>>> GetAllVerifications()
        {
            try
            {
                var verifications = await _verificationService.GetAllVerificationsAsync();
                return Ok(verifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tüm onay talepleri alınırken hata");
                return StatusCode(500, new { message = "Onay talepleri alınırken bir hata oluştu" });
            }
        }
    }
}