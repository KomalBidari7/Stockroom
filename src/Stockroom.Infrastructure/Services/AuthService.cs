using Microsoft.AspNetCore.Identity;
using Stockroom.Application.DTOs.Auth;
using Stockroom.Application.Interfaces;
using Stockroom.Domain.Common;
using Stockroom.Domain.Exceptions;
using Stockroom.Infrastructure.Authentication;
using Stockroom.Infrastructure.Identity;

namespace Stockroom.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator tokenGenerator)
    {
        _userManager = userManager;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser != null)
        {
            throw new ProductConflictException($"A user with email '{normalizedEmail}' is already registered.");
        }

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new IdentityValidationException(result.Errors.Select(e => e.Description));
        }

        // Newly registered users are assigned Viewer role by default.
        // Public registration requests NEVER accept client-supplied roles.
        await _userManager.AddToRoleAsync(user, Roles.Viewer);

        var roles = new[] { Roles.Viewer };
        var (token, expiresAt) = _tokenGenerator.GenerateToken(user, roles);

        return new AuthResponse
        {
            Token = token,
            TokenType = "Bearer",
            ExpiresAtUtc = expiresAt,
            UserId = user.Id,
            Email = user.Email!,
            Roles = roles
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            // Consistent failure message prevents user enumeration attacks
            throw new AuthenticationFailedException("Invalid email or password.");
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            throw new AuthenticationFailedException("Invalid email or password.");
        }

        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var (token, expiresAt) = _tokenGenerator.GenerateToken(user, roles);

        return new AuthResponse
        {
            Token = token,
            TokenType = "Bearer",
            ExpiresAtUtc = expiresAt,
            UserId = user.Id,
            Email = user.Email!,
            Roles = roles
        };
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        var roles = (await _userManager.GetRolesAsync(user)).ToList();

        return new CurrentUserResponse
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            Roles = roles
        };
    }
}
