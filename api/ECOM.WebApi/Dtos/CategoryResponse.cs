namespace ECOM.WebApi.Dtos;

public record CategoryResponse(int Id, string Name, List<CategoryResponse> Subcategories);
