using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECOM.WebApi.Data.DataModels
{
    public class ProductCategory
    {
        [Key]
        public int Id { get; set; }
        public int? ParentId { get; set; }
        [Required]
        public string? Name { get; set; }

        [ForeignKey("ParentId")]
        public ProductCategory? Parent { get; set; }
        public ICollection<ProductCategory> Children { get; set; } = new List<ProductCategory>();
        public ICollection<Products> Products { get; set; } = new List<Products>();
    }
}