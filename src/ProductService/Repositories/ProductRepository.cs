using MongoDB.Bson;
using MongoDB.Driver;
using ProductService.Models;

namespace ProductService.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly IMongoCollection<Product> _products;

    public ProductRepository(IMongoDatabase database)
    {
        _products = database.GetCollection<Product>("products");

    }

    public async Task<Product?> GetByIdAsync(string id)
    {
        return await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Product?> GetBySlugAsync(string slug)
    {
        return await _products.Find(p => p.Slug == slug).FirstOrDefaultAsync();
    }

    public async Task<List<Product>> GetAllAsync(int page = 1, int pageSize = 20, string? categoryId = null, bool? isActive = null)
    {
        var filterBuilder = Builders<Product>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrEmpty(categoryId))
        {
            filter &= filterBuilder.Eq(p => p.Category.Id, categoryId);
        }

        if (isActive.HasValue)
        {
            filter &= filterBuilder.Eq(p => p.IsActive, isActive.Value);
        }

        var skip = (page - 1) * pageSize;

        return await _products.Find(filter)
            .Sort(Builders<Product>.Sort.Descending(p => p.CreatedAt))
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();
    }

    public async Task<long> GetCountAsync(string? categoryId = null, bool? isActive = null)
    {
        var filterBuilder = Builders<Product>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrEmpty(categoryId))
        {
            filter &= filterBuilder.Eq(p => p.Category.Id, categoryId);
        }

        if (isActive.HasValue)
        {
            filter &= filterBuilder.Eq(p => p.IsActive, isActive.Value);
        }

        return await _products.CountDocumentsAsync(filter);
    }

    public async Task<Product> CreateAsync(Product product)
    {
        product.CreatedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;
        await _products.InsertOneAsync(product);
        return product;
    }

    public async Task<bool> UpdateAsync(Product product)
    {
        product.UpdatedAt = DateTime.UtcNow;
        var result = await _products.ReplaceOneAsync(p => p.Id == product.Id, product);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _products.DeleteOneAsync(p => p.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<List<Product>> GetFeaturedAsync(int limit = 10)
    {
        return await _products.Find(p => p.IsFeatured && p.IsActive)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<bool> ExistsBySlugAsync(string slug)
    {
        var count = await _products.CountDocumentsAsync(p => p.Slug == slug);
        return count > 0;
    }
}
