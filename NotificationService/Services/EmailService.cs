using System.Net;
using System.Net.Mail;

namespace NotificationService.Services
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;

        public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body, string? orderId = null, string? customerName = null)
        {
            try
            {
                // Input validation
                if (string.IsNullOrWhiteSpace(to))
                {
                    _logger.LogError("E-posta alıcı adresi boş olamaz");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(subject))
                {
                    _logger.LogWarning("E-posta konusu boş, varsayılan konu kullanılıyor");
                    subject = "Bildirim";
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    _logger.LogWarning("E-posta içeriği boş");
                    body = " ";
                }

                // Format body with HTML template
                body = EmailTemplateService.GetEmailTemplate(body, orderId ?? "", subject, customerName);

                var smtpHost = _configuration["Email:SmtpHost"];
                var smtpPortStr = _configuration["Email:SmtpPort"] ?? "587";

                if (!int.TryParse(smtpPortStr, out var smtpPort) || smtpPort <= 0 || smtpPort > 65535)
                {
                    _logger.LogError("Geçersiz SMTP port numarası: {Port}", smtpPortStr);
                    return false;
                }

                var smtpUsername = _configuration["Email:SmtpUsername"];
                var smtpPassword = _configuration["Email:SmtpPassword"]?.Replace(" ", "");
                var fromEmail = _configuration["Email:FromEmail"];
                var fromDisplayName = _configuration["Email:FromDisplayName"];

                if (string.IsNullOrEmpty(smtpHost))
                {
                    _logger.LogInformation(
                        "SMTP host yapılandırılmamış. Gönderilecek: {To}, Konu: {Subject}, İçerik: {Body}",
                        to, subject, body);
                    await Task.Delay(100);
                    return true;
                }

                if (string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
                {
                    _logger.LogError("SMTP kullanıcı adı veya şifre yapılandırılmamış");
                    return false;
                }

                // Gmail requires FromEmail to match SmtpUsername or use plus addressing
                // If FromEmail is different domain, use Gmail plus addressing or fallback to SmtpUsername
                if (string.IsNullOrEmpty(fromEmail))
                {
                    _logger.LogWarning("Gönderen e-posta adresi yapılandırılmamış, SMTP kullanıcı adı kullanılıyor");
                    fromEmail = smtpUsername;
                }
                else
                {
                    // If FromEmail is a different domain than Gmail, Gmail will reject it
                    // Use Gmail plus addressing: username+alias@gmail.com
                    // Or if it's a Gmail address, use it directly
                    if (!fromEmail.EndsWith("@gmail.com") && !fromEmail.EndsWith("@googlemail.com"))
                    {
                        // For non-Gmail addresses, we need to use Gmail plus addressing
                        // Extract the base Gmail username and add the alias
                        var baseUsername = smtpUsername.Split('@')[0];
                        var alias = fromEmail.Split('@')[0].Replace(".", "-");
                        fromEmail = $"{baseUsername}+{alias}@gmail.com";
                        _logger.LogInformation("Gmail plus addressing kullanılıyor: {FromEmail}", fromEmail);
                    }
                }

                // Display name yoksa e-posta adresini kullan
                if (string.IsNullOrWhiteSpace(fromDisplayName))
                {
                    fromDisplayName = "Trendyol E-Ticaret";
                }

                _logger.LogInformation("E-posta gönderiliyor: {To}, Konu: {Subject}, SMTP: {Host}:{Port}",
                    to, subject, smtpHost, smtpPort);

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Timeout = 30000 // 30 seconds timeout
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromDisplayName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true,
                    Priority = MailPriority.Normal
                };

                mailMessage.To.Add(new MailAddress(to));

                _logger.LogInformation("SMTP bağlantısı kuruluyor...");
                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("E-posta başarıyla gönderildi: {To}", to);
                return true;
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex,
                    "SMTP hatası: E-posta gönderilemedi: {To}. Hata: {Message}. StatusCode: {StatusCode}. InnerException: {InnerException}",
                    to, ex.Message, ex.StatusCode, ex.InnerException?.Message);
                _logger.LogError("SMTP Detayları - Host: {Host}, Port: {Port}, Username: {Username}",
                    _configuration["Email:SmtpHost"], _configuration["Email:SmtpPort"], _configuration["Email:SmtpUsername"]);
                return false;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Geçersiz parametre: E-posta gönderilemedi: {To}. Hata: {Message}. ParamName: {ParamName}",
                    to, ex.Message, ex.ParamName);
                return false;
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                _logger.LogError(ex, "Ağ bağlantı hatası: E-posta gönderilemedi: {To}. Hata: {Message}. SocketError: {SocketError}",
                    to, ex.Message, ex.SocketErrorCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "E-posta gönderilemedi: {To}. Hata: {Message}. Type: {ExceptionType}. StackTrace: {StackTrace}",
                    to, ex.Message, ex.GetType().Name, ex.StackTrace);
                return false;
            }
        }
    }
}
