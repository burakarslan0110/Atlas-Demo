namespace Frontend.Models;

public class SearchResult
{
    public List<ProductDto> Products { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ProductDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Brand { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public CategoryInfo? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public double AvgRating { get; set; }
    public int ReviewCount { get; set; }
}

public class CategoryInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Path { get; set; } = new();
}

public class CategoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public List<CategoryDto> Children { get; set; } = new();
    public List<string> Path { get; set; } = new();
}
