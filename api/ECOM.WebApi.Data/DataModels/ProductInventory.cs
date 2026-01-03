using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECOM.WebApi.Data.DataModels
{
    public class ProductInventory
    {
        [Key]
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int Count { get; set; }
        public int? OfferId { get; set; }

        [ForeignKey("ProductId")]
        public Products Product { get; set; } = default!;
    }
}