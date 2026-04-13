namespace ECOM.ProductApi.Dtos;

public record CategoryFacetResponse(int CategoryId, string CategoryName, int? ParentCategoryId, int Count);
