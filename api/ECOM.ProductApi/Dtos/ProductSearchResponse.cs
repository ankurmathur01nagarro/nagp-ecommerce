namespace ECOM.ProductApi.Dtos;

public record ProductSearchResponse(
    List<ProductResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    ProductFacetsResponse Facets);
