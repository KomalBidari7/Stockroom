using System.ComponentModel.DataAnnotations;

namespace Stockroom.Infrastructure.Authentication;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "StockroomAPI";

    [Required]
    public string Audience { get; set; } = "StockroomClients";

    [Required]
    [MinLength(32, ErrorMessage = "JWT SecretKey must be at least 32 characters (256 bits) long.")]
    public string SecretKey { get; set; } = string.Empty;

    [Range(1, 1440, ErrorMessage = "Token expiration must be between 1 and 1440 minutes.")]
    public int ExpirationMinutes { get; set; } = 60;
}
