using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Frontend.Models;

namespace Frontend.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public HomeController(ILogger<HomeController> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> Index()
    {
        var client = _httpClientFactory.CreateClient("ApiGateway");
        var response = await client.GetAsync("/api/categories");

        List<CategoryDto> allCategories = new List<CategoryDto>();

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            allCategories = JsonSerializer.Deserialize<List<CategoryDto>>(result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CategoryDto>();
        }


        var popularCategories = allCategories.Where(c => string.IsNullOrEmpty(c.ParentId)).Take(4).ToList();

        return View(popularCategories);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
