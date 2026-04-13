using System.Text.Json;
using ECOM.InventoryApi.Data;
using ECOM.InventoryApi.Data.DataModels;
using ECOM.InventoryApi.Data.Repositories;
using ECOM.InventoryApi.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ECOM.InventoryApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController(IInventoryRepository repository, IOfferRepository offerRepository) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var inventory = await repository.GetByIdAsync(id, ct);
        if (inventory is null)
            return NotFound();

        return Ok(MapToResponse(inventory));
    }

    [HttpGet("by-product/{productId:int}")]
    public async Task<IActionResult> GetByProduct(int productId, CancellationToken ct)
    {
        var inventory = await repository.GetByProductIdAsync(productId, ct);
        if (inventory is null)
            return NotFound();

        return Ok(MapToResponse(inventory));
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? lowStockOnly = null,
        [FromQuery] string? warehouseCode = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var (items, totalCount) = await repository.GetListAsync(page, pageSize, lowStockOnly, warehouseCode, ct);

        return Ok(new InventoryListResponse(
            items.Select(MapToResponse).ToList(),
            totalCount,
            page,
            pageSize));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInventoryRequest request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var inventory = new Inventory
        {
            ProductId = request.ProductId,
            Sku = request.Sku,
            Quantity = request.Quantity,
            Reserved = request.Reserved,
            LowStockThreshold = request.LowStockThreshold,
            Metadata = request.Metadata is not null ? JsonSerializer.Serialize(request.Metadata, JsonDefaults.CamelCase) : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        var id = await repository.CreateAsync(inventory, ct);
        var created = await repository.GetByIdAsync(id, ct);

        return CreatedAtAction(nameof(GetById), new { id }, MapToResponse(created!));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateInventoryRequest request, CancellationToken ct)
    {
        var existing = await repository.GetByIdAsync(id, ct);
        if (existing is null)
            return NotFound();

        existing.Sku = request.Sku;
        existing.Quantity = request.Quantity;
        existing.Reserved = request.Reserved;
        existing.LowStockThreshold = request.LowStockThreshold;
        existing.Metadata = request.Metadata is not null ? JsonSerializer.Serialize(request.Metadata, JsonDefaults.CamelCase) : null;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await repository.UpdateAsync(existing, ct);
        var updated = await repository.GetByIdAsync(id, ct);

        return Ok(MapToResponse(updated!));
    }

    [HttpPost("by-product/{productId:int}/adjust")]
    public async Task<IActionResult> Adjust(int productId, [FromBody] AdjustQuantityRequest request, CancellationToken ct)
    {
        var existing = await repository.GetByProductIdAsync(productId, ct);
        if (existing is null)
            return NotFound();

        var ok = await repository.AdjustQuantityAsync(productId, request.Delta, ct);
        if (!ok)
            return Conflict(new { error = "Adjustment would leave quantity below reservations." });

        var updated = await repository.GetByProductIdAsync(productId, ct);
        return Ok(MapToResponse(updated!));
    }

    [HttpPost("reserve")]
    public async Task<IActionResult> Reserve([FromBody] ReservationRequest request, CancellationToken ct)
    {
        var existing = await repository.GetByProductIdAsync(request.ProductId, ct);
        if (existing is null)
            return NotFound();

        var ok = await repository.ReserveAsync(request.ProductId, request.Quantity, ct);
        if (!ok)
            return Conflict(new { error = "Insufficient available stock." });

        var updated = await repository.GetByProductIdAsync(request.ProductId, ct);
        return Ok(MapToResponse(updated!));
    }

    [HttpPost("release")]
    public async Task<IActionResult> Release([FromBody] ReservationRequest request, CancellationToken ct)
    {
        var existing = await repository.GetByProductIdAsync(request.ProductId, ct);
        if (existing is null)
            return NotFound();

        await repository.ReleaseReservationAsync(request.ProductId, request.Quantity, ct);
        var updated = await repository.GetByProductIdAsync(request.ProductId, ct);
        return Ok(MapToResponse(updated!));
    }

    [HttpPost("stock")]
    public async Task<IActionResult> BulkStock([FromBody] BulkStockRequest request, CancellationToken ct)
    {
        if (request.Skus is not { Count: > 0 })
            return Ok(new BulkStockResponse([]));

        if (request.Skus.Count > 200)
            return BadRequest(new { error = "A maximum of 200 SKUs may be requested at once." });

        var skus = request.Skus.Distinct().ToArray();

        var inventories = await repository.GetBySkusAsync(skus, ct);
        var productIds = inventories.Select(i => i.ProductId).ToArray();
        var offers = await offerRepository.GetActiveForProductsAsync(productIds, ct);

        // Best offer per product: product-specific wins; catalog-wide (null ProductId) is the fallback
        var productSpecificOffers = offers
            .Where(o => o.ProductId.HasValue)
            .GroupBy(o => o.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        var catalogWideOffer = offers.FirstOrDefault(o => o.ProductId is null);

        var inventoryBySku = inventories.ToDictionary(i => i.Sku, StringComparer.OrdinalIgnoreCase);

        var items = skus.Select(sku =>
        {
            var inv = inventoryBySku.GetValueOrDefault(sku);
            var available = inv is not null ? inv.Quantity - inv.Reserved : 0;

            var offer = inv is not null
                ? productSpecificOffers.GetValueOrDefault(inv.ProductId) ?? catalogWideOffer
                : catalogWideOffer;
            var offerSummary = offer is not null
                ? new ActiveOfferSummary(offer.Name, offer.DiscountType, offer.DiscountValue, offer.EndsAt)
                : null;

            return new ProductStockSummary(sku, available, available > 0, offerSummary);
        }).ToList();

        return Ok(new BulkStockResponse(items));
    }

    [HttpPost("offers")]
    public async Task<IActionResult> OffersBySku([FromBody] OffersBySkuRequest request, CancellationToken ct)
    {
        if (request.Skus is not { Count: > 0 })
            return Ok(new OffersBySkuResponse([]));

        if (request.Skus.Count > 200)
            return BadRequest(new { error = "A maximum of 200 SKUs may be requested at once." });

        var skus = request.Skus.Distinct().ToArray();

        // Resolve SKUs → ProductIds via Inventories, then fetch all active offers in one query
        var inventories = await repository.GetBySkusAsync(skus, ct);
        var productIds = inventories.Select(i => i.ProductId).ToArray();
        var allOffers = productIds.Length > 0
            ? await offerRepository.GetActiveForProductsAsync(productIds, ct)
            : [];

        // Separate product-specific from catalog-wide
        var offersByProductId = allOffers
            .Where(o => o.ProductId.HasValue)
            .GroupBy(o => o.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
        var catalogWideOffers = allOffers.Where(o => o.ProductId is null).ToList();

        var inventoryBySku = inventories.ToDictionary(i => i.Sku, StringComparer.OrdinalIgnoreCase);

        var items = skus.Select(sku =>
        {
            var inv = inventoryBySku.GetValueOrDefault(sku);

            var productOffers = inv is not null && offersByProductId.TryGetValue(inv.ProductId, out var po)
                ? po
                : [];

            // Merge: product-specific first, then catalog-wide; deduplicate by OfferId
            var combined = productOffers
                .Concat(catalogWideOffers)
                .DistinctBy(o => o.Id)
                .OrderByDescending(o => o.DiscountValue)
                .Select(o => new SkuOfferDetail(o.Id, o.Name, o.Description, o.DiscountType, o.DiscountValue, o.StartsAt, o.EndsAt))
                .ToList();

            return new SkuOfferInfo(sku, combined);
        }).ToList();

        return Ok(new OffersBySkuResponse(items));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await repository.DeleteAsync(id, ct);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    private static InventoryResponse MapToResponse(Inventory i) => new(
        i.Id,
        i.ProductId,
        i.Sku,
        i.Quantity,
        i.Reserved,
        i.Quantity - i.Reserved,
        i.LowStockThreshold,
        i.Metadata is not null ? JsonSerializer.Deserialize<InventoryMetadata>(i.Metadata, JsonDefaults.CamelCase) : null,
        i.CreatedAt,
        i.UpdatedAt);
}
