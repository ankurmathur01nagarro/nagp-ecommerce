namespace ECOM.WebApi.Dtos;

public record ProductSearchRequest(
    int Page = 1,
    int PageSize = 20,
    List<string>? Colors = null,
    bool? InStock = null,
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
