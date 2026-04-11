using System.Text.Json;
using ECOM.InventoryApi.Data;
using ECOM.InventoryApi.Data.DataModels;
using ECOM.InventoryApi.Data.Repositories;
using ECOM.InventoryApi.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ECOM.InventoryApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OffersController(IOfferRepository repository) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var offer = await repository.GetByIdAsync(id, ct);
        if (offer is null)
            return NotFound();

        return Ok(MapToResponse(offer));
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? productId = null,
        [FromQuery] bool? activeOnly = null,
        [FromQuery] string? couponCode = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var (items, totalCount) = await repository.GetListAsync(page, pageSize, productId, activeOnly, couponCode, ct);

        return Ok(new OfferListResponse(
            items.Select(MapToResponse).ToList(),
            totalCount,
            page,
            pageSize));
    }

    [HttpGet("active/by-product/{productId:int}")]
    public async Task<IActionResult> GetActiveForProduct(int productId, CancellationToken ct)
    {
        var offers = await repository.GetActiveForProductAsync(productId, ct);
        return Ok(offers.Select(MapToResponse).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOfferRequest request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var offer = new Offer
        {
            Name = request.Name,
            Description = request.Description,
            ProductId = request.ProductId,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            IsActive = request.IsActive,
            Rules = request.Rules is not null ? JsonSerializer.Serialize(request.Rules, JsonDefaults.CamelCase) : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        var id = await repository.CreateAsync(offer, ct);
        var created = await repository.GetByIdAsync(id, ct);

        return CreatedAtAction(nameof(GetById), new { id }, MapToResponse(created!));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOfferRequest request, CancellationToken ct)
    {
        var existing = await repository.GetByIdAsync(id, ct);
        if (existing is null)
            return NotFound();

        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.ProductId = request.ProductId;
        existing.DiscountType = request.DiscountType;
        existing.DiscountValue = request.DiscountValue;
        existing.StartsAt = request.StartsAt;
        existing.EndsAt = request.EndsAt;
        existing.IsActive = request.IsActive;
        existing.Rules = request.Rules is not null ? JsonSerializer.Serialize(request.Rules, JsonDefaults.CamelCase) : null;
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

    private static OfferResponse MapToResponse(Offer o) => new(
        o.Id,
        o.Name,
        o.Description,
        o.ProductId,
        o.DiscountType,
        o.DiscountValue,
        o.StartsAt,
        o.EndsAt,
        o.IsActive,
        o.Rules is not null ? JsonSerializer.Deserialize<OfferRules>(o.Rules, JsonDefaults.CamelCase) : null,
        o.CreatedAt,
        o.UpdatedAt);
}
