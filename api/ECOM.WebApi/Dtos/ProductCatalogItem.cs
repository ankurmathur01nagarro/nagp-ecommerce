namespace ECOM.WebApi.Dtos;

public record ProductCatalogItem(
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
    DateTimeOffset UpdatedAt,
    // Stock + offer fields merged from InventoryApi
    int AvailableQuantity,
    bool InStock,
    ActiveOfferSummary? ActiveOffer,
    List<SkuOfferDetail> Offers);
