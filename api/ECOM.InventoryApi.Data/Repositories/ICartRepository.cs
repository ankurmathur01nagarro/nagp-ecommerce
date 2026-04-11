using ECOM.InventoryApi.Data.DataModels;

namespace ECOM.InventoryApi.Data.Repositories;

public interface ICartRepository
{
    Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Cart> GetOrCreateByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> SetItemsAsync(Guid userId, string itemsJson, CancellationToken ct = default);
    Task<bool> ClearAsync(Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid userId, CancellationToken ct = default);
}
