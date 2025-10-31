using System.Security.Claims;
using Atlas.Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;
    private readonly ILogger<CartController> _logger;

    public CartController(ICartService cartService, ILogger<CartController> logger)
    {
        _cartService = cartService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetCart()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { message = "User ID not found in token" });
        }

        var cart = await _cartService.GetCartAsync(userId);
        return Ok(cart);
    }

    [HttpPost("add")]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
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

        var success = await _cartService.AddToCartAsync(userId, request);
        if (!success)
        {
            return BadRequest(new { message = "Failed to add product to cart. Product may not exist or insufficient stock." });
        }

        var cart = await _cartService.GetCartAsync(userId);
        _logger.LogInformation("Product {ProductId} added to cart for user {UserId}", request.ProductId, userId);

        return Ok(cart);
    }

    [HttpPut("{productId}")]
    public async Task<IActionResult> UpdateCartItem(string productId, [FromBody] UpdateCartItemRequest request)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { message = "User ID not found in token" });
        }

        var success = await _cartService.UpdateCartItemAsync(userId, productId, request.Quantity);
        if (!success)
        {
            return NotFound(new { message = "Product not found in cart" });
        }

        var cart = await _cartService.GetCartAsync(userId);
        return Ok(cart);
    }

    [HttpDelete("{productId}")]
    public async Task<IActionResult> RemoveFromCart(string productId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { message = "User ID not found in token" });
        }

        var success = await _cartService.RemoveFromCartAsync(userId, productId);
        if (!success)
        {
            return NotFound(new { message = "Product not found in cart" });
        }

        _logger.LogInformation("Product {ProductId} removed from cart for user {UserId}", productId, userId);

        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { message = "User ID not found in token" });
        }

        await _cartService.ClearCartAsync(userId);
        _logger.LogInformation("Cart cleared for user {UserId}", userId);

        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

public class UpdateCartItemRequest
{
    public int Quantity { get; set; }
}
