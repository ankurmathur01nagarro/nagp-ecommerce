namespace ECOM.WebApi.Dtos;

public record CartDetailsItem(
    string ProductId,
    int Quantity,
    decimal Price,
    ActiveOfferSummary? Offer);
