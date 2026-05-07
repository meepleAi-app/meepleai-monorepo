using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Guards;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ConflictException = Api.Middleware.Exceptions.ConflictException;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles user registration with email and password.
/// Creates user and immediately authenticates with session token.
/// </summary>
internal class RegisterCommandHandler : ICommandHandler<RegisterCommand, RegisterResponse>
{
    // C5: SystemConfiguration row that records whether the bootstrap-admin
    // path has already been consumed. Single-use enforcement is layered on
    // top of the constant-time token compare.
    private const string BootstrapAdminConfigKey = "Authentication:BootstrapAdmin";

    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly IConfiguration _configuration;
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        IEmailVerificationService emailVerificationService,
        IConfiguration configuration,
        MeepleAiDbContext db,
        TimeProvider timeProvider,
        ILogger<RegisterCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _emailVerificationService = emailVerificationService ?? throw new ArgumentNullException(nameof(emailVerificationService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RegisterResponse> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate input before domain operations
        Guard.AgainstNullOrWhiteSpace(command.DisplayName, nameof(command.DisplayName));
        Guard.AgainstNullOrWhiteSpace(command.Password, nameof(command.Password));
        Guard.AgainstTooShort(command.Password, nameof(command.Password), 8);

        // Validate and create email
        var email = new Email(command.Email);

        // Allocate the user ID up-front so we can use it as
        // SystemConfiguration.CreatedByUserId when the bootstrap path fires —
        // the bootstrap-admin row is "created by" the very user being
        // provisioned in the same UoW transaction.
        var userId = Guid.NewGuid();

        // C5: role is now determined exclusively by the bootstrap-admin token.
        // The legacy HasAnyUsersAsync first-user-is-admin path is removed
        // because two concurrent first registrations both observed an empty
        // users table and both became Admin.
        var role = await DetermineRoleFromBootstrapTokenAsync(command.BootstrapToken, userId, cancellationToken)
            .ConfigureAwait(false);

        // Reject self-assignment of elevated roles for the explicit-role
        // pathway (admins assign roles via Administration BC, not /auth/register).
        if (!string.IsNullOrWhiteSpace(command.Role))
        {
            var requested = Role.Parse(command.Role);
            if (requested.IsAdmin() || requested.IsEditor())
            {
                throw new DomainException("Only administrators can assign elevated roles");
            }
            // Honour the explicit non-elevated role only if the bootstrap
            // path didn't already grant Admin.
            if (role != Role.Admin)
            {
                role = requested;
            }
        }

        // Create password hash
        var passwordHash = PasswordHash.Create(command.Password);

        // Create user (userId allocated above so it can be reused by the
        // bootstrap-admin SystemConfiguration row).
        var user = new User(
            id: userId,
            email: email,
            displayName: command.DisplayName.Trim(),
            passwordHash: passwordHash,
            role: role
        );

        // Set 7-day grace period for email verification
        // Issue #3672: Allow new users to use the app while verifying email
        user.SetVerificationGracePeriod(DateTime.UtcNow.AddDays(7));

        // Create session for immediate authentication
        var sessionId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var session = new Session(
            id: sessionId,
            userId: userId,
            token: sessionToken,
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent
        );

        // Save user and session. The pre-flight email-existence check that
        // used to live here is removed by C5: it created a TOCTOU window
        // between SELECT and INSERT under concurrent traffic. We rely on the
        // DB unique index instead and translate the resulting unique-violation
        // into a 409 ConflictException.
        await _userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _logger.LogInformation(
                ex,
                "Register lost the email-uniqueness race for {EmailMasked} — surfacing 409.",
                MaskEmail(email.Value));
            throw new ConflictException("Email is already registered");
        }

        // ISSUE-3071: Send verification email after successful registration
        // This is fire-and-forget - email failures should not fail registration
        try
        {
            await _emailVerificationService.SendVerificationEmailAsync(
                userId,
                email.Value,
                command.DisplayName.Trim(),
                cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // Log but don't fail registration - user can request resend
            _logger.LogWarning(ex, "Failed to send verification email for user {UserId}", userId);
        }
#pragma warning restore CA1031

        // Map to DTO
        var userDto = MapToUserDto(user);

        return new RegisterResponse(
            User: userDto,
            SessionToken: sessionToken.Value,
            ExpiresAt: session.ExpiresAt
        );
    }

    /// <summary>
    /// C5: returns <see cref="Role.Admin"/> only if the supplied token equals
    /// the configured <c>Authentication:BootstrapAdminToken</c> (constant-time
    /// compare) AND the bootstrap path has not already been consumed.
    /// All other inputs return <see cref="Role.User"/> — wrong/missing token,
    /// missing config, or post-bootstrap re-use. Single-use enforcement
    /// uses a dedicated row in <c>system_configurations</c>; the flag is
    /// flipped in the same UoW transaction as the user create.
    /// </summary>
    private async Task<Role> DetermineRoleFromBootstrapTokenAsync(
        string? providedToken, Guid bootstrappingUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providedToken))
            return Role.User;

