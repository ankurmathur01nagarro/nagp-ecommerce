namespace ECOM.WebApi.Dtos;

public record CartDetails(
    List<CartDetailsItem> Items,
    decimal TotalPrice);
