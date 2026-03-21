using System.ComponentModel.DataAnnotations;

namespace ECOM.WebApi.Auth;

public record LoginRequest(
    [Required, MaxLength(100)] string Username,
    [Required, MaxLength(200)] string Password
);
