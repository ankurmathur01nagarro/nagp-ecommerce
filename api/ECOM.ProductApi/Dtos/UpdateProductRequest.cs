using System.ComponentModel.DataAnnotations;
using ECOM.ProductApi.Data.DataModels;

namespace ECOM.ProductApi.Dtos;

public record UpdateProductRequest(
    string Name,
    string Sku,
    string? ShortDescription,
    string? Description,
    decimal Price,
    int CategoryId,
    int BrandId,
    [AllowedValues("Men", "Women", "Unisex", null)] string? Gender,
    List<ProductImage>? Images,
    ProductMetadata? Metadata);
