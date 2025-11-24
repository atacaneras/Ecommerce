using Microsoft.AspNetCore.Mvc;
using VerificationService.Services;

namespace VerificationService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VerificationController : ControllerBase
    {
        private readonly IVerificationService _verificationService;
        private readonly ILogger<VerificationController> _logger;

        public VerificationController(IVerificationService verificationService, ILogger<VerificationController> logger)
        {
            _verificationService = verificationService;
            _logger = logger;
        }

        [HttpPost("approve/{orderId}")]
        public async Task<IActionResult> ApproveOrder(Guid orderId)
        {
            try
            {
                var result = await _verificationService.ApproveOrderAsync(orderId);

                if (result)
                    return Ok(new { message = $"Sipariş {orderId} başarıyla onaylandı ve işleniyor" });

                return BadRequest(new { message = $"Sipariş {orderId} onaylanırken bir sorun oluştu veya onaylanmaya uygun değil" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş {OrderId} onaylanırken kritik hata oluştu", orderId);
                return StatusCode(500, new { message = "Bir hata oluştu" });
            }
        }
    }
}