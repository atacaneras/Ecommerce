using System.Text;
using System.Xml.Linq;

namespace NotificationService.Services
{
    public class SmsService : ISmsService
    {
        private readonly ILogger<SmsService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        // NetGSM API endpoint
        private const string API_URL = "https://api.netgsm.com.tr/sms/send/get";

        public SmsService(
            ILogger<SmsService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                var username = _configuration["Sms:NetGSM:Username"];
                var password = _configuration["Sms:NetGSM:Password"];
                var header = _configuration["Sms:NetGSM:Header"]; // Başlık (örn: "BEYMEN")

                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogInformation(
                        "Gönderilecek: {PhoneNumber}, Mesaj: {Message}",
                        phoneNumber, message);
                    await Task.Delay(100);
                    return true;
                }

                // Telefon numarasını temizle (sadece rakamlar)
                phoneNumber = new string(phoneNumber.Where(char.IsDigit).ToArray());

                // Türkiye formatına çevir (+90 olmadan, 10 haneli)
                if (phoneNumber.StartsWith("90") && phoneNumber.Length == 12)
                {
                    phoneNumber = phoneNumber.Substring(2);
                }

                // API parametreleri
                var parameters = new Dictionary<string, string>
                {
                    { "usercode", username },
                    { "password", password },
                    { "gsmno", phoneNumber },
                    { "message", message },
                    { "msgheader", header },
                    { "dil", "TR" } // Türkçe karakter desteği
                };

                // URL encode
                var content = new FormUrlEncodedContent(parameters);
                var requestUrl = $"{API_URL}?{await content.ReadAsStringAsync()}";

                _logger.LogInformation("{PhoneNumber} numarasına NetGSM üzerinden SMS gönderiliyor", phoneNumber);

                var response = await _httpClient.GetAsync(requestUrl);
                var result = await response.Content.ReadAsStringAsync();

                // NetGSM response kodları:
                // 00, 01, 02 = Başarılı
                // 20 = Mesaj çok uzun
                // 30 = Geçersiz kullanıcı adı/şifre
                // 40 = Mesaj başlığı tanımsız
                // 70 = Hatalı numara

                if (result.StartsWith("00") || result.StartsWith("01") || result.StartsWith("02"))
                {
                    _logger.LogInformation("SMS başarıyla gönderildi: {PhoneNumber}. Yanıt: {Response}",
                        phoneNumber, result);
                    return true;
                }
                else
                {
                    _logger.LogWarning("NetGSM üzerinden SMS gönderimi başarısız oldu. Yanıt: {Response}", result);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NetGSM üzerinden SMS gönderilemedi: {PhoneNumber}. Hata: {Message}",
                    phoneNumber, ex.Message);
                return false;
            }
        }
    }
}
