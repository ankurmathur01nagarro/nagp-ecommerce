using ECOM.ProductApi.Data.DataModels;

namespace ECOM.ProductApi.Dtos;

public record ProductResponse(
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
    List<ProductImage>? Images,
    ProductMetadata? Metadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
