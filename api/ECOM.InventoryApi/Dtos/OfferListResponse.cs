namespace ECOM.InventoryApi.Dtos;

public record OfferListResponse(
    List<OfferResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
