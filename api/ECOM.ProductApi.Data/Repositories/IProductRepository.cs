using ECOM.ProductApi.Data.DataModels;

namespace ECOM.ProductApi.Data.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<(List<Product> Items, int TotalCount)> GetListAsync(int page, int pageSize, string? category, string? brand, string? tag, string? gender, CancellationToken ct = default);
    Task<(List<Product> Items, int TotalCount, ProductSearchFacets Facets)> SearchAsync(ProductFilter filter, CancellationToken ct = default);
    Task<int> CreateAsync(Product product, CancellationToken ct = default);
    Task<bool> UpdateAsync(Product product, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
