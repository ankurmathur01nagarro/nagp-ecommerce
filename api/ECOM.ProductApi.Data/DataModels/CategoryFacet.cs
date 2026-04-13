namespace ECOM.ProductApi.Data.DataModels;

public record CategoryFacet(int CategoryId, string CategoryName, int? ParentCategoryId, int Count);
