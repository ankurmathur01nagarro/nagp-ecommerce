using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECOM.InventoryApi.Data.DataModels;

public class Inventory
{
    [Key]
    public int Id { get; set; }

    /// <summary>Foreign reference to Products.Id in the Product service (not enforced at DB level).</summary>
    [Required]
    public int ProductId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Sku { get; set; } = string.Empty;

    /// <summary>Total on-hand quantity across all warehouses.</summary>
    public int Quantity { get; set; }

    /// <summary>Quantity reserved by open carts / pending orders.</summary>
    public int Reserved { get; set; }

    /// <summary>Low-stock alert threshold.</summary>
    public int LowStockThreshold { get; set; }

    /// <summary>Warehouse breakdown + extra attributes — { warehouses: [{code, qty, location}], lastRestockAt, supplier, notes }</summary>
    [Column(TypeName = "jsonb")]
    public string? Metadata { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
