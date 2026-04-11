namespace ECOM.InventoryApi.Data.DataModels;

public class CartItem
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string? ImageUrl { get; set; }
    public int? AppliedOfferId { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}
