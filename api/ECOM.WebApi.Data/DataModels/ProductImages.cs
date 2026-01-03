using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECOM.WebApi.Data.DataModels
{
    public class ProductImages
    {
        [Key]
        public int Id { get; set; }
        public int ProductId { get; set; }
        public byte[]? ImageData { get; set; }
        public string? ImageUrl { get; set; }
        public byte[]? ThumbnailData { get; set; }
        public string? ThumbnailUrl { get; set; }
        public int Index { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        [ForeignKey("ProductId")]
        public Products Product { get; set; } = default!;
    }
}