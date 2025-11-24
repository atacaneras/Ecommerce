using Microsoft.EntityFrameworkCore;
using StockService.Data;
using StockService.DTOs;
using StockService.Models;
using Shared.Infrastructure.Repository;

namespace StockService.Services
{
    public class StockServiceImpl : IStockService
    {
        private readonly IRepository<Product> _productRepository;
        private readonly StockDbContext _context;
        private readonly ILogger<StockServiceImpl> _logger;

        public StockServiceImpl(
            IRepository<Product> productRepository,
            StockDbContext context,
            ILogger<StockServiceImpl> logger)
        {
            _productRepository = productRepository;
            _context = context;
            _logger = logger;
        }

        public async Task<ProductResponse> CreateProductAsync(CreateProductRequest request)
        {
            var product = new Product
            {
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                StockQuantity = request.StockQuantity,
                ReservedQuantity = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _productRepository.AddAsync(product);
            _logger.LogInformation("Ürün oluşturuldu: {ProductName}", product.Name);
            return MapToResponse(product);
        }

        public async Task<ProductResponse?> UpdateProductAsync(int productId, UpdateProductRequest request)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null) return null;

            // Güncellemeleri uygula
            if (request.Name != null) product.Name = request.Name;
            if (request.Description != null) product.Description = request.Description;
            if (request.Price.HasValue) product.Price = request.Price.Value;
            if (request.StockQuantity.HasValue)
            {
                product.StockQuantity = request.StockQuantity.Value;
            }

            product.UpdatedAt = DateTime.UtcNow;
            await _productRepository.UpdateAsync(product);
            _logger.LogInformation("Ürün güncellendi: {ProductId}", productId);
            return MapToResponse(product);
        }

        public async Task<bool> DeleteProductAsync(int productId)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null) return false;

            await _productRepository.DeleteAsync(product);
            _logger.LogInformation("Ürün silindi: {ProductId}", productId);
            return true;
        }

        public async Task<bool> ReserveStockAsync(UpdateStockRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("{OrderId} siparişi için stok rezervasyonu işleniyor", request.OrderId);

                foreach (var item in request.Items)
                {
                    var product = await _productRepository.GetByIdAsync(item.ProductId);

                    if (product == null)
                    {
                        _logger.LogWarning("Ürün {ProductId} bulunamadı", item.ProductId);
                        await transaction.RollbackAsync();
                        return false;
                    }

                    var availableStock = product.StockQuantity - product.ReservedQuantity;

                    if (availableStock < item.Quantity)
                    {
                        _logger.LogWarning(
                            "{ProductId} ürünü için yetersiz stok. Mevcut: {Available}, İstenen: {Requested}",
                            item.ProductId, availableStock, item.Quantity);

                        await transaction.RollbackAsync();
                        return false;
                    }

                    product.ReservedQuantity += item.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;

                    var stockTransaction = new StockTransaction
                    {
                        ProductId = product.Id,
                        OrderId = request.OrderId,
                        Quantity = item.Quantity,
                        Type = StockTransactionType.Reserved,
                        CreatedAt = DateTime.UtcNow,
                        Notes = $"{request.OrderId} siparişi için stok rezerve edildi"
                    };

                    _context.StockTransactions.Add(stockTransaction);
                    await _productRepository.UpdateAsync(product);

                    _logger.LogInformation(
                        "Ürün {ProductId} için stok rezerve edildi. Rezerve edilen miktar: {ReservedQuantity}",
                        product.Id, product.ReservedQuantity);
                }

                await transaction.CommitAsync();
                _logger.LogInformation("{OrderId} siparişi için stok rezervasyonu tamamlandı", request.OrderId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "{OrderId} siparişi için stok rezervasyonu yapılırken hata oluştu", request.OrderId);
                throw;
            }
        }

        public async Task<bool> FinalizeStockAsync(UpdateStockRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("{OrderId} siparişi için stok finalizasyonu işleniyor", request.OrderId);

                foreach (var item in request.Items)
                {
                    var product = await _productRepository.GetByIdAsync(item.ProductId);

                    if (product == null)
                    {
                        _logger.LogWarning("Ürün {ProductId} bulunamadı", item.ProductId);
                        await transaction.RollbackAsync();
                        return false;
                    }

                    if (product.ReservedQuantity < item.Quantity)
                    {
                        _logger.LogWarning(
                            "{ProductId} ürünü için yeterli rezervasyon yok. Rezerve: {Reserved}, Gerekli: {Requested}",
                            item.ProductId, product.ReservedQuantity, item.Quantity);

                        await transaction.RollbackAsync();
                        return false;
                    }

                    // Rezervasyonu azalt
                    product.ReservedQuantity -= item.Quantity;

                    // Asıl stoktan düş
                    product.StockQuantity -= item.Quantity;

                    product.UpdatedAt = DateTime.UtcNow;

                    var stockTransaction = new StockTransaction
                    {
                        ProductId = product.Id,
                        OrderId = request.OrderId,
                        Quantity = item.Quantity,
                        Type = StockTransactionType.Sale,
                        CreatedAt = DateTime.UtcNow,
                        Notes = $"{request.OrderId} siparişi tamamlandı. Stok düşüldü."
                    };

                    _context.StockTransactions.Add(stockTransaction);
                    await _productRepository.UpdateAsync(product);

                    _logger.LogInformation(
                        "Ürün {ProductId} için final stok düşümü yapıldı. Kalan stok: {Stock}",
                        product.Id, product.StockQuantity);
                }

                await transaction.CommitAsync();
                _logger.LogInformation("{OrderId} siparişi için stok finalizasyonu tamamlandı", request.OrderId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "{OrderId} siparişi için stok finalizasyonu yapılırken hata oluştu", request.OrderId);
                throw;
            }
        }


        public async Task<ProductResponse?> GetProductByIdAsync(int productId)
        {
            try
            {
                var product = await _productRepository.GetByIdAsync(productId);
                return product != null ? MapToResponse(product) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ürün {ProductId} alınırken hata oluştu", productId);
                throw;
            }
        }

        public async Task<IEnumerable<ProductResponse>> GetAllProductsAsync()
        {
            try
            {
                var products = await _productRepository.GetAllAsync();
                return products.Select(MapToResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tüm ürünler alınırken hata oluştu");
                throw;
            }
        }

        public async Task<bool> CheckStockAvailabilityAsync(int productId, int quantity)
        {
            try
            {
                var product = await _productRepository.GetByIdAsync(productId);
                if (product == null) return false;

                // Mevcut kullanılabilir stoğu hesapla
                var availableStock = product.StockQuantity - product.ReservedQuantity;
                return availableStock >= quantity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ürün {ProductId} için stok müsaitliği kontrol edilirken hata oluştu", productId);
                throw;
            }
        }

        private ProductResponse MapToResponse(Product product)
        {
            return new ProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                ReservedQuantity = product.ReservedQuantity,
                // Kullanılabilir Miktar = Toplam Stok - Rezerve Miktar
                AvailableQuantity = product.StockQuantity - product.ReservedQuantity
            };
        }
    }
}