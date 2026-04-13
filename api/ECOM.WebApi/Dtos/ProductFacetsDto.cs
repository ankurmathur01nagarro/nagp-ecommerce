namespace ECOM.WebApi.Dtos;

/// <summary>Internal DTO — mirrors ProductApi's ProductFacetsResponse for deserialization.</summary>
public record ProductFacetsDto(
    List<CategoryFacetDto> Categories,
    List<ColorFacetDto> Colors,
    List<FacetCountDto> Sizes,
    List<FacetCountDto> Brands,
    List<FacetCountDto> Tags);
