using System.Net.Http.Json;
using ECOM.WebApi.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ECOM.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> GetAll(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("product-api");
        var response = await client.GetAsync("/api/categories", ct);
        response.EnsureSuccessStatusCode();
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>(ct);
        return Ok(categories);
    }
}
