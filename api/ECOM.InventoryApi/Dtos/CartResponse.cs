using ECOM.InventoryApi.Data.DataModels;

namespace ECOM.InventoryApi.Dtos;

public record CartResponse(
    int Id,
    int UserId,
    List<CartItem> Items,
    decimal Subtotal,
    int ItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
