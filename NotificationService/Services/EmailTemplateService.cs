using System.Text;

namespace NotificationService.Services
{
    public class EmailTemplateService
    {
        public static string GetEmailTemplate(string message, string orderId = "", string title = "Sipariş Bildirimi", string? customerName = null, InvoiceData? invoice = null)
        {
            var (primaryColor, icon, bgColor) = GetEmailTheme(message);
            var formattedMessage = message;
            if (!string.IsNullOrWhiteSpace(customerName))
            {
                formattedMessage = $"Sayın {customerName},<br><br>{message}";
            }
            var invoiceSection = invoice != null ? BuildInvoiceSection(invoice) : "";

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

                            {invoiceSection}
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
                                © {DateTime.Now.Year} Ata E-Ticaret. Tüm hakları saklıdır.
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

        private static string BuildInvoiceSection(InvoiceData invoice)
        {
            var itemsHtml = new StringBuilder();
            foreach (var item in invoice.Items)
            {
                itemsHtml.Append($@"
                    <tr>
                        <td style=""padding: 12px; border-bottom: 1px solid #e9ecef; color: #333;"">{item.ProductName}</td>
                        <td style=""padding: 12px; border-bottom: 1px solid #e9ecef; color: #666; font-size: 12px;"">
                            {item.Description}
                        </td>
                        <td style=""padding: 12px; border-bottom: 1px solid #e9ecef; color: #333; text-align: center;"">{item.Quantity}</td>
                        <td style=""padding: 12px; border-bottom: 1px solid #e9ecef; color: #333; text-align: right;"">{item.UnitPrice:F2} ₺</td>
                        <td style=""padding: 12px; border-bottom: 1px solid #e9ecef; color: #333; text-align: right; font-weight: 600;"">{item.TotalPrice:F2} ₺</td>
                    </tr>
                ");
            }

            return $@"
                <div style=""margin-top: 30px; background-color: #fff; border: 1px solid #e9ecef; border-radius: 8px; overflow: hidden;"">
                    <div style=""background-color: #28a745; color: white; padding: 15px 20px; font-weight: 600; font-size: 16px;"">
                        📄 Fatura Bilgileri
                    </div>
                    <div style=""padding: 20px;"">
                        <div style=""margin-bottom: 15px;"">
                            <p style=""margin: 5px 0; color: #666; font-size: 14px;"">
                                <strong>Fatura No:</strong> {invoice.InvoiceNumber}
                            </p>
                            <p style=""margin: 5px 0; color: #666; font-size: 14px;"">
                                <strong>Fatura Tarihi:</strong> {invoice.CreatedAt:dd.MM.yyyy HH:mm}
                            </p>
                            <p style=""margin: 5px 0; color: #666; font-size: 14px;"">
                                <strong>Vade Tarihi:</strong> {invoice.DueDate:dd.MM.yyyy}
                            </p>
                        </div>
                        
                        <table style=""width: 100%; border-collapse: collapse; margin-top: 20px;"">
                            <thead>
                                <tr style=""background-color: #f8f9fa;"">
                                    <th style=""padding: 12px; border-bottom: 2px solid #dee2e6; color: #495057; text-align: left; font-weight: 600;"">Ürün</th>
                                    <th style=""padding: 12px; border-bottom: 2px solid #dee2e6; color: #495057; text-align: left; font-weight: 600;"">Açıklama</th>
                                    <th style=""padding: 12px; border-bottom: 2px solid #dee2e6; color: #495057; text-align: center; font-weight: 600;"">Adet</th>
                                    <th style=""padding: 12px; border-bottom: 2px solid #dee2e6; color: #495057; text-align: right; font-weight: 600;"">Birim Fiyat</th>
                                    <th style=""padding: 12px; border-bottom: 2px solid #dee2e6; color: #495057; text-align: right; font-weight: 600;"">Toplam</th>
                                </tr>
                            </thead>
                            <tbody>
                                {itemsHtml}
                            </tbody>
                        </table>
                        
                        <div style=""margin-top: 20px; padding-top: 15px; border-top: 2px solid #dee2e6;"">
                            <div style=""display: flex; justify-content: space-between; margin-bottom: 8px;"">
                                <table style=""width: 100%; border-collapse: collapse;"">
                                    <tr>
                                        <td style=""padding: 5px 0; color: #666; text-align: right;"">Ara Toplam:</td>
                                        <td style=""padding: 5px 0 5px 20px; color: #333; font-weight: 600; text-align: right; width: 120px;"">{invoice.SubTotal:F2} ₺</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 5px 0; color: #666; text-align: right;"">KDV (%{invoice.TaxRate:F0}):</td>
                                        <td style=""padding: 5px 0 5px 20px; color: #333; font-weight: 600; text-align: right;"">{invoice.TaxAmount:F2} ₺</td>
                                    </tr>
                                    <tr style=""border-top: 2px solid #28a745;"">
                                        <td style=""padding: 10px 0 5px 0; color: #333; font-size: 18px; font-weight: 700; text-align: right;"">TOPLAM:</td>
                                        <td style=""padding: 10px 0 5px 20px; color: #28a745; font-size: 20px; font-weight: 700; text-align: right;"">{invoice.TotalAmount:F2} ₺</td>
                                    </tr>
                                </table>
                            </div>
                        </div>
                    </div>
                </div>
            ";
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

    public class InvoiceData
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public List<InvoiceItemData> Items { get; set; } = new();
        public decimal SubTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class InvoiceItemData
    {
        public string ProductName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty; 
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}