using ECOM.InventoryApi.Data.DataModels;

namespace ECOM.InventoryApi.Data.Repositories;

public interface ICartRepository
{
    Task<Cart?> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<Cart> GetOrCreateByUserIdAsync(int userId, CancellationToken ct = default);
    Task<bool> SetItemsAsync(int userId, string itemsJson, CancellationToken ct = default);
    Task<bool> ClearAsync(int userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(int userId, CancellationToken ct = default);
}
