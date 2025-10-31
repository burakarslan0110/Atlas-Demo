using System.Text.Json;
using Atlas.Common.Constants;
using Atlas.Common.DTOs;
using StackExchange.Redis;

namespace OrderService.Services;

public class CartService : ICartService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CartService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public CartService(
        IConnectionMultiplexer redis,
        ILogger<CartService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _redis = redis;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<CartDto> GetCartAsync(Guid userId)
    {
        var db = _redis.GetDatabase();
        var cacheKey = string.Format(AppConstants.CacheKeys.Cart, userId);
        var cartJson = await db.StringGetAsync(cacheKey);

        if (cartJson.IsNullOrEmpty)
        {
            return new CartDto { Items = new List<CartItemDto>(), Total = 0 };
        }

        var cart = JsonSerializer.Deserialize<CartDto>(cartJson!);
        return cart ?? new CartDto();
    }

    public async Task<bool> AddToCartAsync(Guid userId, AddToCartRequest request)
    {
        try
        {
            var productDto = await GetProductDetailsAsync(request.ProductId);
            if (productDto == null)
            {
                _logger.LogWarning("Product {ProductId} not found", request.ProductId);
                return false;
            }

            if (productDto.Stock < request.Quantity)
            {
                _logger.LogWarning("Insufficient stock for product {ProductId}", request.ProductId);
                return false;
            }

            var cart = await GetCartAsync(userId);

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
            if (existingItem != null)
            {
                existingItem.Quantity += request.Quantity;
                existingItem.Subtotal = existingItem.Quantity * existingItem.Price;
            }
            else
            {
                cart.Items.Add(new CartItemDto
                {
                    ProductId = request.ProductId,
                    ProductName = productDto.Name,
                    Price = productDto.Price,
                    Quantity = request.Quantity,
                    Subtotal = productDto.Price * request.Quantity,
                    ProductImage = productDto.Images?.FirstOrDefault()
                });
            }

            cart.Total = cart.Items.Sum(i => i.Subtotal);

            await SaveCartAsync(userId, cart);

            _logger.LogInformation("Added {Quantity} of product {ProductId} to cart for user {UserId}",
                request.Quantity, request.ProductId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding product to cart for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> UpdateCartItemAsync(Guid userId, string productId, int quantity)
    {
        try
        {
            if (quantity <= 0)
            {
                return await RemoveFromCartAsync(userId, productId);
            }

            var cart = await GetCartAsync(userId);
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (item == null)
            {
                _logger.LogWarning("Product {ProductId} not found in cart for user {UserId}", productId, userId);
                return false;
            }

            item.Quantity = quantity;
            item.Subtotal = item.Quantity * item.Price;
            cart.Total = cart.Items.Sum(i => i.Subtotal);

            await SaveCartAsync(userId, cart);

            _logger.LogInformation("Updated quantity of product {ProductId} to {Quantity} in cart for user {UserId}",
                productId, quantity, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cart item for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> RemoveFromCartAsync(Guid userId, string productId)
    {
        try
        {
            var cart = await GetCartAsync(userId);
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (item == null)
            {
                return false;
            }

            cart.Items.Remove(item);
            cart.Total = cart.Items.Sum(i => i.Subtotal);

            await SaveCartAsync(userId, cart);

            _logger.LogInformation("Removed product {ProductId} from cart for user {UserId}", productId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item from cart for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> ClearCartAsync(Guid userId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var cacheKey = string.Format(AppConstants.CacheKeys.Cart, userId);
            await db.KeyDeleteAsync(cacheKey);

            _logger.LogInformation("Cleared cart for user {UserId}", userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cart for user {UserId}", userId);
            return false;
        }
    }

    private async Task SaveCartAsync(Guid userId, CartDto cart)
    {
        var db = _redis.GetDatabase();
        var cacheKey = string.Format(AppConstants.CacheKeys.Cart, userId);
        var cartJson = JsonSerializer.Serialize(cart);
        await db.StringSetAsync(cacheKey, cartJson, TimeSpan.FromDays(7));
    }

    private async Task<ProductDto?> GetProductDetailsAsync(string productId)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("ProductService");
            var response = await httpClient.GetAsync($"/api/products/{productId}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ProductDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching product details for {ProductId}", productId);
            return null;
        }
    }
}
