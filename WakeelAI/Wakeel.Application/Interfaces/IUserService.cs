using System;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.Users;
using Wakeel.Application.DTOs.Employees;

namespace Wakeel.Application.Interfaces;

public interface IUserService
{
    Task<InviteUserResponse> InviteUserAsync(Guid ownerUserId, Guid companyId, InviteUserRequest request, CancellationToken cancellationToken = default);
    Task<UserListResponse> ListUsersAsync(Guid companyId, string? role, int page, int limit, CancellationToken cancellationToken = default);
    Task<UserListItem?> UpdateUserStatusAsync(Guid companyId, Guid userId, bool isActive, CancellationToken cancellationToken = default);
}
