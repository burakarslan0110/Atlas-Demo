using Atlas.Common.DTOs;

namespace OrderService.Services;

public interface ICartService
{
    Task<CartDto> GetCartAsync(Guid userId);
    Task<bool> AddToCartAsync(Guid userId, AddToCartRequest request);
    Task<bool> UpdateCartItemAsync(Guid userId, string productId, int quantity);
    Task<bool> RemoveFromCartAsync(Guid userId, string productId);
    Task<bool> ClearCartAsync(Guid userId);
}
