using Atlas.Common.DTOs;

namespace OrderService.Services;

public interface IOrderService
{
    Task<OrderDto?> CreateOrderAsync(Guid userId, CreateOrderRequest request);
    Task<OrderDto?> GetOrderByIdAsync(Guid orderId, Guid userId);
    Task<List<OrderDto>> GetUserOrdersAsync(Guid userId, int page = 1, int pageSize = 20);
    Task<bool> CancelOrderAsync(Guid orderId, Guid userId);
}
