using System.ComponentModel.DataAnnotations;

namespace ECOM.Identity.Api.DataAccess;

public class Users
{
    [Key]
    public int Id { get; set; }
    [Required]
    public string Username { get; set; } = default!;
    [Required]
    public string Email { get; set; } = default!;
    public string? MobileNumber { get; set; }
    [Required]
    public string Role { get; set; } = default!;
    [Required]
    public string PasswordHash { get; set; } = default!;
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
