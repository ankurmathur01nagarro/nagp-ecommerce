namespace ECOM.WebApi.Dtos;

/// <summary>Internal DTO — mirrors ProductApi's ProductResponse for deserialization.</summary>
public record ProductApiItem(
    int Id,
    string Name,
    string Sku,
    string? ShortDescription,
    string? Description,
    decimal Price,
    int CategoryId,
    string? CategoryName,
    int BrandId,
    string? BrandName,
    string? Gender,
    List<ProductImageDto>? Images,
    ProductMetadataDto? Metadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
