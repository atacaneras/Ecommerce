using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.DTOs;
using NotificationService.Services;

namespace NotificationService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(INotificationService notificationService, ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest request)
        {
            try
            {
                var result = await _notificationService.SendNotificationAsync(request);
                if (result)
                    return Ok(new { message = "Bildirim başarıyla gönderildi" });

                return BadRequest(new { message = "Bildirim gönderilemedi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bildirim gönderilirken hata oluştu");
                return StatusCode(500, new { message = "Bir hata oluştu" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<NotificationResponse>> GetNotification(int id)
        {
            try
            {
                var notification = await _notificationService.GetNotificationByIdAsync(id);
                if (notification == null)
                    return NotFound(new { message = $"Bildirim {id} bulunamadı" });

                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bildirim alınırken hata oluştu {Id}", id);
                return StatusCode(500, new { message = "Bir hata oluştu" });
            }
        }

        [HttpGet("order/{orderId}")]
        public async Task<ActionResult<IEnumerable<NotificationResponse>>> GetNotificationsByOrder(Guid orderId)
        {
            try
            {
                var notifications = await _notificationService.GetNotificationsByOrderIdAsync(orderId);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş için bildirimler alınırken hata oluştu {OrderId}", orderId);
                return StatusCode(500, new { message = "Bir hata oluştu" });
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<NotificationResponse>>> GetAllNotifications()
        {
            try
            {
                var notifications = await _notificationService.GetAllNotificationsAsync();
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tüm bildirimler alınırken hata oluştu");
                return StatusCode(500, new { message = "Bir hata oluştu" });
            }
        }

        [HttpPost("test-email")]
        public async Task<IActionResult> TestEmail([FromBody] TestEmailRequest request)
        {
            try
            {
                var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
                var result = await emailService.SendEmailAsync(
                    request.To ?? "atacaneras@gmail.com",
                    request.Subject ?? "Test Email",
                    request.Body ?? "This is a test email from Notification Service",
                    null,
                    null);
                
                if (result)
                    return Ok(new { message = "Test email başarıyla gönderildi" });
                
                return BadRequest(new { message = "Test email gönderilemedi. Lütfen logları kontrol edin." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test email gönderilirken hata oluştu");
                return StatusCode(500, new { message = $"Hata: {ex.Message}" });
            }
        }
    }

    public class TestEmailRequest
    {
        public string? To { get; set; }
        public string? Subject { get; set; }
        public string? Body { get; set; }
    }
}
