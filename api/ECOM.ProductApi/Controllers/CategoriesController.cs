using ECOM.ProductApi.Data.Repositories;
using ECOM.ProductApi.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ECOM.ProductApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController(ICategoryRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> GetAll(CancellationToken ct)
    {
        var flat = await repository.GetAllAsync(ct);

        // Build a lookup by Id for O(n) tree assembly
        var lookup = flat.ToDictionary(c => c.Id, c => new CategoryResponse(c.Id, c.Name, []));

        var roots = new List<CategoryResponse>();

        foreach (var item in flat)
        {
            if (item.ParentCategoryId is { } parentId && lookup.TryGetValue(parentId, out var parent))
                parent.Subcategories.Add(lookup[item.Id]);
            else
                roots.Add(lookup[item.Id]);
        }

        return Ok(roots);
    }
}
