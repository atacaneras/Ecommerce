using Microsoft.AspNetCore.Mvc;
using StockService.DTOs;
using StockService.Services;

namespace StockService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController : ControllerBase
    {
        private readonly IStockService _stockService;
        private readonly ILogger<StockController> _logger;

        public StockController(IStockService stockService, ILogger<StockController> logger)
        {
            _stockService = stockService;
            _logger = logger;
        }

        /// <summary>
        /// Yeni ürün oluşturur ve stokunu başlatır.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductResponse>> CreateProduct([FromBody] CreateProductRequest request)
        {
            try
            {
                var product = await _stockService.CreateProductAsync(request);
                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ürün oluşturulurken hata oluştu: {ProductName}", request.Name);
                return StatusCode(500, new { message = "Ürün oluşturulurken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Mevcut bir ürünü günceller.
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductResponse>> UpdateProduct(int id, [FromBody] UpdateProductRequest request)
        {
            try
            {
                var product = await _stockService.UpdateProductAsync(id, request);
                if (product == null)
                    return NotFound(new { message = $"Ürün {id} bulunamadı" });

                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ürün güncellenirken hata oluştu: {ProductId}", id);
                return StatusCode(500, new { message = "Ürün güncellenirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Belirtilen ID'ye sahip ürünü siler.
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            try
            {
                var result = await _stockService.DeleteProductAsync(id);
                if (!result)
                    return NotFound(new { message = $"Ürün {id} bulunamadı" });

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ürün silinirken hata oluştu: {ProductId}", id);
                return StatusCode(500, new { message = "Ürün silinirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Stok miktarını günceller (Manuel stok girişi gibi düşünülmüştür, rezervasyon değil).
        /// Not: Bu metot artık sipariş akışının parçası değildir ve devre dışı bırakılmıştır.
        /// </summary>
        [HttpPost("update")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> UpdateStock([FromBody] UpdateStockRequest request)
        {
            // CS1061 hatası çözüldü. Metot kaldırıldı ve yerine uyarı mesajı dönüldü.
            _logger.LogWarning("Stok Controller'daki UpdateStock metodu devre dışı bırakıldı. Bu endpoint, sipariş akışının parçası değildir.");
            return Task.FromResult<IActionResult>(StatusCode(403, new { message = "Bu endpoint sipariş akışı için kullanılmaz. Lütfen `/api/stock/{id}` PUT metodunu kullanın." }));
        }

        [HttpGet("products")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductResponse>>> GetAllProducts()
        {
            try
            {
                var products = await _stockService.GetAllProductsAsync();
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ürünler getirilirken hata oluştu");
                return StatusCode(500, new { message = "Ürünler getirilirken bir hata oluştu" });
            }
        }

        [HttpGet("products/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductResponse>> GetProduct(int id)
        {
            try
            {
                var product = await _stockService.GetProductByIdAsync(id);
                if (product == null)
                    return NotFound(new { message = $"Ürün {id} bulunamadı" });

                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ürün getirilirken hata oluştu: {ProductId}", id);
                return StatusCode(500, new { message = "Ürün getirilirken bir hata oluştu" });
            }
        }
    }
}