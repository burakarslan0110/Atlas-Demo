using System.Text.Json;
using Atlas.Common.Constants;
using Atlas.Common.DTOs;
using Atlas.EventBus;
using ProductService.Models;
using ProductService.Repositories;
using StackExchange.Redis;

namespace ProductService.Services;

public class ProductServiceImpl : IProductService
{
    private readonly IProductRepository _productRepo;
    private readonly IOpenSearchIndexer _searchIndexer;
    private readonly IEventPublisher _eventPublisher;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ProductServiceImpl> _logger;

    public ProductServiceImpl(
        IProductRepository productRepo,
        IOpenSearchIndexer searchIndexer,
        IEventPublisher eventPublisher,
        IConnectionMultiplexer redis,
        ILogger<ProductServiceImpl> logger)
    {
        _productRepo = productRepo;
        _searchIndexer = searchIndexer;
        _eventPublisher = eventPublisher;
        _redis = redis;
        _logger = logger;
    }

    public async Task<ProductDto?> GetByIdAsync(string id)
    {

        var db = _redis.GetDatabase();
        var cacheKey = string.Format(AppConstants.CacheKeys.ProductDetail, id);
        var cached = await db.StringGetAsync(cacheKey);

        if (!cached.IsNullOrEmpty)
        {
            _logger.LogInformation("Product {ProductId} retrieved from cache", id);
            return JsonSerializer.Deserialize<ProductDto>(cached!);
        }


        var product = await _productRepo.GetByIdAsync(id);
        if (product == null) return null;

        var dto = MapToDto(product);


        await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(dto), TimeSpan.FromSeconds(AppConstants.CacheDuration.ProductDetail));

