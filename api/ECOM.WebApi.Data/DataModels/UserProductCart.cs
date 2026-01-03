using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECOM.WebApi.Data.DataModels
{
    public class UserProductCart
    {
        [Key]
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int UserId { get; set; }
        public int Quantity { get; set; }

        [ForeignKey("ProductId")]
        public Products Product { get; set; } = default!;
        [ForeignKey("UserId")]
        public Users User { get; set; } = default!;
    }
}