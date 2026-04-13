namespace ECOM.WebApi.Dtos;

/// <summary>Internal DTO — mirrors ProductApi's ProductListResponse for deserialization.</summary>
public record ProductApiListResult(
    List<ProductApiItem> Items,
    int TotalCount,
    int Page,
    int PageSize);
