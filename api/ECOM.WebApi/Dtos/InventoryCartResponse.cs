namespace ECOM.WebApi.Dtos;

/// <summary>Internal DTO — mirrors InventoryApi's CartResponse for deserialization.</summary>
public record InventoryCartResponse(
    int Id,
    int UserId,
    List<InventoryCartItem> Items,
    decimal Subtotal,
    int ItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
