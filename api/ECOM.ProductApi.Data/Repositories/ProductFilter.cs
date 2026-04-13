namespace ECOM.ProductApi.Data.Repositories;

/// <summary>
/// Filter and sort parameters for product search queries.
/// Popularity sort falls back to name ordering (no popularity column in schema).
/// InStock filtering is not applied here — it is handled at the WebApi layer via InventoryApi.
/// </summary>
public record ProductFilter(
    int Page,
    int PageSize,
    List<string>? Colors,
    List<string>? Sizes,
    decimal? PriceMin,
    decimal? PriceMax,
    List<string>? Brands,
    List<string>? Tags,
    string? Category,
    string? Gender,
    int? RatingMin,
    int? RatingMax,
    string SortBy,   // "name" | "price" | "rating" | "popularity"
    string SortDir   // "asc" | "desc"
);
