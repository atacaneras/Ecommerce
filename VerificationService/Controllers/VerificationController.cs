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

        //Frontend'den gelen onaylama isteği
        [HttpPost("approve/{orderId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ApproveOrder(Guid orderId)
        {
            try
            {
                var result = await _verificationService.ApproveOrderAsync(orderId);

                if (result)
                {
                    return Ok(new { message = $"Sipariş {orderId} onaylandı ve stok kesinleştirme kuyruğa eklendi." });
                }

                return NotFound(new { message = $"Onaylanacak bekleyen sipariş {orderId} bulunamadı." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş onaylanırken hata oluştu: {OrderId}", orderId);
                return StatusCode(500, new { message = "Sipariş onaylanırken bir hata oluştu." });
            }
        }
    }
}