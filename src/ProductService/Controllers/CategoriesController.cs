using Atlas.Common.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProductService.Models;
using ProductService.Repositories;

namespace ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryRepository _categoryRepo;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(ICategoryRepository categoryRepo, ILogger<CategoriesController> logger)
    {
        _categoryRepo = categoryRepo;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var categories = await _categoryRepo.GetAllAsync();
        var categoryDtos = categories.Select(MapToDto).ToList();


        var rootCategories = categoryDtos.Where(c => c.ParentId == null).ToList();
        foreach (var root in rootCategories)
        {
            root.Children = BuildCategoryTree(root.Id, categoryDtos);
        }

        return Ok(rootCategories);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var category = await _categoryRepo.GetByIdAsync(id);
        if (category == null)
        {
            return NotFound(new { message = "Category not found" });
        }

        return Ok(MapToDto(category));
    }

    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var category = await _categoryRepo.GetBySlugAsync(slug);
        if (category == null)
        {
            return NotFound(new { message = "Category not found" });
        }

        return Ok(MapToDto(category));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CategoryDto dto)
    {
        var category = new Category
        {
            Name = dto.Name,
            Slug = dto.Slug,
            ParentId = dto.ParentId,
            Path = dto.Path
        };

        category = await _categoryRepo.CreateAsync(category);
        _logger.LogInformation("Category created: {CategoryId}", category.Id);

        return CreatedAtAction(nameof(GetById), new { id = category.Id }, MapToDto(category));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] CategoryDto dto)
    {
        var existing = await _categoryRepo.GetByIdAsync(id);
        if (existing == null)
        {
            return NotFound(new { message = "Category not found" });
        }

        existing.Name = dto.Name;
        existing.Slug = dto.Slug;
        existing.ParentId = dto.ParentId;
        existing.Path = dto.Path;

        await _categoryRepo.UpdateAsync(existing);
        _logger.LogInformation("Category updated: {CategoryId}", id);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var success = await _categoryRepo.DeleteAsync(id);
        if (!success)
        {
            return NotFound(new { message = "Category not found" });
        }

        _logger.LogInformation("Category deleted: {CategoryId}", id);
        return NoContent();
    }

    private CategoryDto MapToDto(Category category)
    {
        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            ParentId = category.ParentId,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            Path = category.Path
        };
    }

    private List<CategoryDto> BuildCategoryTree(string parentId, List<CategoryDto> allCategories)
    {
        var children = allCategories.Where(c => c.ParentId == parentId).ToList();
        foreach (var child in children)
        {
            child.Children = BuildCategoryTree(child.Id, allCategories);
        }
        return children;
    }
}
