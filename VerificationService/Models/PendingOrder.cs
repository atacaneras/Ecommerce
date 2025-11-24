using OrderService.Models;

namespace VerificationService.Models
{
    public class PendingOrder
    {
        public Guid Id { get; set; } // Sipariş ID'si
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime ReservedAt { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.StockReserved;

        public string ItemsJson { get; set; } = string.Empty;

        public List<OrderItem> GetItems()
        {
            if (string.IsNullOrEmpty(ItemsJson))
                return new List<OrderItem>();

            return System.Text.Json.JsonSerializer.Deserialize<List<OrderItem>>(ItemsJson)
                   ?? new List<OrderItem>();
        }
    }
}