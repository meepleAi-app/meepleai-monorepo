using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for ImpersonateUserCommand (Issue #2890).
/// Creates session as target user for admin debugging.
/// HIGH SECURITY RISK: Logs all impersonation actions.
/// </summary>
internal sealed class ImpersonateUserCommandHandler
    : IRequestHandler<ImpersonateUserCommand, ImpersonateUserResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ImpersonateUserCommandHandler> _logger;

    public ImpersonateUserCommandHandler(
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository,
        IMediator mediator,
        IUnitOfWork unitOfWork,
        ILogger<ImpersonateUserCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ImpersonateUserResponseDto> Handle(
        ImpersonateUserCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogWarning("⚠️ SECURITY: Admin {AdminId} attempting to impersonate user {TargetUserId}",
            command.AdminUserId, command.TargetUserId);

        // Verify target user exists
        var targetUser = await _userRepository.GetByIdAsync(command.TargetUserId, cancellationToken)
            .ConfigureAwait(false);
        if (targetUser is null)
        {
            throw new NotFoundException($"User with ID '{command.TargetUserId}' not found");
        }

        // Check if target user is suspended (Issue #2890 code review)
        if (targetUser.IsSuspended)
        {
            throw new ConflictException($"Cannot impersonate suspended user '{command.TargetUserId}'");
        }

        // Verify admin exists and has admin role
        var adminUser = await _userRepository.GetByIdAsync(command.AdminUserId, cancellationToken)
            .ConfigureAwait(false);
        if (adminUser is null)
        {
            throw new NotFoundException($"Admin user with ID '{command.AdminUserId}' not found");
        }

        if (!string.Equals(adminUser.Role.Value, "admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("Only admins can impersonate users");
        }

        // Create session via MediatR (reuse existing command)
        var createSessionCommand = new CreateSessionCommand(
            UserId: command.TargetUserId,
            IpAddress: "impersonated",
            UserAgent: $"Admin Impersonation by {adminUser.DisplayName ?? adminUser.Email.Value}"
        );

        var sessionResponse = await _mediator.Send(createSessionCommand, cancellationToken)
            .ConfigureAwait(false);

        // Create audit logs for both admin and target user
        var adminAuditLog = new Api.BoundedContexts.Administration.Domain.Entities.AuditLog(
            id: Guid.NewGuid(),
            userId: command.AdminUserId,
            action: "impersonate_user_started",
            resource: "User",
            result: "success",
            resourceId: command.TargetUserId.ToString(),
            details: System.Text.Json.JsonSerializer.Serialize(new
            {
                targetUserId = command.TargetUserId,
                targetEmail = targetUser.Email.Value,
                sessionToken = sessionResponse.SessionToken[..Math.Min(16, sessionResponse.SessionToken.Length)] + "...",
                expiresAt = sessionResponse.ExpiresAt
            }),
            ipAddress: "admin-action"
        );

        var userAuditLog = new Api.BoundedContexts.Administration.Domain.Entities.AuditLog(
            id: Guid.NewGuid(),
            userId: command.TargetUserId,
            action: "impersonated_by_admin",
            resource: "User",
            result: "security_event",
            resourceId: command.AdminUserId.ToString(),
            details: System.Text.Json.JsonSerializer.Serialize(new
            {
                adminUserId = command.AdminUserId,
                adminEmail = adminUser.Email.Value,
                sessionCreated = true
            }),
            ipAddress: "admin-impersonation"
        );

        await _auditLogRepository.AddAsync(adminAuditLog, cancellationToken).ConfigureAwait(false);
        await _auditLogRepository.AddAsync(userAuditLog, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogWarning("⚠️ SECURITY: Impersonation successful - Admin {AdminId} → User {TargetUserId}",
            command.AdminUserId, command.TargetUserId);

        return new ImpersonateUserResponseDto(
            SessionToken: sessionResponse.SessionToken,
            ImpersonatedUserId: command.TargetUserId,
            ExpiresAt: sessionResponse.ExpiresAt
        );
    }
}
