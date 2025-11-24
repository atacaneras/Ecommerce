using Microsoft.AspNetCore.Mvc;
using OrderService.DTOs;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<OrderResponse>> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                var order = await _orderService.CreateOrderAsync(request);
                return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş oluşturulurken hata oluştu");
                return StatusCode(500, new { message = "Sipariş oluşturulurken bir hata oluştu" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrderResponse>> GetOrder(Guid id)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(id);
                if (order == null)
                    return NotFound(new { message = $"Sipariş {id} bulunamadı" });

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş {OrderId} alınırken hata oluştu", id);
                return StatusCode(500, new { message = "Sipariş alınırken bir hata oluştu" });
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrderResponse>>> GetAllOrders()
        {
            try
            {
                var orders = await _orderService.GetAllOrdersAsync();
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tüm siparişler alınırken hata oluştu");
                return StatusCode(500, new { message = "Siparişler alınırken bir hata oluştu" });
            }
        }

        [HttpPut("status/{id}")]
        public async Task<ActionResult<OrderResponse>> UpdateOrderStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
        {
            try
            {
                if (!Enum.TryParse(request.Status, true, out OrderStatus newStatus))
                {
                    return BadRequest(new { message = $"Geçersiz sipariş durumu: {request.Status}" });
                }

                var order = await _orderService.UpdateOrderStatusAsync(id, newStatus);

                if (order == null)
                    return NotFound(new { message = $"Sipariş {id} bulunamadı" });

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş {OrderId} durumu güncellenirken hata", id);
                return StatusCode(500, new { message = "Sipariş durumu güncellenirken bir hata oluştu" });
            }
        }
    }
}