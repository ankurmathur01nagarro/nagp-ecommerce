namespace ECOM.ProductApi.Dtos;

/// <summary>
/// Request body for POST /api/products/search.
/// Popularity sorting falls back to name ordering (no popularity column in schema).
/// InStock filtering is intentionally absent — availability is owned by InventoryApi
/// and is applied at the WebApi (BFF) layer after a cross-service stock lookup.
/// </summary>
public record ProductSearchRequest(
    int Page = 1,
    int PageSize = 20,
    List<string>? Colors = null,
    List<string>? Sizes = null,
    decimal? PriceMin = null,
    decimal? PriceMax = null,
    List<string>? Brands = null,
    List<string>? Tags = null,
    string? Category = null,
    string? Gender = null,
    int? RatingMin = null,
    int? RatingMax = null,
    string SortBy = "name",    // "name" | "price" | "rating" | "popularity"
    string SortDir = "asc"     // "asc" | "desc"
);
