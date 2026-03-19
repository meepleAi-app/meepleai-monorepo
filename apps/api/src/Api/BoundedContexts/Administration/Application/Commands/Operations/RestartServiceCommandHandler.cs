using Api.BoundedContexts.Administration.Application.Commands.Operations;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.Operations;

/// <summary>
/// Handler for RestartServiceCommand.
/// Issue #3696: Operations - Service Control Panel.
/// Triggers graceful API shutdown; container orchestrator handles restart.
/// HIGH SECURITY RISK: SuperAdmin only, audit logged.
/// </summary>
internal sealed class RestartServiceCommandHandler
    : IRequestHandler<RestartServiceCommand, RestartServiceResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RestartServiceCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public RestartServiceCommandHandler(
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository,
        IHostApplicationLifetime lifetime,
        IUnitOfWork unitOfWork,
        ILogger<RestartServiceCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RestartServiceResponseDto> Handle(
        RestartServiceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogCritical(
            "🚨 CRITICAL: Admin {AdminId} attempting to restart service '{ServiceName}'",
            command.AdminUserId, command.ServiceName);

        // Verify admin exists and has SuperAdmin role
        var adminUser = await _userRepository.GetByIdAsync(command.AdminUserId, cancellationToken)
            .ConfigureAwait(false);

        if (adminUser is null)
        {
            throw new NotFoundException($"Admin user with ID '{command.AdminUserId}' not found");
        }

        if (!string.Equals(adminUser.Role.Value, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "⚠️ SECURITY: Non-SuperAdmin {UserId} attempted to restart service",
                command.AdminUserId);
            throw new ConflictException("Only SuperAdmin can restart services");
        }

        // Create audit log
        var auditLog = new Api.BoundedContexts.Administration.Domain.Entities.AuditLog(
            id: Guid.NewGuid(),
            userId: command.AdminUserId,
            action: "service_restart_initiated",
            resource: "Service",
            result: "initiated",
            resourceId: command.ServiceName,
            details: System.Text.Json.JsonSerializer.Serialize(new
            {
                serviceName = command.ServiceName,
                timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                adminEmail = adminUser.Email.Value
            }),
            ipAddress: "admin-action"
        );

        await _auditLogRepository.AddAsync(auditLog, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogCritical(
            "🚨 CRITICAL: Service restart initiated by SuperAdmin {AdminId} - shutting down application",
            command.AdminUserId);

        // Trigger graceful shutdown (orchestrator will restart)
        // Use Task.Run to avoid blocking the response
        _ = Task.Run(() =>
        {
            Thread.Sleep(TimeSpan.FromSeconds(2)); // Allow response to be sent
            _lifetime.StopApplication();
        }, CancellationToken.None);

        return new RestartServiceResponseDto(
            Message: "Service restart initiated. Application shutting down gracefully.",
            EstimatedDowntime: "30-60 seconds"
        );
    }
}
