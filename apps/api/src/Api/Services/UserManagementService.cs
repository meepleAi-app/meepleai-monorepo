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
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly ILogger<UserManagementService> _logger;
    private readonly TimeProvider _timeProvider;

    public UserManagementService(
        MeepleAiDbContext dbContext,
        AuthService authService,
        IPasswordHashingService passwordHashingService,
        ILogger<UserManagementService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _authService = authService;
        _passwordHashingService = passwordHashingService;
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
        // CWE-476: Add null check for Email property
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(term)) ||
                (u.DisplayName != null && u.DisplayName.ToLower().Contains(term)));
        }

        // Role filter
        if (!string.IsNullOrWhiteSpace(roleFilter) && roleFilter != "all")
        {
            // Normalize role filter to lowercase for comparison
            var normalizedRole = roleFilter.ToLower();
            query = query.Where(u => u.Role == normalizedRole);
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

        // Parse role from request (default to "user")
        var role = !string.IsNullOrWhiteSpace(request.Role) ? request.Role.ToLower() : "user";

        // Create user directly (bypassing AuthService role restrictions for admin-created users)
        var userId = Guid.NewGuid();
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
        var userGuid = Guid.Parse(userId);
        var user = await _dbContext.Users
            .Include(u => u.Sessions)
            .FirstOrDefaultAsync(u => u.Id == userGuid, cancellationToken);
        if (user == null)
        {
            throw new KeyNotFoundException($"User {userId} not found");
        }

        // Update email if provided and different
        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            // Check uniqueness
            if (await _dbContext.Users.AnyAsync(u => u.Email == request.Email && u.Id != userGuid, cancellationToken))
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
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            user.Role = request.Role.ToLower();
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
        if (user.Role == "admin")
        {
            var adminCount = await _dbContext.Users.CountAsync(u => u.Role == "admin", cancellationToken);
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
            Id: user.Id.ToString(),
            Email: user.Email,
            DisplayName: user.DisplayName ?? string.Empty,
            Role: user.Role,
            CreatedAt: user.CreatedAt,
            LastSeenAt: lastSeenAt
        );
    }

    /// <summary>
    /// Hash password using centralized IPasswordHashingService.
    /// </summary>
    private string HashPassword(string password)
    {
        return _passwordHashingService.HashSecret(password);
    }
}
