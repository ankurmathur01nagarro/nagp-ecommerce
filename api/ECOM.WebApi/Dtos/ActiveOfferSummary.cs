namespace ECOM.WebApi.Dtos;

public record ActiveOfferSummary(
    string Name,
    string DiscountType,
    decimal DiscountValue,
    DateTimeOffset EndsAt);
