using Atlas.Common.DTOs;
using ProductService.Models;

namespace ProductService.Services;

public interface IProductService
{
    Task<ProductDto?> GetByIdAsync(string id);
    Task<ProductDto?> GetBySlugAsync(string slug);
    Task<List<ProductDto>> GetAllAsync(int page = 1, int pageSize = 20, string? categoryId = null);
    Task<long> GetCountAsync(string? categoryId = null);
    Task<ProductDto> CreateAsync(ProductDto dto);
    Task<bool> UpdateAsync(string id, ProductDto dto);
    Task<bool> DeleteAsync(string id);
    Task<List<ProductDto>> GetFeaturedAsync(int limit = 10);
    Task<ProductSearchResponse> SearchAsync(ProductSearchRequest request);
    Task<int> ReindexAllProductsAsync();
    Task<bool> DecreaseStockAsync(string productId, int quantity);
}
