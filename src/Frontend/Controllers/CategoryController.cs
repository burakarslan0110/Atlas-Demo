using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Frontend.Models;

namespace Frontend.Controllers;

public class CategoryController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CategoryController> _logger;

    public CategoryController(IHttpClientFactory httpClientFactory, ILogger<CategoryController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var client = _httpClientFactory.CreateClient("ApiGateway");
        var response = await client.GetAsync("/api/categories");

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            var categories = JsonSerializer.Deserialize<List<CategoryDto>>(result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return View(categories ?? new List<CategoryDto>());
        }

        return View(new List<CategoryDto>());
    }

    public async Task<IActionResult> Products(string id, string slug)
    {
        var client = _httpClientFactory.CreateClient("ApiGateway");


        var allCategoriesResponse = await client.GetAsync("/api/categories");
        List<CategoryDto> allCategories = new List<CategoryDto>();

        if (allCategoriesResponse.IsSuccessStatusCode)
        {
            var allCategoriesResult = await allCategoriesResponse.Content.ReadAsStringAsync();
            allCategories = JsonSerializer.Deserialize<List<CategoryDto>>(allCategoriesResult, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CategoryDto>();
        }


        CategoryDto? category = FindCategoryById(id, allCategories);


        var searchRequest = new
        {
            query = "",
            categoryId = id,
            page = 1,
            pageSize = 20,
            sortBy = "relevance"
        };

        var content = new StringContent(JsonSerializer.Serialize(searchRequest), System.Text.Encoding.UTF8, "application/json");
        var productsResponse = await client.PostAsync("/api/products/search", content);

        SearchResult products = new SearchResult();

        if (productsResponse.IsSuccessStatusCode)
        {
            var productsResult = await productsResponse.Content.ReadAsStringAsync();
            products = JsonSerializer.Deserialize<SearchResult>(productsResult, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new SearchResult();
        }

        ViewData["Category"] = category;
        return View(products);
    }

    private CategoryDto? FindCategoryById(string id, List<CategoryDto> categories)
    {
        foreach (var category in categories)
        {
            if (category.Id == id)
                return category;

            var found = FindCategoryById(id, category.Children);
            if (found != null)
                return found;
        }
        return null;
    }
}
