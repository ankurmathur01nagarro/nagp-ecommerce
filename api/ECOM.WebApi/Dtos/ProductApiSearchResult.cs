namespace ECOM.WebApi.Dtos;

/// <summary>Internal DTO — mirrors ProductApi's ProductSearchResponse for deserialization.</summary>
public record ProductApiSearchResult(
    List<ProductApiItem> Items,
    int TotalCount,
    int Page,
    int PageSize,
    ProductFacetsDto Facets);
