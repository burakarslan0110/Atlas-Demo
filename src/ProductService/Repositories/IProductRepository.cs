using ProductService.Models;

namespace ProductService.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(string id);
    Task<Product?> GetBySlugAsync(string slug);
    Task<List<Product>> GetAllAsync(int page = 1, int pageSize = 20, string? categoryId = null, bool? isActive = null);
    Task<long> GetCountAsync(string? categoryId = null, bool? isActive = null);
    Task<Product> CreateAsync(Product product);
    Task<bool> UpdateAsync(Product product);
    Task<bool> DeleteAsync(string id);
    Task<List<Product>> GetFeaturedAsync(int limit = 10);
    Task<bool> ExistsBySlugAsync(string slug);
}
