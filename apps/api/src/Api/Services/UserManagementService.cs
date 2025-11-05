using System.Security.Cryptography;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Service for managing user accounts (CRUD operations).
/// Used by admin interfaces for user management without direct database access.
/// </summary>
public class UserManagementService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly AuthService _authService;
    private readonly ILogger<UserManagementService> _logger;
    private readonly TimeProvider _timeProvider;

    public UserManagementService(
        MeepleAiDbContext dbContext,
        AuthService authService,
        ILogger<UserManagementService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _authService = authService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Get paginated list of users with optional filtering and sorting.
    /// </summary>
    public async Task<PagedResult<UserDto>> GetUsersAsync(
        string? searchTerm,
        string? roleFilter,
        string? sortBy,
        string? sortOrder,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Users
            .Include(u => u.Sessions)
            .AsNoTracking();

        // Search filter (email or display name)
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Contains(term) ||
                u.DisplayName != null && u.DisplayName.ToLower().Contains(term));
        }

        // Role filter
        if (!string.IsNullOrWhiteSpace(roleFilter) && roleFilter != "all")
        {
            if (Enum.TryParse<UserRole>(roleFilter, true, out var roleEnum))
            {
                query = query.Where(u => u.Role == roleEnum);
            }
        }

        // Sorting
        query = sortBy?.ToLower() switch
        {
            "email" => sortOrder == "asc" ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
            "displayname" => sortOrder == "asc" ? query.OrderBy(u => u.DisplayName) : query.OrderByDescending(u => u.DisplayName),
            "role" => sortOrder == "asc" ? query.OrderBy(u => u.Role) : query.OrderByDescending(u => u.Role),
            _ => sortOrder == "asc" ? query.OrderBy(u => u.CreatedAt) : query.OrderByDescending(u => u.CreatedAt)
        };

        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<UserDto>(
            Items: users.Select(MapToDto).ToList(),
            Total: total,
            Page: page,
            PageSize: limit
        );
    }

    /// <summary>
    /// Create a new user with specified role.
    /// Admin users can create users with any role, bypassing the normal registration restrictions.
    /// </summary>
    public async Task<UserDto> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate email uniqueness
        if (await _dbContext.Users.AnyAsync(u => u.Email == request.Email, cancellationToken))
        {
            throw new InvalidOperationException($"User with email {request.Email} already exists");
        }

        // Parse role from request
        var role = UserRole.User;
        if (!string.IsNullOrWhiteSpace(request.Role) && Enum.TryParse<UserRole>(request.Role, true, out var parsedRole))
        {
            role = parsedRole;
        }

        // Create user directly (bypassing AuthService role restrictions for admin-created users)
        var userId = Guid.NewGuid().ToString("N");
        var user = new UserEntity
        {
            Id = userId,
            Email = request.Email,
            DisplayName = request.DisplayName,
            PasswordHash = HashPassword(request.Password),
            Role = role,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin created user {UserId} with role {Role}", userId, role);

        // Reload with sessions for DTO mapping
        var createdUser = await _dbContext.Users
            .Include(u => u.Sessions)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (createdUser == null)
        {
            throw new InvalidOperationException($"Failed to retrieve created user {userId}");
        }

        return MapToDto(createdUser);
    }

    /// <summary>
    /// Update existing user details (email, display name, role).
    /// </summary>
    public async Task<UserDto> UpdateUserAsync(
        string userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.Sessions)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            throw new KeyNotFoundException($"User {userId} not found");
        }

        // Update email if provided and different
        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            // Check uniqueness
            if (await _dbContext.Users.AnyAsync(u => u.Email == request.Email && u.Id != userId, cancellationToken))
            {
                throw new InvalidOperationException($"Email {request.Email} is already in use");
            }
            user.Email = request.Email;
        }

        // Update display name
        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            user.DisplayName = request.DisplayName;
        }

        // Update role
        if (!string.IsNullOrWhiteSpace(request.Role) && Enum.TryParse<UserRole>(request.Role, true, out var roleEnum))
        {
            user.Role = roleEnum;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Admin updated user {UserId}", userId);

        return MapToDto(user);
    }

    /// <summary>
    /// Delete a user account.
    /// Prevents self-deletion and deletion of the last admin.
    /// </summary>
    public async Task DeleteUserAsync(
        string userId,
        string requestingUserId,
        CancellationToken cancellationToken = default)
    {
        // Prevent self-deletion
        if (userId == requestingUserId)
        {
            throw new InvalidOperationException("Cannot delete your own account");
        }

        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
        {
            throw new KeyNotFoundException($"User {userId} not found");
        }

        // Prevent deletion of last admin
        if (user.Role == UserRole.Admin)
        {
            var adminCount = await _dbContext.Users.CountAsync(u => u.Role == UserRole.Admin, cancellationToken);
            if (adminCount <= 1)
            {
                throw new InvalidOperationException("Cannot delete the last admin user");
            }
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Admin deleted user {UserId}", userId);
    }

    private static UserDto MapToDto(UserEntity user)
    {
        // Get the most recent session's LastSeenAt as the "last login" time
        var lastSeenAt = user.Sessions
            .Where(s => s.RevokedAt == null)
            .OrderByDescending(s => s.LastSeenAt ?? s.CreatedAt)
            .FirstOrDefault()
            ?.LastSeenAt;

        return new UserDto(
            Id: user.Id,
            Email: user.Email,
            DisplayName: user.DisplayName ?? string.Empty,
            Role: user.Role.ToString(),
            CreatedAt: user.CreatedAt,
            LastSeenAt: lastSeenAt
        );
    }

    /// <summary>
    /// Hash password using PBKDF2 with 210,000 iterations (same as AuthService).
    /// Format: v1.iterations.base64_salt.base64_hash
    /// </summary>
    private static string HashPassword(string password)
    {
        const int iterations = 210_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}
