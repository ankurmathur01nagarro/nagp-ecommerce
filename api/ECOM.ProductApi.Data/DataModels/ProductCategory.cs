using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECOM.ProductApi.Data.DataModels;

public class ProductCategory
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public int? ParentCategoryId { get; set; }

    [ForeignKey("ParentCategoryId")]
    public ProductCategory? ParentCategory { get; set; }

    public ICollection<ProductCategory> SubCategories { get; set; } = new List<ProductCategory>();

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
