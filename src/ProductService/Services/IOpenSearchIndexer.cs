using ProductService.Models;
using Atlas.Common.DTOs;

namespace ProductService.Services;

public interface IOpenSearchIndexer
{
    Task IndexProductAsync(Product product);
    Task UpdateProductAsync(Product product);
    Task DeleteProductAsync(string productId);
    Task<ProductSearchResponse> SearchProductsAsync(ProductSearchRequest request);
}