        var configuredToken = _configuration["Authentication:BootstrapAdminToken"];
        if (string.IsNullOrWhiteSpace(configuredToken))
            return Role.User;

        // Constant-time compare: equal-length is required by FixedTimeEquals,
        // so length-mismatch is detected explicitly without short-circuiting
        // on the data itself.
        var configBytes = Encoding.UTF8.GetBytes(configuredToken);
        var inputBytes = Encoding.UTF8.GetBytes(providedToken);
        if (configBytes.Length != inputBytes.Length)
            return Role.User;
        if (!CryptographicOperations.FixedTimeEquals(configBytes, inputBytes))
            return Role.User;

        // Single-use enforcement. Find/create the dedicated singleton row.
        var sysConfig = await _db.Set<SystemConfigurationEntity>()
            .FirstOrDefaultAsync(c => c.Key == BootstrapAdminConfigKey, cancellationToken)
            .ConfigureAwait(false);

        if (sysConfig != null && sysConfig.BootstrapAdminCreated)
        {
            _logger.LogWarning(
                "Bootstrap admin token presented after the first admin was already provisioned — " +
                "rejecting privilege escalation; rotate AUTHENTICATION__BOOTSTRAPADMINTOKEN.");
            return Role.User;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (sysConfig == null)
        {
            sysConfig = new SystemConfigurationEntity
            {
                Id = Guid.NewGuid(),
                Key = BootstrapAdminConfigKey,
                Value = "true",
                ValueType = "bool",
                Category = "Authentication",
                Description = "Bootstrap-admin single-use guard (C5).",
                IsActive = true,
                Environment = "All",
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
                // FK Restrict on system_configurations.CreatedByUserId — point
                // it at the bootstrapping user being persisted in this same UoW.
                CreatedByUserId = bootstrappingUserId,
            };
            _db.Set<SystemConfigurationEntity>().Add(sysConfig);
        }
        sysConfig.BootstrapAdminCreated = true;
        sysConfig.BootstrapAdminCreatedAt = now;
        sysConfig.UpdatedAt = now;
        sysConfig.UpdatedByUserId = bootstrappingUserId;

        return Role.Admin;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // Postgres SQLSTATE 23505 = unique_violation. The constraint name
        // varies by EF migration history; we accept any unique-violation on
        // the user-bearing transaction since the only unique index Register
        // can hit is users(email) (sessions use a fresh GUID token).
        if (ex.InnerException is Npgsql.PostgresException pgEx)
        {
            return string.Equals(pgEx.SqlState, "23505", StringComparison.Ordinal);
        }
        return false;
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0) return "***";
        var local = email[..at];
        var domain = email[at..];
        return local.Length <= 2
            ? "***" + domain
            : string.Concat(local.AsSpan(0, 2), "***", domain);
    }

    private static Api.BoundedContexts.Authentication.Application.DTOs.UserDto MapToUserDto(User user)
    {
        return new Api.BoundedContexts.Authentication.Application.DTOs.UserDto(
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
            VerificationGracePeriodEndsAt: user.VerificationGracePeriodEndsAt
        );
    }
}
