using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ECOM.WebApi.Data.DataModels
{
    public class Users
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string? Username { get; set; }
        [Required]
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }
        [Required]
        public string? Role { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public ICollection<UserProductCart> UserProductCarts { get; set; } = new List<UserProductCart>();
    }
}