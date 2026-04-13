namespace ECOM.WebApi.Dtos;

public record ProductCatalogResponse(
    List<ProductCatalogItem> Items,
    int TotalCount,
    int Page,
    int PageSize,
    ProductFacetsDto Facets);
