using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECOM.InventoryApi.Data.DataModels;

public class Cart
{
    [Key]
    public int Id { get; set; }

    /// <summary>Owning user — one cart per user (enforced by unique index).</summary>
    [Required]
    public int UserId { get; set; }

    /// <summary>Cart line items stored as a JSONB array — { productId, sku, name, unitPrice, quantity, imageUrl, addedAt, appliedOfferId }</summary>
    [Column(TypeName = "jsonb")]
    public string? Items { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
