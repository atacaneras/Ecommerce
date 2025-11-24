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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public StockServiceImpl(
           IRepository<Product> productRepository,
           StockDbContext context,
           ILogger<StockServiceImpl> logger,
           IHttpClientFactory httpClientFactory, 
           IConfiguration configuration)
        {
            _productRepository = productRepository;
            _context = context;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
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

        public async Task<bool> UpdateStockAsync(UpdateStockRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("{OrderId} siparişi için stok güncellemesi işleniyor", request.OrderId);

                foreach (var item in request.Items)
                {

                    var product = await _productRepository.GetByIdAsync(item.ProductId);

                    if (product == null)
                    {
                        _logger.LogWarning("Ürün {ProductId} bulunamadı", item.ProductId);
                        await transaction.RollbackAsync();
                        return false;
                    }

                    // Mevcut kullanılabilir stok: Toplam Stok - Rezerve Edilmiş Stok
                    var availableStock = product.StockQuantity - product.ReservedQuantity;

                    if (availableStock < item.Quantity)
                    {
                        _logger.LogWarning(
                            "{ProductId} ürünü için yetersiz stok. Mevcut: {Available}, İstenen: {Requested}",
                            item.ProductId, availableStock, item.Quantity);
                        await transaction.RollbackAsync();
                        return false;
                    }

                    // KRİTİK DEĞİŞİKLİK: StockQuantity'yi düşürmek yerine, ReservedQuantity'yi artırıyoruz.
                    // Böylece StockQuantity ilk oluşturulan değerinde kalır.
                    product.ReservedQuantity += item.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;

                    // İşlem kaydı oluştur
                    var stockTransaction = new StockTransaction
                    {
                        ProductId = product.Id,
                        OrderId = request.OrderId,
                        Quantity = item.Quantity,
                        Type = StockTransactionType.Sale,
                        CreatedAt = DateTime.UtcNow,
                        Notes = $"{request.OrderId} siparişi için stok rezerve edildi" // Not güncellendi
                    };

                    _context.StockTransactions.Add(stockTransaction);
                    await _productRepository.UpdateAsync(product);

                    _logger.LogInformation(
                        "Ürün {ProductId} için stok rezerve edildi. Rezerve edilen miktar: {ReservedQuantity}",
                        product.Id, product.ReservedQuantity);
                }


                var orderServiceUrl = _configuration["Services:OrderService"] ?? "http://order-service";
                var orderClient = _httpClientFactory.CreateClient();

                var updateStatusRequest = new
                {
                    Status = "StockReserved" // OrderService'teki enum ismine göre
                };

                var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(updateStatusRequest), System.Text.Encoding.UTF8, "application/json");

                var statusUpdateResponse = await orderClient.PutAsync($"{orderServiceUrl}/api/orders/status/{request.OrderId}", content);

                if (!statusUpdateResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Sipariş {OrderId} durumu OrderService'e bildirilemedi: StockReserved. Hata: {Error}", request.OrderId, await statusUpdateResponse.Content.ReadAsStringAsync());
                    // Stock transaction'ı geri al (Rollback)
                    await transaction.RollbackAsync();
                    return false;
                }

                await transaction.CommitAsync();
                _logger.LogInformation("{OrderId} siparişi için stok rezervasyonu başarıyla tamamlandı ve sipariş durumu güncellendi", request.OrderId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "{OrderId} siparişi için stok güncellenirken hata oluştu", request.OrderId);
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

        public async Task<bool> ConfirmStockAsync(Guid orderId, bool approved)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("{OrderId} siparişi için stok konfirmasyonu işleniyor. Onaylandı: {Approved}", orderId, approved);

                // İlgili stok işlemlerini bul
                var stockTransactions = await _context.StockTransactions
                    .Where(st => st.OrderId == orderId)
                    .Include(st => st.Product)
                    .ToListAsync();

                if (!stockTransactions.Any())
                {
                    _logger.LogWarning("Sipariş {OrderId} için stok işlemi bulunamadı", orderId);
                    await transaction.RollbackAsync();
                    return false;
                }

                foreach (var stockTransaction in stockTransactions)
                {
                    var product = stockTransaction.Product;

                    if (approved)
                    {
                        // Onaylandı: ReservedQuantity'den düş, StockQuantity'den de düş (kesinleştir)
                        if (product.ReservedQuantity >= stockTransaction.Quantity)
                        {
                            product.ReservedQuantity -= stockTransaction.Quantity;
                            product.StockQuantity -= stockTransaction.Quantity;
                            product.UpdatedAt = DateTime.UtcNow;

                            // İşlem notunu güncelle
                            stockTransaction.Notes = $"{orderId} siparişi onaylandı ve stok kesinleştirildi";
                            _context.StockTransactions.Update(stockTransaction);

                            await _productRepository.UpdateAsync(product);

                            _logger.LogInformation(
                                "Ürün {ProductId} için stok onaylandı. Rezerve: {Reserved}, Toplam: {Total}",
                                product.Id, product.ReservedQuantity, product.StockQuantity);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Ürün {ProductId} için rezerve stok yetersiz. İstenen: {Requested}, Mevcut Rezerve: {Reserved}",
                                product.Id, stockTransaction.Quantity, product.ReservedQuantity);
                            await transaction.RollbackAsync();
                            return false;
                        }
                    }
                    else
                    {
                        // Reddedildi: Sadece ReservedQuantity'den düş, StockQuantity'ye dokunma (iade et)
                        if (product.ReservedQuantity >= stockTransaction.Quantity)
                        {
                            product.ReservedQuantity -= stockTransaction.Quantity;
                            product.UpdatedAt = DateTime.UtcNow;

                            // İşlem notunu güncelle
                            stockTransaction.Notes = $"{orderId} siparişi reddedildi ve rezervasyon iade edildi";
                            _context.StockTransactions.Update(stockTransaction);

                            await _productRepository.UpdateAsync(product);

                            _logger.LogInformation(
                                "Ürün {ProductId} için rezervasyon iade edildi. Rezerve: {Reserved}, Toplam: {Total}",
                                product.Id, product.ReservedQuantity, product.StockQuantity);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Ürün {ProductId} için rezerve stok yetersiz. İstenen: {Requested}, Mevcut Rezerve: {Reserved}",
                                product.Id, stockTransaction.Quantity, product.ReservedQuantity);
                            await transaction.RollbackAsync();
                            return false;
                        }
                    }
                }

                await transaction.CommitAsync();
                _logger.LogInformation("{OrderId} siparişi için stok konfirmasyonu başarıyla tamamlandı", orderId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "{OrderId} siparişi için stok konfirmasyonu işlenirken hata", orderId);
                throw;
            }
        }

        public async Task<bool> FinalizeStockAsync(UpdateStockRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("{OrderId} siparişi için stok kesinleştirme (deduction) işleniyor", request.OrderId);

                foreach (var item in request.Items)
                {
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);

                    if (product == null || product.ReservedQuantity < item.Quantity)
                    {
                        _logger.LogError("Ürün {ProductId} bulunamadı veya rezerve edilen miktar yetersiz.", item.ProductId);
                        await transaction.RollbackAsync();
                        return false;
                    }

                    // Stok Kesinleştirme:
                    product.StockQuantity -= item.Quantity; // Toplam Stoktan düş
                    product.ReservedQuantity -= item.Quantity; // Rezerve Stoğu düş

                    product.UpdatedAt = DateTime.UtcNow;

                    var stockTransaction = new StockTransaction
                    {
                        ProductId = product.Id,
                        OrderId = request.OrderId,
                        Quantity = item.Quantity,
                        Type = StockTransactionType.Sale,
                        CreatedAt = DateTime.UtcNow,
                        Notes = $"{request.OrderId} siparişi için stok kesinleştirildi ve düşüldü"
                    };

                    _context.StockTransactions.Add(stockTransaction);
                    _context.Products.Update(product);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("{OrderId} siparişi için stok kesinleştirme başarıyla tamamlandı", request.OrderId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "{OrderId} siparişi için stok kesinleştirilirken hata oluştu", request.OrderId);
                throw;
            }
        }
    }
}