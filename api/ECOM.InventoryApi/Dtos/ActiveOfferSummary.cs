namespace ECOM.InventoryApi.Dtos;

public record ActiveOfferSummary(
    string Name,
    string DiscountType,
    decimal DiscountValue,
    DateTimeOffset EndsAt);
