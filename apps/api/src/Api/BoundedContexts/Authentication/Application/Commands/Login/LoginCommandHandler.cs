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
    private readonly Api.BoundedContexts.SecurityAudit.Application.Services.IAuditLogger _auditLogger;

    public LoginCommandHandler(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        ITempSessionService tempSessionService,
        IUnitOfWork unitOfWork,
        Api.BoundedContexts.SecurityAudit.Application.Services.IAuditLogger auditLogger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _tempSessionService = tempSessionService ?? throw new ArgumentNullException(nameof(tempSessionService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<LoginResponse> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var email = new Email(command.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        if (user == null)
        {
            // I10: failed login on unknown email — no actor (we don't have a
            // user id), masked email in metadata so we don't leak the typed
            // value.
            await _auditLogger.LogAsync(
                Api.BoundedContexts.SecurityAudit.Application.Services.AuditEventType.LoginFailure,
                actorUserId: null,
                targetUserId: null,
                ipAddress: command.IpAddress,
                userAgent: command.UserAgent,
                metadata: $"{{\"emailMasked\":\"{MaskEmail(command.Email)}\",\"reason\":\"unknown_email\"}}",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            throw new DomainException("Invalid email or password");
        }

        // Issue #2886 + C2: gate the entire login flow on the account status BEFORE
        // touching the lockout counter or VerifyPassword. CanAuthenticate() returns
        // true only for Status == Active, covering Pending/Suspended/Banned (and
        // future Deleted) under a single neutral "Account is not available" message
        // so we don't (a) leak existence of pending invitations via the
        // "Invalid email or password" branch, (b) increment the failed-login counter
        // for accounts that must never authenticate, or (c) NRE on null PasswordHash.
        if (!user.CanAuthenticate())
            throw new DomainException("Account is not available");

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

            // I10: invalid-password login failure. Actor is null (the user
            // hasn't authenticated yet, so we don't credit the attempt to
            // them); target is the user whose row was probed.
            await _auditLogger.LogAsync(
                Api.BoundedContexts.SecurityAudit.Application.Services.AuditEventType.LoginFailure,
                actorUserId: null,
                targetUserId: user.Id,
                ipAddress: command.IpAddress,
                userAgent: command.UserAgent,
                metadata: $"{{\"emailMasked\":\"{MaskEmail(command.Email)}\",\"reason\":\"invalid_password\",\"failedAttempts\":{user.FailedLoginAttempts}}}",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (wasLocked)
            {
                // I10: account just crossed the failed-attempts threshold.
                await _auditLogger.LogAsync(
                    Api.BoundedContexts.SecurityAudit.Application.Services.AuditEventType.AccountLocked,
                    actorUserId: null,
                    targetUserId: user.Id,
                    ipAddress: command.IpAddress,
                    userAgent: command.UserAgent,
                    metadata: $"{{\"attemptCount\":{user.FailedLoginAttempts},\"lockoutUntilUtc\":\"{user.LockedUntil:O}\"}}",
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                throw new DomainException("Account has been locked due to too many failed login attempts. Please try again in 15 minutes.");
            }

            throw new DomainException("Invalid email or password");
        }

        // C6: do NOT reset the failed-login counter here. If 2FA is required
        // and the user fails the second factor, an attacker could re-login
        // with the right password to keep refilling the brute-force budget.
        // RecordSuccessfulLogin is now invoked in Verify2FACommandHandler
        // for the 2FA branch and inline below for the non-2FA branch.

        // Check if 2FA is required
        if (user.RequiresTwoFactor())
        {
            // Create temp session for 2FA verification (5-min TTL, single-use)
            var tempSessionToken = await _tempSessionService.CreateTempSessionAsync(
                user.Id,
                command.IpAddress
            ).ConfigureAwait(false);

            // F5 (auth security review): on the 2FA-required branch we expose
            // the temp-session expiration through ExpiresAt so the client
            // can time out the 2FA prompt. The temp-session lifetime is
            // managed by ITempSessionService (5 minutes by default); we
            // reflect that bound via the time provider.
            var tempSessionExpiresAt = DateTime.UtcNow.AddMinutes(5);
            return new LoginResponse(
                RequiresTwoFactor: true,
                TempSessionToken: tempSessionToken,
                User: null,
                SessionToken: null,
                ExpiresAt: tempSessionExpiresAt
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

        // C6: only reset the failed-login counter once we know the login
        // fully succeeded — for the non-2FA branch that is right now (the
        // session is about to be persisted). The 2FA branch resets the
        // counter from Verify2FACommandHandler.
        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        user.RecordSuccessfulLogin();
        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // I10: successful login (non-2FA path). The 2FA-required branch
        // logs success from Verify2FACommandHandler instead — this branch
        // only fires when no second factor was needed.
        await _auditLogger.LogAsync(
            Api.BoundedContexts.SecurityAudit.Application.Services.AuditEventType.LoginSuccess,
            actorUserId: user.Id,
            targetUserId: user.Id,
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Issue #3677: Enforce device limit (max 5 unique devices)
        await EnforceDeviceLimitAsync(user.Id, session.DeviceFingerprint, cancellationToken).ConfigureAwait(false);

        // Map to DTO
        var userDto = MapToUserDto(user);

        return new LoginResponse(
            RequiresTwoFactor: false,
            TempSessionToken: null,
            User: userDto,
            SessionToken: sessionToken.Value,
            // I2: surface the Session aggregate's canonical expiration so
            // the endpoint stops recomputing it from configuration (and
            // potentially returning a value that doesn't match the DB row).
            ExpiresAt: session.ExpiresAt
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

    /// <summary>
    /// I10: mask the email so audit logs don't carry the raw value (PII).
    /// "alice@example.com" → "al***@example.com".
    /// </summary>
    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "***";
        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0) return "***";
        var local = email[..at];
        var domain = email[at..];
        return local.Length <= 2
            ? "***" + domain
            : string.Concat(local.AsSpan(0, 2), "***", domain);
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
            EmailVerified: user.EmailVerified,
            EmailVerifiedAt: user.EmailVerifiedAt,
            VerificationGracePeriodEndsAt: user.VerificationGracePeriodEndsAt,
            OnboardingCompleted: user.OnboardingCompleted,
            OnboardingSkipped: user.OnboardingSkipped
        );
    }
}
