using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Frontend.Controllers;

[Authorize]
public class OrderController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OrderController> _logger;

    public OrderController(IHttpClientFactory httpClientFactory, ILogger<OrderController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IActionResult> MyOrders()
    {
        var token = HttpContext.Session.GetString("jwt_token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToAction("Login", "Account");
        }

        var client = _httpClientFactory.CreateClient("ApiGateway");
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var response = await client.GetAsync("/api/orders");

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            ViewBag.OrdersData = result;
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(string orderId)
    {
        var token = HttpContext.Session.GetString("jwt_token");
        if (string.IsNullOrEmpty(token))
        {
            return Unauthorized();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("ApiGateway");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var response = await client.PostAsync($"/api/orders/{orderId}/cancel", null);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Order {OrderId} cancelled successfully", orderId);
                return Ok(new { message = "Sipariş başarıyla iptal edildi" });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to cancel order {OrderId}: {Error}", orderId, errorContent);
                return BadRequest(new { message = "Sipariş iptal edilemedi" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
            return StatusCode(500, new { message = "Bir hata oluştu" });
        }
    }
}
