using System.Security.Claims;
using Atlas.Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { message = "User ID not found in token" });
        }

        var order = await _orderService.CreateOrderAsync(userId, request);
        if (order == null)
        {
            return BadRequest(new { message = "Failed to create order. Cart may be empty." });
        }

        _logger.LogInformation("Order {OrderId} created for user {UserId}", order.Id, userId);

        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { message = "User ID not found in token" });
        }

        var orders = await _orderService.GetUserOrdersAsync(userId, page, pageSize);
        return Ok(new { orders, page, pageSize });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderById(Guid id)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { message = "User ID not found in token" });
        }

        var order = await _orderService.GetOrderByIdAsync(id, userId);
        if (order == null)
        {
            return NotFound(new { message = "Order not found" });
        }

        return Ok(order);
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid id)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { message = "User ID not found in token" });
        }

        var success = await _orderService.CancelOrderAsync(id, userId);
        if (!success)
        {
            return BadRequest(new { message = "Failed to cancel order. Order may not exist or cannot be cancelled." });
        }

        _logger.LogInformation("Order {OrderId} cancelled by user {UserId}", id, userId);

        return Ok(new { message = "Order cancelled successfully" });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
