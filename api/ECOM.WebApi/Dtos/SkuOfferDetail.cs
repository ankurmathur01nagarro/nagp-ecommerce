namespace ECOM.WebApi.Dtos;

/// <summary>Internal DTO — mirrors InventoryApi's SkuOfferDetail for deserialization.</summary>
public record SkuOfferDetail(
    int OfferId,
    string Name,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);
