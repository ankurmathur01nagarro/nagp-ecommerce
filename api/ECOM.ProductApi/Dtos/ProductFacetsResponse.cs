namespace ECOM.ProductApi.Dtos;

public record ProductFacetsResponse(
    List<CategoryFacetResponse> Categories,
    List<ColorFacetResponse> Colors,
    List<FacetCountResponse> Sizes,
    List<FacetCountResponse> Brands,
    List<FacetCountResponse> Tags);
