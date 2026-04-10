namespace ECOM.ProductApi.Dtos;

public record ProductListResponse(
    List<ProductResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
