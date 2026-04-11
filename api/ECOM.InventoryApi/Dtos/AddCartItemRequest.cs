namespace ECOM.InventoryApi.Dtos;

public record AddCartItemRequest(
    int ProductId,
    string Sku,
    string Name,
    decimal UnitPrice,
    int Quantity,
    string? ImageUrl,
    int? AppliedOfferId);
