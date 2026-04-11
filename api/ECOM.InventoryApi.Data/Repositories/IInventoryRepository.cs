using ECOM.InventoryApi.Data.DataModels;

namespace ECOM.InventoryApi.Data.Repositories;

public interface IInventoryRepository
{
    Task<Inventory?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Inventory?> GetByProductIdAsync(int productId, CancellationToken ct = default);
    Task<(List<Inventory> Items, int TotalCount)> GetListAsync(int page, int pageSize, bool? lowStockOnly, string? warehouseCode, CancellationToken ct = default);
    Task<int> CreateAsync(Inventory inventory, CancellationToken ct = default);
    Task<bool> UpdateAsync(Inventory inventory, CancellationToken ct = default);
    Task<bool> AdjustQuantityAsync(int productId, int delta, CancellationToken ct = default);
    Task<bool> ReserveAsync(int productId, int quantity, CancellationToken ct = default);
    Task<bool> ReleaseReservationAsync(int productId, int quantity, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