        return dto;
    }

    public async Task<ProductDto?> GetBySlugAsync(string slug)
    {
        var product = await _productRepo.GetBySlugAsync(slug);
        if (product == null) return null;


        var db = _redis.GetDatabase();
        var cacheKey = string.Format(AppConstants.CacheKeys.ProductDetail, product.Id);
        var dto = MapToDto(product);
        await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(dto), TimeSpan.FromSeconds(AppConstants.CacheDuration.ProductDetail));

        return dto;
    }

    public async Task<List<ProductDto>> GetAllAsync(int page = 1, int pageSize = 20, string? categoryId = null)
    {
        var products = await _productRepo.GetAllAsync(page, pageSize, categoryId, isActive: true);
        return products.Select(MapToDto).ToList();
    }

    public async Task<long> GetCountAsync(string? categoryId = null)
    {
        return await _productRepo.GetCountAsync(categoryId, isActive: true);
    }

    public async Task<ProductDto> CreateAsync(ProductDto dto)
    {

        if (string.IsNullOrEmpty(dto.Slug))
        {
            dto.Slug = GenerateSlug(dto.Name);
        }


        if (await _productRepo.ExistsBySlugAsync(dto.Slug))
        {
            dto.Slug = $"{dto.Slug}-{Guid.NewGuid().ToString()[..8]}";
        }

        var product = MapToEntity(dto);
        product = await _productRepo.CreateAsync(product);


        await _searchIndexer.IndexProductAsync(product);


        await _eventPublisher.PublishAsync(
            AppConstants.Exchanges.ProductEvents,
            AppConstants.EventTypes.ProductCreated,
            new { ProductId = product.Id, Product = product }
        );

        _logger.LogInformation("Product created: {ProductId}", product.Id);

        return MapToDto(product);
    }

    public async Task<bool> UpdateAsync(string id, ProductDto dto)
    {
        var existingProduct = await _productRepo.GetByIdAsync(id);
        if (existingProduct == null) return false;


        existingProduct.Name = dto.Name;
        existingProduct.Description = dto.Description;
        existingProduct.Price = dto.Price;
        existingProduct.Stock = dto.Stock;
        existingProduct.Category = new Models.CategoryInfo
        {
            Id = dto.Category.Id,
            Name = dto.Category.Name,
            Path = dto.Category.Path
        };
        existingProduct.Brand = dto.Brand;
        existingProduct.Tags = dto.Tags;
        existingProduct.Images = dto.Images;
        existingProduct.Attributes = dto.Attributes;
        existingProduct.IsActive = dto.IsActive;
        existingProduct.IsFeatured = dto.IsFeatured;

        var success = await _productRepo.UpdateAsync(existingProduct);

        if (success)
        {

            var db = _redis.GetDatabase();
            var cacheKey = string.Format(AppConstants.CacheKeys.ProductDetail, id);
            await db.KeyDeleteAsync(cacheKey);


            await _searchIndexer.UpdateProductAsync(existingProduct);


            await _eventPublisher.PublishAsync(
                AppConstants.Exchanges.ProductEvents,
                AppConstants.EventTypes.ProductUpdated,
                new { ProductId = id, Product = existingProduct }
            );

            _logger.LogInformation("Product updated: {ProductId}", id);
        }

        return success;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var success = await _productRepo.DeleteAsync(id);

        if (success)
        {

            var db = _redis.GetDatabase();
            var cacheKey = string.Format(AppConstants.CacheKeys.ProductDetail, id);
            await db.KeyDeleteAsync(cacheKey);


            await _searchIndexer.DeleteProductAsync(id);


            await _eventPublisher.PublishAsync(
                AppConstants.Exchanges.ProductEvents,
                AppConstants.EventTypes.ProductDeleted,
                new { ProductId = id }
            );

            _logger.LogInformation("Product deleted: {ProductId}", id);
        }

        return success;
    }

    public async Task<List<ProductDto>> GetFeaturedAsync(int limit = 10)
    {
        var products = await _productRepo.GetFeaturedAsync(limit);
        return products.Select(MapToDto).ToList();
    }

    public async Task<ProductSearchResponse> SearchAsync(ProductSearchRequest request)
    {
        return await _searchIndexer.SearchProductsAsync(request);
    }

    public async Task<int> ReindexAllProductsAsync()
    {

        var allProducts = await _productRepo.GetAllAsync(page: 1, pageSize: 10000, categoryId: null, isActive: true);


        int indexedCount = 0;
        foreach (var product in allProducts)
        {
            try
            {
                await _searchIndexer.IndexProductAsync(product);
                indexedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to index product {ProductId}", product.Id);
            }
        }

        _logger.LogInformation("Reindexed {Count} products to OpenSearch", indexedCount);
        return indexedCount;
    }

    public async Task<bool> DecreaseStockAsync(string productId, int quantity)
    {
        try
        {
            var product = await _productRepo.GetByIdAsync(productId);
            if (product == null)
            {
                _logger.LogWarning("Product {ProductId} not found for stock decrease", productId);
                return false;
            }

            if (product.Stock < quantity)
            {
                _logger.LogWarning("Insufficient stock for product {ProductId}. Available: {Available}, Requested: {Requested}",
                    productId, product.Stock, quantity);
                return false;
            }

            product.Stock -= quantity;
            product.UpdatedAt = DateTime.UtcNow;

            var updated = await _productRepo.UpdateAsync(product);

            if (updated)
            {

                var db = _redis.GetDatabase();
                var cacheKey = string.Format(AppConstants.CacheKeys.ProductDetail, productId);
                await db.KeyDeleteAsync(cacheKey);


                await _eventPublisher.PublishAsync(
                    AppConstants.Exchanges.ProductEvents,
                    AppConstants.EventTypes.ProductStockChanged,
                    new
                    {
                        ProductId = productId,
                        OldStock = product.Stock + quantity,
                        NewStock = product.Stock,
                        Quantity = quantity,
                        ChangedAt = DateTime.UtcNow
                    }
                );


                await _searchIndexer.IndexProductAsync(product);

                _logger.LogInformation("Stock decreased for product {ProductId}. New stock: {NewStock}", productId, product.Stock);
            }

            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decreasing stock for product {ProductId}", productId);
            return false;
        }
    }

    private ProductDto MapToDto(Product product)
    {
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            Description = product.Description,
            Price = product.Price,
            Stock = product.Stock,
            Category = new Atlas.Common.DTOs.CategoryInfo
            {
                Id = product.Category.Id,
                Name = product.Category.Name,
                Path = product.Category.Path
            },
            Brand = product.Brand,
            Tags = product.Tags,
            Images = product.Images,
            AvgRating = product.AvgRating,
            ReviewCount = product.ReviewCount,
            Attributes = product.Attributes,
            IsActive = product.IsActive,
            IsFeatured = product.IsFeatured,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }

    private Product MapToEntity(ProductDto dto)
    {
        return new Product
        {
            Name = dto.Name,
            Slug = dto.Slug,
            Description = dto.Description,
            Price = dto.Price,
            Stock = dto.Stock,
            Category = new Models.CategoryInfo
            {
                Id = dto.Category.Id,
                Name = dto.Category.Name,
                Path = dto.Category.Path
            },
            Brand = dto.Brand,
            Tags = dto.Tags,
            Images = dto.Images,
            Attributes = dto.Attributes,
            IsActive = dto.IsActive,
            IsFeatured = dto.IsFeatured
        };
    }

    private string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("ğ", "g")
            .Replace("ü", "u")
            .Replace("ş", "s")
            .Replace("ı", "i")
            .Replace("ö", "o")
            .Replace("ç", "c")
            .Replace("İ", "i");
    }
}
