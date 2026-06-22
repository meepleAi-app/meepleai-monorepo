using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for <see cref="ImpersonationStartCommand"/> (SP5 Admin Security S2 — T3).
///
/// Creates an impersonation session via <see cref="CreateSessionCommand"/> (extended in S2 to
/// carry the dual-principal fields). Audit is NO LONGER written here: the
/// <c>AuditLoggingBehavior</c> + <c>[AtomicAudit]</c> pipeline writes a single Pending
/// <c>audit_outbox</c> row in the same transaction (replacing the legacy two manual
/// <c>IAuditLogRepository.AddAsync</c> calls — see issue #1534).
///
/// Eligibility (D-S2-1, tightened vs legacy):
///   1. Requester MUST be superadmin (legacy accepted admin too — removed).
///   2. Requester != target (self-impersonation forbidden; also guarded by the validator).
///   3. Target MUST NOT be admin or superadmin (no peer/superior impersonation).
///   4. Target MUST NOT be suspended.
///   5. Target MUST NOT be a demo account.
///   6. Target MUST NOT be banned (Status == "Banned").
/// </summary>
internal sealed class ImpersonationStartCommandHandler
    : IRequestHandler<ImpersonationStartCommand, ImpersonationStartResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IMediator _mediator;
    private readonly TimeProvider _timeProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ImpersonationStartCommandHandler> _logger;

    public ImpersonationStartCommandHandler(
        IUserRepository userRepository,
        IMediator mediator,
        TimeProvider timeProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ImpersonationStartCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ImpersonationStartResponseDto> Handle(
        ImpersonationStartCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogWarning(
            "SECURITY: superadmin {RequesterId} attempting to impersonate user {TargetUserId}",
            command.RequestingUserId, command.TargetUserId);

        // Load requester first (prevents target enumeration before authz).
        var requester = await _userRepository.GetByIdAsync(command.RequestingUserId, cancellationToken)
            .ConfigureAwait(false);
        if (requester is null)
        {
            throw new NotFoundException($"Requesting user with ID '{command.RequestingUserId}' not found");
        }

        // D-S2-1 rule 1: superadmin only (tightened — legacy allowed admin too).
        if (!string.Equals(requester.Role.Value, "superadmin", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Only superadmins can impersonate users");
        }

        // D-S2-1 rule 2: self-impersonation forbidden (validator also guards this).
        if (command.RequestingUserId == command.TargetUserId)
        {
            throw new ForbiddenException("Cannot impersonate yourself");
        }

        var target = await _userRepository.GetByIdAsync(command.TargetUserId, cancellationToken)
            .ConfigureAwait(false);
        if (target is null)
        {
            throw new NotFoundException($"Target user with ID '{command.TargetUserId}' not found");
        }

        // D-S2-1 rule 3: cannot impersonate a peer or higher (admin/superadmin).
        if (string.Equals(target.Role.Value, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target.Role.Value, "superadmin", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "SECURITY: superadmin {RequesterId} attempted to impersonate privileged user {TargetUserId} (role {Role})",
                command.RequestingUserId, command.TargetUserId, target.Role.Value);
            throw new ForbiddenException("Cannot impersonate admin or superadmin users");
        }

        // D-S2-1 rule 6: cannot impersonate a banned account. Checked BEFORE the suspended guard
        // because User.Ban() also sets IsSuspended=true for backward compat (Epic #4068); a banned
        // target would otherwise be caught by rule 4 and surface a misleading "suspended" error.
        if (target.Status == UserAccountStatus.Banned)
        {
            throw new ConflictException($"Cannot impersonate banned user '{command.TargetUserId}'");
        }

        // D-S2-1 rule 4: cannot impersonate a suspended account.
        if (target.IsSuspended)
        {
            throw new ConflictException($"Cannot impersonate suspended user '{command.TargetUserId}'");
        }

        // D-S2-1 rule 5: cannot impersonate a demo account.
        if (target.IsDemoAccount)
        {
            throw new ConflictException($"Cannot impersonate demo account '{command.TargetUserId}'");
        }

        // Create the impersonation session. UserId = target (subject); ImpersonatedByUserId =
        // requester (actor); ImpersonatedUntil caps the lifetime to DurationMinutes (D-S2-4).
        // No more magic-string IpAddress="impersonated" — the dual-principal columns carry the
        // impersonation state explicitly.
        //
        // SP5 S3 spike §5: inherit the actor's LastTotpVerifiedAt into the new impersonate
        // session. The impersonation MaxAge clock starts from the actor's most recent TOTP
        // verification — semantically correct (the admin's step-up gates impersonate behavior,
        // not the target's enrollment).
        var impersonatedUntil = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(command.DurationMinutes);
        var actorLastTotpVerifiedAt = ExtractCallerLastTotpVerifiedAt();
        var createSessionCommand = new CreateSessionCommand(
            UserId: command.TargetUserId,
            IpAddress: null,
            UserAgent: $"Impersonation by superadmin {requester.DisplayName ?? requester.Email.Value}",
            ImpersonatedByUserId: command.RequestingUserId,
            ImpersonatedUntil: impersonatedUntil,
            LastTotpVerifiedAt: actorLastTotpVerifiedAt);

        var sessionResponse = await _mediator.Send(createSessionCommand, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogWarning(
            "SECURITY: impersonation started — superadmin {RequesterId} -> user {TargetUserId}, expires {ImpersonatedUntil:O}",
            command.RequestingUserId, command.TargetUserId, impersonatedUntil);

        // NB: the audit row is written by AuditLoggingBehavior ([AtomicAudit]) in the same
        // transaction. No manual IAuditLogRepository.AddAsync here (dismantled — issue #1534).

        return new ImpersonationStartResponseDto(
            SessionId: sessionResponse.SessionId,
            SessionToken: sessionResponse.SessionToken,
            ImpersonatedUserId: command.TargetUserId,
            ImpersonatedUntil: impersonatedUntil,
            ExpiresAt: sessionResponse.ExpiresAt);
    }

    /// <summary>
    /// Reads the acting admin's <c>LastTotpVerifiedAt</c> from the current request's session
    /// (HttpContext-populated by <c>SessionAuthenticationMiddleware</c>). Returns null when no
    /// session is in scope (defensive; the endpoint is gated by <c>RequireSuperAdmin</c> so this
    /// path is normally unreachable) or when the actor has no recorded TOTP verification.
    ///
    /// SP5 S3 spike §5: enables the new impersonate session to inherit the actor's TOTP recency
    /// so the strict <c>TwoFactorEnforcementBehavior</c> can gate subsequent commands without a
    /// separate step-up.
    /// </summary>
    private DateTime? ExtractCallerLastTotpVerifiedAt()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        if (httpContext.Items.TryGetValue(nameof(SessionStatusDto), out var value)
            && value is SessionStatusDto { IsValid: true } sessionStatus)
        {
            return sessionStatus.LastTotpVerifiedAt;
        }

        return null;
    }
}
