using ECOM.InventoryApi.Data.DataModels;

namespace ECOM.InventoryApi.Data.Repositories;

public interface IOfferRepository
{
    Task<Offer?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<(List<Offer> Items, int TotalCount)> GetListAsync(int page, int pageSize, int? productId, bool? activeOnly, string? couponCode, CancellationToken ct = default);
    Task<List<Offer>> GetActiveForProductAsync(int productId, CancellationToken ct = default);
    Task<List<Offer>> GetActiveForProductsAsync(int[] productIds, CancellationToken ct = default);
    Task<int> CreateAsync(Offer offer, CancellationToken ct = default);
    Task<bool> UpdateAsync(Offer offer, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
