using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stockroom.Application.DTOs.Auth;
using Stockroom.Application.Interfaces;
using Stockroom.Domain.Common;
using Stockroom.Domain.Exceptions;
using Stockroom.Infrastructure.Identity;

namespace Stockroom.Infrastructure.Services;

public class UserAdminService : IUserAdminService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserAdminService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
        var userDtos = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userDtos.Add(new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                Roles = roles.ToList()
            });
        }

        return userDtos;
    }

    public async Task<UserDto?> GetUserByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Roles = roles.ToList()
        };
    }

    public async Task<UserDto> UpdateUserRolesAsync(
        string id,
        UpdateUserRolesRequest request,
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            throw new EntityNotFoundException("User", id);
        }

        // Validate that all requested roles are valid domain roles
        foreach (var requestedRole in request.Roles)
        {
            if (!Roles.IsValidRole(requestedRole))
            {
                throw new RoleOperationException(
                    $"Role '{requestedRole}' is invalid. Permitted roles are: {string.Join(", ", Roles.All)}.");
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);

        // Safety invariant: Prevent removing the Admin role from the last active administrator
        if (currentRoles.Contains(Roles.Admin) && !request.Roles.Contains(Roles.Admin, StringComparer.OrdinalIgnoreCase))
        {
            var admins = await _userManager.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count <= 1 && admins.Any(a => a.Id == user.Id))
            {
                throw new RoleOperationException("Cannot remove the Admin role from the last active administrator in the system.");
            }
        }

        var rolesToRemove = currentRoles.Except(request.Roles, StringComparer.OrdinalIgnoreCase).ToList();
        var rolesToAdd = request.Roles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();

        if (rolesToRemove.Any())
        {
            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }

        if (rolesToAdd.Any())
        {
            await _userManager.AddToRolesAsync(user, rolesToAdd);
        }

        var updatedRoles = await _userManager.GetRolesAsync(user);

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Roles = updatedRoles.ToList()
        };
    }
}
