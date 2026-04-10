using System.Text.Json;
using ECOM.ProductApi.Data;
using ECOM.ProductApi.Data.DataModels;
using ECOM.ProductApi.Data.Repositories;
using ECOM.ProductApi.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ECOM.ProductApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(IProductRepository repository) : ControllerBase
{

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var product = await repository.GetByIdAsync(id, ct);
        if (product is null)
            return NotFound();

        return Ok(MapToResponse(product));
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? category = null,
        [FromQuery] string? brand = null,
        [FromQuery] string? tag = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var (items, totalCount) = await repository.GetListAsync(page, pageSize, category, brand, tag, ct);

        return Ok(new ProductListResponse(
            items.Select(MapToResponse).ToList(),
            totalCount,
            page,
            pageSize));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var product = new Product
        {
            Name = request.Name,
            Sku = request.Sku,
            ShortDescription = request.ShortDescription,
            Description = request.Description,
            Price = request.Price,
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            Images = request.Images is not null ? JsonSerializer.Serialize(request.Images, JsonDefaults.CamelCase) : null,
            Metadata = request.Metadata is not null ? JsonSerializer.Serialize(request.Metadata, JsonDefaults.CamelCase) : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        var id = await repository.CreateAsync(product, ct);
        var created = await repository.GetByIdAsync(id, ct);

        return CreatedAtAction(nameof(GetById), new { id }, MapToResponse(created!));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        var existing = await repository.GetByIdAsync(id, ct);
        if (existing is null)
            return NotFound();

        existing.Name = request.Name;
        existing.Sku = request.Sku;
        existing.ShortDescription = request.ShortDescription;
        existing.Description = request.Description;
        existing.Price = request.Price;
        existing.CategoryId = request.CategoryId;
        existing.BrandId = request.BrandId;
        existing.Images = request.Images is not null ? JsonSerializer.Serialize(request.Images, JsonDefaults.CamelCase) : null;
        existing.Metadata = request.Metadata is not null ? JsonSerializer.Serialize(request.Metadata, JsonDefaults.CamelCase) : null;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await repository.UpdateAsync(existing, ct);
        var updated = await repository.GetByIdAsync(id, ct);

        return Ok(MapToResponse(updated!));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await repository.DeleteAsync(id, ct);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    private static ProductResponse MapToResponse(Product p) => new(
        p.Id,
        p.Name,
        p.Sku,
        p.ShortDescription,
        p.Description,
        p.Price,
        p.CategoryId,
        p.Category?.Name,
        p.BrandId,
        p.Brand?.Name,
        p.Images is not null ? JsonSerializer.Deserialize<List<ProductImage>>(p.Images, JsonDefaults.CamelCase) : null,
        p.Metadata is not null ? JsonSerializer.Deserialize<ProductMetadata>(p.Metadata, JsonDefaults.CamelCase) : null,
        p.CreatedAt,
        p.UpdatedAt);
}
