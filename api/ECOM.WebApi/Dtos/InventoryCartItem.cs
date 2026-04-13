namespace ECOM.WebApi.Dtos;

/// <summary>Internal DTO — mirrors InventoryApi's CartItem for deserialization.</summary>
public record InventoryCartItem(
    int ProductId,
    string Sku,
    string Name,
    decimal UnitPrice,
    int Quantity,
    string? ImageUrl,
    int? AppliedOfferId,
    DateTimeOffset AddedAt);
