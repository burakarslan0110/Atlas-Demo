using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Frontend.Models;

namespace Frontend.Controllers;

public class ProductController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProductController> _logger;

    public ProductController(IHttpClientFactory httpClientFactory, ILogger<ProductController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var client = _httpClientFactory.CreateClient("ApiGateway");

        var searchRequest = new
        {
            query = "",
            page = 1,
            pageSize = 20,
            sortBy = "relevance"
        };

        var content = new StringContent(JsonSerializer.Serialize(searchRequest), System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/products/search", content);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            var products = JsonSerializer.Deserialize<SearchResult>(result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return View(products);
        }

        return View(new SearchResult());
    }

    [HttpGet]
    public async Task<IActionResult> Search(string query)
    {
        var client = _httpClientFactory.CreateClient("ApiGateway");

        var searchRequest = new
        {
            query = query ?? "",
            page = 1,
            pageSize = 20,
            sortBy = "relevance"
        };

        var content = new StringContent(JsonSerializer.Serialize(searchRequest), System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/products/search", content);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            var products = JsonSerializer.Deserialize<SearchResult>(result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            ViewData["SearchQuery"] = query;
            return View("Index", products);
        }

        return View("Index", new SearchResult());
    }

    public async Task<IActionResult> Detail(string id)
    {
        var client = _httpClientFactory.CreateClient("ApiGateway");
        var response = await client.GetAsync($"/api/products/{id}");

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            var product = JsonSerializer.Deserialize<ProductDto>(result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return View(product);
        }

        return NotFound();
    }
}
