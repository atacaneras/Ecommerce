using StockService.DTOs;

namespace StockService.Services
{
    public interface IStockService
    {
        Task<ProductResponse> CreateProductAsync(CreateProductRequest request);
        Task<ProductResponse?> UpdateProductAsync(int productId, UpdateProductRequest request);
        Task<bool> DeleteProductAsync(int productId);

        Task<bool> UpdateStockAsync(UpdateStockRequest request);
        Task<bool> CheckStockAvailabilityAsync(int productId, int quantity);

        Task<ProductResponse?> GetProductByIdAsync(int productId);
        Task<IEnumerable<ProductResponse>> GetAllProductsAsync();
    }
}