using System.ComponentModel.DataAnnotations;

namespace ECOM.WebApi.Auth;

public record RegisterRequest(
    [Required, MaxLength(100)] string Username,
    [Required, EmailAddress, MaxLength(200)] string Email,
    [Required, MaxLength(200)] string Password,
    [MaxLength(20)] string? MobileNumber = null
);
