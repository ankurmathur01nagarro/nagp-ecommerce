using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECOM.ProductApi.Data.DataModels;

public class Product
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Sku { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ShortDescription { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int CategoryId { get; set; }

    public int BrandId { get; set; }

    /// <summary>Images gallery — array of { url, alt, sortOrder }</summary>
    [Column(TypeName = "jsonb")]
    public string? Images { get; set; }

    /// <summary>Product metadata — colors, sizes, tags, techSpecs, additionalInfo</summary>
    [Column(TypeName = "jsonb")]
    public string? Metadata { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [ForeignKey("CategoryId")]
    public ProductCategory Category { get; set; } = default!;

    [ForeignKey("BrandId")]
    public Brand Brand { get; set; } = default!;
}
