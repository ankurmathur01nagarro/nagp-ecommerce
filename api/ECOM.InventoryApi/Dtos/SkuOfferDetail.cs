namespace ECOM.InventoryApi.Dtos;

public record SkuOfferDetail(
    int OfferId,
    string Name,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);
