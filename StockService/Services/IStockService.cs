using StockService.DTOs;

namespace StockService.Services
{
    public interface IStockService
    {
        Task<ProductResponse> CreateProductAsync(CreateProductRequest request);
        Task<ProductResponse?> UpdateProductAsync(int productId, UpdateProductRequest request);
        Task<bool> DeleteProductAsync(int productId);

        Task<bool> ReserveStockAsync(UpdateStockRequest request); // İsim UpdateStockAsync'den ReserveStockAsync olarak değiştirildi
        Task<bool> FinalizeStockAsync(UpdateStockRequest request); // Yeni metod
        Task<bool> CheckStockAvailabilityAsync(int productId, int quantity);

        Task<ProductResponse?> GetProductByIdAsync(int productId);
        Task<IEnumerable<ProductResponse>> GetAllProductsAsync();
    }
}