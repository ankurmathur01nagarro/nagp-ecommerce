using ECOM.InventoryApi.Data.DataModels;

namespace ECOM.InventoryApi.Dtos;

public record CreateOfferRequest(
    string Name,
    string? Description,
    int? ProductId,
    string DiscountType,
    decimal DiscountValue,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsActive,
    OfferRules? Rules);
