using Microsoft.AspNetCore.Identity;

namespace Stockroom.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
