using Atlas.Common.Constants;
using Atlas.Common.DTOs;
using Atlas.EventBus;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;

namespace OrderService.Services;

public class OrderServiceImpl : IOrderService
{
    private readonly OrderDbContext _context;
    private readonly ICartService _cartService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<OrderServiceImpl> _logger;

    public OrderServiceImpl(
        OrderDbContext context,
        ICartService cartService,
        IEventPublisher eventPublisher,
        ILogger<OrderServiceImpl> logger)
    {
        _context = context;
        _cartService = cartService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<OrderDto?> CreateOrderAsync(Guid userId, CreateOrderRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {

            var cart = await _cartService.GetCartAsync(userId);

            if (cart.Items == null || cart.Items.Count == 0)
            {
                _logger.LogWarning("Cannot create order - cart is empty for user {UserId}", userId);
                return null;
            }


            var order = new Order
            {
                UserId = userId,
                Total = cart.Total,
                Status = AppConstants.OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };


            foreach (var cartItem in cart.Items)
            {
                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = cartItem.ProductId,
                    ProductName = cartItem.ProductName,
                    Quantity = cartItem.Quantity,
                    Price = cartItem.Price
                };

                order.Items.Add(orderItem);
            }


            var payment = new Payment
            {
                OrderId = order.Id,
                Amount = cart.Total,
                Method = request.PaymentMethod,
                Status = AppConstants.PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };


            _context.Orders.Add(order);
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();


            payment.Status = AppConstants.PaymentStatus.Success;
            payment.TransactionId = Guid.NewGuid().ToString();
            order.Status = AppConstants.OrderStatus.Processing;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();


            await _cartService.ClearCartAsync(userId);


            await transaction.CommitAsync();


            await _eventPublisher.PublishAsync(
                AppConstants.Exchanges.OrderEvents,
                AppConstants.EventTypes.OrderCreated,
                new
                {
                    OrderId = order.Id,
                    UserId = userId,
                    Email = request.Email ?? string.Empty,
                    UserName = request.UserName ?? string.Empty,
                    PhoneNumber = request.PhoneNumber,
                    TotalAmount = order.Total,
                    OrderDate = order.CreatedAt,
                    Total = order.Total,
                    ItemCount = order.Items.Count,
                    Items = order.Items.Select(i => new
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        Price = i.Price
                    }).ToList(),
                    CreatedAt = order.CreatedAt
                }
            );

            _logger.LogInformation("Order {OrderId} created successfully for user {UserId} with total {Total}",
                order.Id, userId, order.Total);

            return MapToDto(order);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating order for user {UserId}", userId);
            return null;
        }
    }

    public async Task<OrderDto?> GetOrderByIdAsync(Guid orderId, Guid userId)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

        if (order == null)
        {
            return null;
        }

        return MapToDto(order);
    }

    public async Task<List<OrderDto>> GetUserOrdersAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        var skip = (page - 1) * pageSize;

        var orders = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return orders.Select(MapToDto).ToList();
    }

    public async Task<bool> CancelOrderAsync(Guid orderId, Guid userId)
    {
        var order = await _context.Orders
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for user {UserId}", orderId, userId);
            return false;
        }


        if (order.Status != AppConstants.OrderStatus.Pending &&
            order.Status != AppConstants.OrderStatus.Processing)
        {
            _logger.LogWarning("Cannot cancel order {OrderId} with status {Status}", orderId, order.Status);
            return false;
        }

        order.Status = AppConstants.OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;

        if (order.Payment != null)
        {
            order.Payment.Status = AppConstants.PaymentStatus.Failed;
        }

        await _context.SaveChangesAsync();


        await _eventPublisher.PublishAsync(
            AppConstants.Exchanges.OrderEvents,
            AppConstants.EventTypes.OrderCancelled,
            new
            {
                OrderId = order.Id,
                UserId = userId,
                CancelledAt = order.UpdatedAt
            }
        );

        _logger.LogInformation("Order {OrderId} cancelled for user {UserId}", orderId, userId);

        return true;
    }

    private OrderDto MapToDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            UserId = order.UserId,
            Total = order.Total,
            Status = order.Status,
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                Price = i.Price
            }).ToList(),
            Payment = order.Payment == null ? null : new PaymentDto
            {
                Id = order.Payment.Id,
                Amount = order.Payment.Amount,
                Method = order.Payment.Method,
                Status = order.Payment.Status,
                CreatedAt = order.Payment.CreatedAt
            },
            CreatedAt = order.CreatedAt
        };
    }
}
