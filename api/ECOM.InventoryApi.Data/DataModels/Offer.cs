using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECOM.InventoryApi.Data.DataModels;

public class Offer
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Product this offer applies to. Nullable for catalog-wide or category-wide offers (see Rules).</summary>
    public int? ProductId { get; set; }

    /// <summary>Percentage (0-100) or FixedAmount</summary>
    [Required]
    [MaxLength(50)]
    public string DiscountType { get; set; } = "Percentage";

    public decimal DiscountValue { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Offer rules — { minQuantity, maxQuantity, applicableCategoryIds, applicableBrandIds, couponCodes, tags }</summary>
    [Column(TypeName = "jsonb")]
    public string? Rules { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
