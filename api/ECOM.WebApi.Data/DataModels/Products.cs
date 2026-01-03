using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ECOM.WebApi.Data.DataModels
{
    public class Products
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string? Name { get; set; }
        public float Price { get; set; }
        public int CategoryId { get; set; }
        [Column(TypeName = "jsonb")]
        public string? Metadata { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        [ForeignKey("CategoryId")]
        public ProductCategory Category { get; set; } = default!;
        public ICollection<ProductImages> ProductImages { get; set; } = new List<ProductImages>();
        public ICollection<ProductInventory> ProductInventories { get; set; } = new List<ProductInventory>();
        public ICollection<UserProductCart> UserProductCarts { get; set; } = new List<UserProductCart>();
    }
}