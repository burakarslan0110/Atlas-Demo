using ProductService.Models;

namespace ProductService.Repositories;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(string id);
    Task<Category?> GetBySlugAsync(string slug);
    Task<List<Category>> GetAllAsync();
    Task<List<Category>> GetRootCategoriesAsync();
    Task<List<Category>> GetChildrenAsync(string parentId);
    Task<Category> CreateAsync(Category category);
    Task<bool> UpdateAsync(Category category);
    Task<bool> DeleteAsync(string id);
}
