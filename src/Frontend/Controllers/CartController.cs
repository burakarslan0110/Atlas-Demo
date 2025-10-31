using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Frontend.Controllers;

[Authorize]
public class CartController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CartController> _logger;

    public CartController(IHttpClientFactory httpClientFactory, ILogger<CartController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var token = HttpContext.Session.GetString("jwt_token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToAction("Login", "Account");
        }

        var client = _httpClientFactory.CreateClient("ApiGateway");
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var response = await client.GetAsync("/api/cart");

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            ViewBag.CartData = result;
        }

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetCartCount()
    {
        var token = HttpContext.Session.GetString("jwt_token");
        if (string.IsNullOrEmpty(token))
        {
            return Json(new { count = 0 });
        }

        try
        {
            var client = _httpClientFactory.CreateClient("ApiGateway");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var response = await client.GetAsync("/api/cart");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                var cart = JsonSerializer.Deserialize<Atlas.Common.DTOs.CartDto>(result, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var totalItems = cart?.Items?.Sum(item => item.Quantity) ?? 0;
                return Json(new { count = totalItems });
            }

            return Json(new { count = 0 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching cart count");
            return Json(new { count = 0 });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Add(string productId, int quantity = 1)
    {
        var token = HttpContext.Session.GetString("jwt_token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("ApiGateway");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var requestData = new { ProductId = productId, Quantity = quantity };
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/cart/add", content);

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Ürün sepete eklendi!";
                _logger.LogInformation("Product {ProductId} added to cart successfully", productId);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                TempData["Error"] = "Ürün sepete eklenirken bir hata oluştu.";
                _logger.LogWarning("Failed to add product {ProductId} to cart: {Error}", productId, errorContent);
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Bir hata oluştu. Lütfen tekrar deneyin.";
            _logger.LogError(ex, "Error adding product {ProductId} to cart", productId);
        }

        return RedirectToAction("Index", "Cart");
    }

    [HttpPost]
    public async Task<IActionResult> UpdateQuantity(string productId, int quantity)
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

            var requestData = new { Quantity = quantity };
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PutAsync($"/api/cart/{productId}", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Cart item {ProductId} quantity updated to {Quantity}", productId, quantity);
                return Ok();
            }
            else
            {
                _logger.LogWarning("Failed to update cart item {ProductId}", productId);
                return BadRequest();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cart item {ProductId}", productId);
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Remove(string productId)
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

            var response = await client.DeleteAsync($"/api/cart/{productId}");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Product {ProductId} removed from cart", productId);
                return Ok();
            }
            else
            {
                _logger.LogWarning("Failed to remove product {ProductId} from cart", productId);
                return BadRequest();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing product {ProductId} from cart", productId);
            return StatusCode(500);
        }
    }

    public async Task<IActionResult> Checkout()
    {
        var token = HttpContext.Session.GetString("jwt_token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToAction("Login", "Account");
        }

        var client = _httpClientFactory.CreateClient("ApiGateway");
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var response = await client.GetAsync("/api/cart");

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            ViewBag.CartData = result;
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> PlaceOrder(string paymentMethod = "credit_card", string? email = null, string? userName = null, string? phoneNumber = null)
    {
        var token = HttpContext.Session.GetString("jwt_token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("ApiGateway");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var requestData = new {
                PaymentMethod = paymentMethod,
                Email = email,
                UserName = userName,
                PhoneNumber = phoneNumber
            };
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/orders", content);

            if (response.IsSuccessStatusCode)
            {
                var orderJson = await response.Content.ReadAsStringAsync();
                var order = JsonSerializer.Deserialize<JsonElement>(orderJson);
                var orderId = order.GetProperty("id").GetString();

                TempData["Success"] = "Siparişiniz başarıyla oluşturuldu!";
                _logger.LogInformation("Order created successfully");

                return RedirectToAction("OrderConfirmation", new { orderId });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                TempData["Error"] = "Sipariş oluşturulurken bir hata oluştu.";
                _logger.LogWarning("Failed to create order: {Error}", errorContent);
                return RedirectToAction("Checkout");
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Bir hata oluştu. Lütfen tekrar deneyin.";
            _logger.LogError(ex, "Error creating order");
            return RedirectToAction("Checkout");
        }
    }

    public async Task<IActionResult> OrderConfirmation(string orderId)
    {
        var token = HttpContext.Session.GetString("jwt_token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToAction("Login", "Account");
        }

        var client = _httpClientFactory.CreateClient("ApiGateway");
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var response = await client.GetAsync($"/api/orders/{orderId}");

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            ViewBag.OrderData = result;
        }

        return View();
    }
}
