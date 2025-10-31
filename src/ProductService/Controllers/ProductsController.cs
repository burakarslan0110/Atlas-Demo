using Atlas.Common.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProductService.Services;

namespace ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IImageStorageService _imageStorageService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IProductService productService,
        IImageStorageService imageStorageService,
        ILogger<ProductsController> logger)
    {
        _productService = productService;
        _imageStorageService = imageStorageService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? categoryId = null)
    {
        var products = await _productService.GetAllAsync(page, pageSize, categoryId);
        var totalCount = await _productService.GetCountAsync(categoryId);

        return Ok(new
        {
            products,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null)
        {
            return NotFound(new { message = "Product not found" });
        }

        return Ok(product);
    }

    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var product = await _productService.GetBySlugAsync(slug);
        if (product == null)
        {
            return NotFound(new { message = "Product not found" });
        }

        return Ok(product);
    }

    [HttpGet("featured")]
    public async Task<IActionResult> GetFeatured([FromQuery] int limit = 10)
    {
        var products = await _productService.GetFeaturedAsync(limit);
        return Ok(products);
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] ProductSearchRequest request)
    {
        var result = await _productService.SearchAsync(request);
        return Ok(result);
    }

    [HttpPost("reindex")]
    public async Task<IActionResult> Reindex()
    {
        _logger.LogInformation("Starting product reindex operation");
        var count = await _productService.ReindexAllProductsAsync();
        _logger.LogInformation("Completed product reindex: {Count} products indexed", count);

        return Ok(new { message = "Reindex completed successfully", productsIndexed = count });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var product = await _productService.CreateAsync(dto);
        _logger.LogInformation("Product created: {ProductId}", product.Id);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] ProductDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var success = await _productService.UpdateAsync(id, dto);
        if (!success)
        {
            return NotFound(new { message = "Product not found" });
        }

        _logger.LogInformation("Product updated: {ProductId}", id);
        return NoContent();
    }

    [HttpPost("{id}/upload-image")]
    public async Task<IActionResult> UploadImage(string id, IFormFile image)
    {
        if (image == null || image.Length == 0)
        {
            return BadRequest(new { message = "No image file provided" });
        }


        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(image.ContentType.ToLower()))
        {
            return BadRequest(new { message = "Invalid image type. Only JPEG, PNG, and WebP are allowed" });
        }


        const long maxFileSize = 5 * 1024 * 1024;
        if (image.Length > maxFileSize)
        {
            return BadRequest(new { message = "Image size exceeds 5MB limit" });
        }

        try
        {

            var product = await _productService.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound(new { message = "Product not found" });
            }


            using var stream = image.OpenReadStream();
            var imageUrl = await _imageStorageService.UploadImageAsync(
                image.FileName,
                image.ContentType,
                stream);

            _logger.LogInformation("Image uploaded for product {ProductId}: {ImageUrl}", id, imageUrl);

            return Ok(new { imageUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload image for product {ProductId}", id);
            return StatusCode(500, new { message = "Failed to upload image" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var success = await _productService.DeleteAsync(id);
        if (!success)
        {
            return NotFound(new { message = "Product not found" });
        }

        _logger.LogInformation("Product deleted: {ProductId}", id);
        return NoContent();
    }
}
