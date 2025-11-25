namespace NotificationService.Services
{
    public class EmailTemplateService
    {
        public static string GetEmailTemplate(string message, string orderId = "", string title = "Sipariş Bildirimi", string? customerName = null)
        {
            // Determine email type and color scheme
            var (primaryColor, icon, bgColor) = GetEmailTheme(message);
            
            // Format message with customer name
            var formattedMessage = message;
            if (!string.IsNullOrWhiteSpace(customerName))
            {
                formattedMessage = $"Sayın {customerName},<br><br>{message}";
            }

            return $@"
<!DOCTYPE html>
<html lang=""tr"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{title}</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse; background-color: #f4f4f4; padding: 20px;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" style=""max-width: 600px; width: 100%; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.1);"">
                    <!-- Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, {primaryColor} 0%, {GetDarkerColor(primaryColor)} 100%); padding: 30px 20px; text-align: center;"">
                            <h1 style=""margin: 0; color: #ffffff; font-size: 24px; font-weight: 600;"">
                                {icon} {title}
                            </h1>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style=""padding: 40px 30px;"">
                            <div style=""background-color: {bgColor}; border-left: 4px solid {primaryColor}; padding: 20px; border-radius: 4px; margin-bottom: 20px;"">
                                <p style=""margin: 0; color: #333333; font-size: 16px; line-height: 1.6;"">
                                    {formattedMessage}
                                </p>
                            </div>
                            
                            {(!string.IsNullOrEmpty(orderId) ? $@"
                            <div style=""background-color: #f8f9fa; padding: 15px; border-radius: 4px; margin-top: 20px;"">
                                <p style=""margin: 0; color: #666666; font-size: 14px;"">
                                    <strong>Sipariş ID:</strong> <span style=""color: #333333; font-family: monospace;"">{orderId}</span>
                                </p>
                            </div>
                            " : "")}
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: #f8f9fa; padding: 20px 30px; text-align: center; border-top: 1px solid #e9ecef;"">
                            <p style=""margin: 0; color: #6c757d; font-size: 12px; line-height: 1.5;"">
                                Bu e-posta otomatik olarak gönderilmiştir. Lütfen bu e-postaya yanıt vermeyin.<br>
                                Sorularınız için lütfen müşteri hizmetlerimizle iletişime geçin.
                            </p>
                            <p style=""margin: 15px 0 0 0; color: #6c757d; font-size: 11px;"">
                                © {DateTime.Now.Year} Trendyol E-Ticaret. Tüm hakları saklıdır.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private static (string primaryColor, string icon, string bgColor) GetEmailTheme(string message)
        {
            var lowerMessage = message.ToLower();
            
            if (lowerMessage.Contains("iptal") || lowerMessage.Contains("iptal edildi"))
            {
                return ("#dc3545", "❌", "#fee");
            }
            else if (lowerMessage.Contains("onaylandı") || lowerMessage.Contains("onaylandi"))
            {
                return ("#28a745", "✅", "#efe");
            }
            else if (lowerMessage.Contains("alındı") || lowerMessage.Contains("alindi") || lowerMessage.Contains("bekliyor"))
            {
                return ("#007bff", "📦", "#e7f3ff");
            }
            else
            {
                return ("#6c757d", "📧", "#f8f9fa");
            }
        }

        private static string GetDarkerColor(string hexColor)
        {
            // Simple darkening - convert hex to RGB, darken, convert back
            hexColor = hexColor.TrimStart('#');
            var r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
            var g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
            var b = Convert.ToInt32(hexColor.Substring(4, 2), 16);
            
            r = Math.Max(0, (int)(r * 0.8));
            g = Math.Max(0, (int)(g * 0.8));
            b = Math.Max(0, (int)(b * 0.8));
            
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}

