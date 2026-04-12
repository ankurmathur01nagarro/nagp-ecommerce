namespace ECOM.ProductApi.Data.Repositories;

public interface ICategoryRepository
{
    Task<List<FlatCategory>> GetAllAsync(CancellationToken ct = default);
}
