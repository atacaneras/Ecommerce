namespace StockService.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public int ReservedQuantity { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<StockTransaction> Transactions { get; set; } = new();
    }

    public class StockTransaction
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Guid? OrderId { get; set; }
        public int Quantity { get; set; }
        public StockTransactionType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Notes { get; set; } = string.Empty;
        public Product Product { get; set; } = null!;
    }

    public enum StockTransactionType
    {
        Purchase,
        Sale,
        Return,
        Adjustment
    }
}