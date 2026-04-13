namespace ECOM.WebApi.Dtos;

public record CategoryResponse(int Id, string Name, int? ParentCategoryId, List<CategoryResponse> Subcategories);
