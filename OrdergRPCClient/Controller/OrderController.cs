using Microsoft.AspNetCore.Mvc;

namespace OrdergRPCClient.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly OrderServiceGrpcClient _orderService;

        public OrderController(OrderServiceGrpcClient orderService)
        {
            _orderService = orderService;
        }

        [HttpGet]
        [Route("GetProductDetails")]
        public async Task<IActionResult> GetProductDetails(string productId)
        {
            try
            {
                var product = await _orderService.GetProductDetailsAsync(productId);
                return Ok(product);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPost]
        [Route("UpdateStock")]
        public async Task<IActionResult> UpdateStock([FromBody] UpdateStockRequest request)
        {
            try
            {
                var success = await _orderService.UpdateStockAsync(request.productId, request.Quantity, request.TransactionId);
                return Ok(new { Success = success });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }

    // Temporerly have taken this class once working fine then move it
    public class UpdateStockRequest
    {
        public string productId { get; set; }
        public int Quantity { get; set; }
        public string TransactionId { get; set; }
    }

}
