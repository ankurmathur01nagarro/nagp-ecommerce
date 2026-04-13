namespace ECOM.ProductApi.Data.DataModels;

public record ProductSearchFacets(
    List<CategoryFacet> Categories,
    List<ColorFacet> Colors,
    List<FacetCount> Sizes,
    List<FacetCount> Brands,
    List<FacetCount> Tags);
