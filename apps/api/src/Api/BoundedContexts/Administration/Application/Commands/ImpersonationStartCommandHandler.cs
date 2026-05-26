using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using MediatR;
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
    private readonly ILogger<ImpersonationStartCommandHandler> _logger;

    public ImpersonationStartCommandHandler(
        IUserRepository userRepository,
        IMediator mediator,
        TimeProvider timeProvider,
        ILogger<ImpersonationStartCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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

        // D-S2-1 rule 4: cannot impersonate a suspended account.
        if (target.IsSuspended)
        {
            throw new ConflictException($"Cannot impersonate suspended user '{command.TargetUserId}'");
        }

        // D-S2-1 rules 5+6: cannot impersonate demo or banned accounts.
        if (target.IsDemoAccount)
        {
            throw new ConflictException($"Cannot impersonate demo account '{command.TargetUserId}'");
        }
        if (string.Equals(target.Status.ToString(), "Banned", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException($"Cannot impersonate banned user '{command.TargetUserId}'");
        }

        // Create the impersonation session. UserId = target (subject); ImpersonatedByUserId =
        // requester (actor); ImpersonatedUntil caps the lifetime to DurationMinutes (D-S2-4).
        // No more magic-string IpAddress="impersonated" — the dual-principal columns carry the
        // impersonation state explicitly.
        var impersonatedUntil = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(command.DurationMinutes);
        var createSessionCommand = new CreateSessionCommand(
            UserId: command.TargetUserId,
            IpAddress: null,
            UserAgent: $"Impersonation by superadmin {requester.DisplayName ?? requester.Email.Value}",
            ImpersonatedByUserId: command.RequestingUserId,
            ImpersonatedUntil: impersonatedUntil);

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
}
