namespace ECOM.ProductApi.Dtos;

public record CategoryResponse(int Id, string Name, int? ParentCategoryId, List<CategoryResponse> Subcategories);
