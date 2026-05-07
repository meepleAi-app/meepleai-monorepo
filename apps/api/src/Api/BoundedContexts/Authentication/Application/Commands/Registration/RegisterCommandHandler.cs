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
    private readonly Api.BoundedContexts.SecurityAudit.Application.Services.IAuditLogger _auditLogger;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        IEmailVerificationService emailVerificationService,
        IConfiguration configuration,
        MeepleAiDbContext db,
        TimeProvider timeProvider,
        ILogger<RegisterCommandHandler> logger,
        Api.BoundedContexts.SecurityAudit.Application.Services.IAuditLogger auditLogger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _emailVerificationService = emailVerificationService ?? throw new ArgumentNullException(nameof(emailVerificationService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
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

        var userId = Guid.NewGuid();

        // C5 + F1: the bootstrap-admin claim happens AFTER the user is
        // committed (see further down). The user starts as Role.User by
        // default; if and only if the post-commit atomic claim wins the
        // single-use race, the user is promoted to Admin via a second
        // SaveChanges. This sequence avoids the FK constraint on
        // SystemConfiguration.CreatedByUserId (which restricts to a real
        // existing user) and removes the TOCTOU race the original
        // read-then-write design left open under concurrent valid-token
        // registrations.
        var role = Role.User;

        // Reject self-assignment of elevated roles for the explicit-role
        // pathway (admins assign roles via Administration BC, not /auth/register).
        if (!string.IsNullOrWhiteSpace(command.Role))
        {
            var requested = Role.Parse(command.Role);
            if (requested.IsAdmin() || requested.IsEditor())
            {
                throw new DomainException("Only administrators can assign elevated roles");
            }
            role = requested;
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

        // C5 + F1: bootstrap-admin claim runs only AFTER the user is committed,
        // so the SystemConfiguration.CreatedByUserId FK can reference this
        // newly-persisted user. The claim itself is atomic — even under
        // concurrent valid-token registrations, exactly one caller can flip
        // the singleton row's BootstrapAdminCreated flag from false to true.
        if (await TryClaimBootstrapAdminAsync(command.BootstrapToken, userId, cancellationToken)
            .ConfigureAwait(false))
        {
            user.UpdateRole(Role.Admin);
            await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // I10: bootstrap admin provisioned. This is a one-time, high-
            // privilege transition; the security review requires it be
            // surfaced explicitly so any operator can spot a successful
            // bootstrap on the audit dashboard.
            await _auditLogger.LogAsync(
                Api.BoundedContexts.SecurityAudit.Application.Services.AuditEventType.BootstrapAdminCreated,
                actorUserId: user.Id,
                targetUserId: user.Id,
                ipAddress: command.IpAddress,
                userAgent: command.UserAgent,
                cancellationToken: cancellationToken).ConfigureAwait(false);
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
    /// C5 + F1: atomic single-use bootstrap-admin claim. Returns true only
    /// when the supplied token matches the configured one (constant-time)
    /// AND this call wins the race to flip the singleton SystemConfiguration
    /// row's <c>BootstrapAdminCreated</c> flag from false to true. Concurrent
    /// valid-token callers all hit the same row but only one transitions the
    /// flag — Postgres serialises both the unique-constraint INSERT and the
    /// conditional UPDATE at the row level. Losers return false → caller
    /// keeps the user at Role.User.
    ///
    /// The claim runs after the bootstrapping user is already committed so
    /// SystemConfiguration.CreatedByUserId (FK Restrict on users.Id) is
    /// satisfied. If the claim succeeds the caller MUST promote the user via
    /// <see cref="User.UpdateRole"/> + a follow-up SaveChanges.
    /// </summary>
    private async Task<bool> TryClaimBootstrapAdminAsync(
        string? providedToken, Guid bootstrappingUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providedToken))
            return false;

        var configuredToken = _configuration["Authentication:BootstrapAdminToken"];
        if (string.IsNullOrWhiteSpace(configuredToken))
            return false;

        // Constant-time compare: equal-length is required by FixedTimeEquals,
        // so length-mismatch is detected explicitly without short-circuiting
        // on the data itself.
        var configBytes = Encoding.UTF8.GetBytes(configuredToken);
        var inputBytes = Encoding.UTF8.GetBytes(providedToken);
        if (configBytes.Length != inputBytes.Length)
            return false;
        if (!CryptographicOperations.FixedTimeEquals(configBytes, inputBytes))
            return false;

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Step 1 — atomic flip on the existing row, if any. ExecuteUpdateAsync
        // emits a single SQL statement; Postgres takes a row-level lock for
        // the duration of the WHERE evaluation + SET, so concurrent callers
        // serialise here: exactly one observes the flag-false condition and
        // flips it. The others get rowsAffected == 0.
        var rowsAffected = await _db.Set<SystemConfigurationEntity>()
            .Where(c => c.Key == BootstrapAdminConfigKey
                        && c.Environment == "All"
                        && !c.BootstrapAdminCreated)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.BootstrapAdminCreated, true)
                .SetProperty(c => c.BootstrapAdminCreatedAt, (DateTime?)now)
                .SetProperty(c => c.UpdatedAt, now)
                .SetProperty(c => c.UpdatedByUserId, (Guid?)bootstrappingUserId), cancellationToken)
            .ConfigureAwait(false);

        if (rowsAffected == 1)
            return true;

        if (rowsAffected > 1)
        {
            _logger.LogError(
                "Bootstrap claim affected {RowCount} rows; expected 0 or 1. " +
                "The (Key, Environment) unique index on system_configurations should " +
                "make this impossible — investigate schema drift.",
                rowsAffected);
            return false;
        }

        // rowsAffected == 0 means either the row already exists with flag=true
        // (post-bootstrap re-use — token must be rotated) OR the row doesn't
        // exist yet (cold start). Distinguish via a read.
        var alreadyClaimed = await _db.Set<SystemConfigurationEntity>()
            .AsNoTracking()
            .AnyAsync(c => c.Key == BootstrapAdminConfigKey
                        && c.Environment == "All"
                        && c.BootstrapAdminCreated, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyClaimed)
        {
            _logger.LogWarning(
                "Bootstrap admin token presented after the first admin was already provisioned — " +
                "rejecting privilege escalation; rotate AUTHENTICATION__BOOTSTRAPADMINTOKEN.");
            return false;
        }

        // Step 2 — cold-start INSERT. The (Key, Environment) unique index
        // serialises concurrent INSERTs: at most one wins, the rest get a
        // 23505. Losers detach the failed entity and retry the atomic flip
        // (which now finds the row the winner just created and flips it iff
        // they were the winner — they weren't, so they get 0 rows).
        var newRow = new SystemConfigurationEntity
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
            CreatedByUserId = bootstrappingUserId,
            UpdatedByUserId = bootstrappingUserId,
            BootstrapAdminCreated = true,
            BootstrapAdminCreatedAt = now,
        };

        try
        {
            _db.Set<SystemConfigurationEntity>().Add(newRow);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _db.Set<SystemConfigurationEntity>().Entry(newRow).State = EntityState.Detached;
            _logger.LogWarning(ex,
                "Lost the cold-start bootstrap INSERT race; another concurrent caller " +
                "won. Falling back to Role.User.");
            return false;
        }
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
