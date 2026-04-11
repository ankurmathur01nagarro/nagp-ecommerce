using ECOM.InventoryApi.Data.DataModels;

namespace ECOM.InventoryApi.Dtos;

public record OfferResponse(
    int Id,
    string Name,
    string? Description,
    int? ProductId,
    string DiscountType,
    decimal DiscountValue,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsActive,
    OfferRules? Rules,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
