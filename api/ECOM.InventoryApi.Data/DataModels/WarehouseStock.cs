namespace ECOM.InventoryApi.Data.DataModels;

public class WarehouseStock
{
    public string Code { get; set; } = string.Empty;
    public string? Location { get; set; }
    public int Quantity { get; set; }
}
