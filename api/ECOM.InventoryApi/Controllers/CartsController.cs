using System.Text.Json;
using ECOM.InventoryApi.Data;
using ECOM.InventoryApi.Data.DataModels;
using ECOM.InventoryApi.Data.Repositories;
using ECOM.InventoryApi.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ECOM.InventoryApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CartsController(ICartRepository repository) : ControllerBase
{
    [HttpGet("{userId:int}")]
    public async Task<IActionResult> GetByUser(int userId, CancellationToken ct)
    {
        var cart = await repository.GetByUserIdAsync(userId, ct);
        if (cart is null)
            return NotFound();

        return Ok(MapToResponse(cart));
    }

    [HttpPost("{userId:int}/items")]
    public async Task<IActionResult> AddItem(int userId, [FromBody] AddCartItemRequest request, CancellationToken ct)
    {
        if (request.Quantity <= 0)
            return BadRequest(new { error = "Quantity must be greater than zero." });

        var cart = await repository.GetOrCreateByUserIdAsync(userId, ct);
        var items = DeserializeItems(cart.Items);

        var existing = items.FirstOrDefault(i => i.ProductId == request.ProductId);
        if (existing is not null)
        {
            existing.Quantity += request.Quantity;
        }
        else
        {
            items.Add(new CartItem
            {
                ProductId = request.ProductId,
                Sku = request.Sku,
                Name = request.Name,
                UnitPrice = request.UnitPrice,
                Quantity = request.Quantity,
                ImageUrl = request.ImageUrl,
                AppliedOfferId = request.AppliedOfferId,
                AddedAt = DateTimeOffset.UtcNow
            });
        }

        await repository.SetItemsAsync(userId, SerializeItems(items), ct);
        var updated = await repository.GetByUserIdAsync(userId, ct);
        return Ok(MapToResponse(updated!));
    }

    [HttpPut("{userId:int}/items/{productId:int}")]
    public async Task<IActionResult> UpdateItem(int userId, int productId, [FromBody] UpdateCartItemRequest request, CancellationToken ct)
    {
        var cart = await repository.GetByUserIdAsync(userId, ct);
        if (cart is null)
            return NotFound();

        var items = DeserializeItems(cart.Items);
        var item = items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null)
            return NotFound();

        if (request.Quantity <= 0)
            items.Remove(item);
        else
            item.Quantity = request.Quantity;

        await repository.SetItemsAsync(userId, SerializeItems(items), ct);
        var updated = await repository.GetByUserIdAsync(userId, ct);
        return Ok(MapToResponse(updated!));
    }

    [HttpDelete("{userId:int}/items/{productId:int}")]
    public async Task<IActionResult> RemoveItem(int userId, int productId, CancellationToken ct)
    {
        var cart = await repository.GetByUserIdAsync(userId, ct);
        if (cart is null)
            return NotFound();

        var items = DeserializeItems(cart.Items);
        var removed = items.RemoveAll(i => i.ProductId == productId);
        if (removed == 0)
            return NotFound();

        await repository.SetItemsAsync(userId, SerializeItems(items), ct);
        var updated = await repository.GetByUserIdAsync(userId, ct);
        return Ok(MapToResponse(updated!));
    }

    [HttpPost("{userId:int}/clear")]
    public async Task<IActionResult> Clear(int userId, CancellationToken ct)
    {
        var ok = await repository.ClearAsync(userId, ct);
        if (!ok)
            return NotFound();

        var updated = await repository.GetByUserIdAsync(userId, ct);
        return Ok(MapToResponse(updated!));
    }

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> Delete(int userId, CancellationToken ct)
    {
        var deleted = await repository.DeleteAsync(userId, ct);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    private static List<CartItem> DeserializeItems(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<CartItem>>(json, JsonDefaults.CamelCase) ?? [];

    private static string SerializeItems(List<CartItem> items) =>
        JsonSerializer.Serialize(items, JsonDefaults.CamelCase);

    private static CartResponse MapToResponse(Cart c)
    {
        var items = DeserializeItems(c.Items);
        return new CartResponse(
            c.Id,
            c.UserId,
            items,
            items.Sum(i => i.UnitPrice * i.Quantity),
            items.Sum(i => i.Quantity),
            c.CreatedAt,
            c.UpdatedAt);
    }
}
