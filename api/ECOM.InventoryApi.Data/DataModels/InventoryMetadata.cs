namespace ECOM.InventoryApi.Data.DataModels;

public class InventoryMetadata
{
    public List<WarehouseStock> Warehouses { get; set; } = [];
    public DateTimeOffset? LastRestockAt { get; set; }
    public string? Supplier { get; set; }
    public string? Notes { get; set; }
}
