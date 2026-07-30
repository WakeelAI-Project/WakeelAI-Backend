using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wakeel.Application.DTOs.Users;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Domain.Entities;
using Wakeel.Domain.Enums;

namespace Wakeel.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<UserService> _logger;
    private readonly IEmailSender _emailSender;

    public UserService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher, ILogger<UserService> logger, IEmailSender emailSender)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    }

    public async Task<InviteUserResponse> InviteUserAsync(Guid ownerUserId, Guid companyId, InviteUserRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        // Ensure email uniqueness
        var exists = await _unitOfWork.Users.EmailExistsAsync(request.Email, cancellationToken);
        if (exists)
            throw new InvalidOperationException("email_already_exists");

        // Create a user with a random temporary password / invited status
        var tempPassword = Guid.NewGuid().ToString("N");
        var hashed = _passwordHasher.HashPassword(tempPassword);

        var role = request.Role switch
        {
            "HR_Manager" => UserRole.HR_Manager,
            _ => UserRole.Employee
        };

        // Only set CreatedByUserId if the creator actually exists in the database to avoid FK conflicts
        var creator = await _unitOfWork.Users.GetByIdAsync(ownerUserId, cancellationToken);

        var user = new User
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Email = request.Email,
            PasswordHash = hashed,
            FullName = request.FullName,
            Phone = string.Empty,
            Role = role,
            IsActive = true,
            IsEmailConfirmed = false,
            ActivationToken = string.Empty,
            ActivationTokenExpiry = DateTime.UtcNow,
            CreatedByUserId = creator is null ? null : ownerUserId,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // send email with credentials and temp password
        var subject = "You're invited to Wakeel";
        var body = $"<p>Hello {user.FullName},</p><p>You have been invited to Wakeel. Your login is <strong>{user.Email}</strong> and temporary password is <strong>{tempPassword}</strong>. Please change your password after first login.</p>";
        try
        {
            await _emailSender.SendEmailAsync(user.Email, subject, body, cancellationToken);
        }
        catch
        {
            _logger.LogWarning("Failed to send invitation email to {Email}", user.Email);
        }

        return new InviteUserResponse
        {
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role.ToString(),
            Status = "invited"
        };
    }

    public async Task<UserListResponse> ListUsersAsync(Guid companyId, string? role, int page, int limit, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        // The IUserRepository doesn't expose AsQueryable; use FindAsync to filter by company

        var all = await _unitOfWork.Users.FindAsync(u => u.CompanyId == companyId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(role))
        {
            if (Enum.TryParse<UserRole>(role, out var parsed))
                all = all.Where(u => u.Role == parsed).ToList();
            else
                all = new List<User>();
        }

        var total = all.Count;
        var items = all.Skip((page - 1) * limit).Take(limit)
            .Select(u => new UserListItem
            {
                UserId = u.Id,
                FullName = u.FullName,
                Role = u.Role.ToString(),
                IsActive = u.IsActive
            }).ToList();

        return new UserListResponse
        {
            Data = items,
            Page = page,
            Total = total
        };
    }

    public async Task<UserListItem?> UpdateUserStatusAsync(Guid companyId, Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.CompanyId != companyId)
            return null;

        user.IsActive = isActive;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserListItem
        {
            UserId = user.Id,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            IsActive = user.IsActive
        };
    }
}
