using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles user login with email and password.
/// Returns session token or temp token if 2FA is required.
/// </summary>
internal class LoginCommandHandler : ICommandHandler<LoginCommand, LoginResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly ITempSessionService _tempSessionService;
    private readonly IUnitOfWork _unitOfWork;

    public LoginCommandHandler(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        ITempSessionService tempSessionService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _tempSessionService = tempSessionService ?? throw new ArgumentNullException(nameof(tempSessionService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<LoginResponse> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Issue #2564: Validate null/empty password BEFORE repository call (efficiency)
        if (string.IsNullOrWhiteSpace(command.Password))
            throw new ValidationException("Password is required");

        // Find user by email (even for short passwords - prevents timing attacks)
        var email = new Email(command.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        // Issue #2564: Validate password length AFTER repository call (security: prevent timing attack)
        if (command.Password.Length < 8)
            throw new ValidationException("Password must be at least 8 characters");

        if (user == null)
            throw new DomainException("Invalid email or password");

        // Issue #3339: Check if account is locked out
        if (user.IsLockedOut())
        {
            var remainingTime = user.GetRemainingLockoutDuration();
            var remainingMinutes = (int)Math.Ceiling(remainingTime.TotalMinutes);
            throw new DomainException($"Account is locked. Please try again in {remainingMinutes} minute(s).");
        }

        // Verify password
        if (!user.VerifyPassword(command.Password))
        {
            // Issue #3339: Record failed login attempt
            var wasLocked = user.RecordFailedLogin(command.IpAddress);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (wasLocked)
                throw new DomainException("Account has been locked due to too many failed login attempts. Please try again in 15 minutes.");

            throw new DomainException("Invalid email or password");
        }

        // Issue #3339: Reset failed attempts on successful login
        user.RecordSuccessfulLogin();

        // Issue #2886: Check if user is suspended
        if (user.IsSuspended)
            throw new DomainException("Account is suspended");

        // Check if 2FA is required
        if (user.RequiresTwoFactor())
        {
            // Create temp session for 2FA verification (5-min TTL, single-use)
            var tempSessionToken = await _tempSessionService.CreateTempSessionAsync(
                user.Id,
                command.IpAddress
            ).ConfigureAwait(false);

            return new LoginResponse(
                RequiresTwoFactor: true,
                TempSessionToken: tempSessionToken,
                User: null,
                SessionToken: null
            );
        }

        // Create session
        var sessionId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var session = new Session(
            id: sessionId,
            userId: user.Id,
            token: sessionToken,
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent
        );

        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Issue #3677: Enforce device limit (max 5 unique devices)
        await EnforceDeviceLimitAsync(user.Id, session.DeviceFingerprint, cancellationToken).ConfigureAwait(false);

        // Map to DTO
        var userDto = MapToUserDto(user);

        return new LoginResponse(
            RequiresTwoFactor: false,
            TempSessionToken: null,
            User: userDto,
            SessionToken: sessionToken.Value
        );
    }

    /// <summary>
    /// Enforces device limit (max 5 unique devices per user).
    /// Issue #3677: Auto-revokes oldest device if limit exceeded.
    /// </summary>
    private async Task EnforceDeviceLimitAsync(Guid userId, string? currentDeviceFingerprint, CancellationToken cancellationToken)
    {
        const int MaxDevices = 5;

        // Skip if no fingerprint (shouldn't happen, but defensive)
        if (string.IsNullOrWhiteSpace(currentDeviceFingerprint))
            return;

        // Get all active sessions for user
        var activeSessions = await _sessionRepository.GetActiveSessionsByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        // Group by device fingerprint and count unique devices
        var deviceGroups = activeSessions
            .Where(s => !string.IsNullOrWhiteSpace(s.DeviceFingerprint))
            .GroupBy(s => s.DeviceFingerprint, StringComparer.Ordinal)
            .Select(g => new
            {
                Fingerprint = g.Key!,
                Sessions = g.ToList(),
                OldestSession = g.OrderBy(s => s.CreatedAt).First()
            })
            .ToList();

        // Check if device limit exceeded (allow exactly MaxDevices, revoke at MaxDevices+1)
        if (deviceGroups.Count <= MaxDevices)
            return; // At or under limit, no action needed

        // Find oldest device (by first session creation time)
        var oldestDevice = deviceGroups
            .OrderBy(d => d.OldestSession.CreatedAt)
            .ThenBy(d => d.Fingerprint, StringComparer.Ordinal) // Tie-breaker for deterministic ordering
            .First();

        // Revoke all sessions for the oldest device
        foreach (var session in oldestDevice.Sessions)
        {
            session.Revoke(reason: "DeviceLimitExceeded");
            await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto(
            Id: user.Id,
            Email: user.Email.Value,
            DisplayName: user.DisplayName,
            Role: user.Role.Value,
            Tier: user.Tier.Value,
            CreatedAt: user.CreatedAt,
            IsTwoFactorEnabled: user.IsTwoFactorEnabled,
            TwoFactorEnabledAt: user.TwoFactorEnabledAt,
            Level: user.Level,
            ExperiencePoints: user.ExperiencePoints,
            OnboardingCompleted: user.OnboardingCompleted,              // Issue #323
            OnboardingSkipped: user.OnboardingSkipped                   // Issue #323
        );
    }
}
