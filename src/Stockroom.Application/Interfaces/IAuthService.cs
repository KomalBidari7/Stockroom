using Stockroom.Application.DTOs.Auth;

namespace Stockroom.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<CurrentUserResponse?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default);
}

public interface IUserAdminService
{
    Task<IReadOnlyList<UserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<UserDto?> GetUserByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateUserRolesAsync(string id, UpdateUserRolesRequest request, string currentUserId, CancellationToken cancellationToken = default);
}
