namespace ECOM.WebApi.Dtos;

/// <summary>Internal DTO — mirrors ProductApi's CategoryFacetResponse for deserialization.</summary>
public record CategoryFacetDto(int CategoryId, string CategoryName, int? ParentCategoryId, int Count);
