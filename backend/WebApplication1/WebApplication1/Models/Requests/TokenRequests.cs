using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models.Requests
{
    public class RefreshTokenRequest
    {
        [Required]
        public required string RefreshToken { get; set; }
    }

    public class RevokeTokenRequest
    {
        [Required]
        public required string RefreshToken { get; set; }
    }
} 