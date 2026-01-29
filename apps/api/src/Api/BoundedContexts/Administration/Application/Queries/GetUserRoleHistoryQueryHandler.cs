using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for GetUserRoleHistoryQuery (Issue #2890).
/// Retrieves role change history from audit logs.
/// </summary>
internal sealed class GetUserRoleHistoryQueryHandler
    : IRequestHandler<GetUserRoleHistoryQuery, List<RoleChangeHistoryDto>>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<GetUserRoleHistoryQueryHandler> _logger;

    public GetUserRoleHistoryQueryHandler(
        IAuditLogRepository auditLogRepository,
        ILogger<GetUserRoleHistoryQueryHandler> logger)
    {
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<RoleChangeHistoryDto>> Handle(
        GetUserRoleHistoryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation("Getting role change history for user {UserId}", query.UserId);

        // Get audit logs for role changes
        var auditLogs = await _auditLogRepository.GetByUserIdAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Filter for role change actions and parse details
        var roleChanges = auditLogs
            .Where(log => log.Action.Equals("role_changed", StringComparison.OrdinalIgnoreCase))
            .Select(log =>
            {
                // Parse details JSON to extract old/new roles
                var details = System.Text.Json.JsonDocument.Parse(log.Details ?? "{}");
                var oldRole = details.RootElement.TryGetProperty("oldRole", out var oldRoleEl)
                    ? oldRoleEl.GetString() ?? "unknown"
                    : "unknown";
                var newRole = details.RootElement.TryGetProperty("newRole", out var newRoleEl)
                    ? newRoleEl.GetString() ?? "unknown"
                    : "unknown";
                var changedBy = details.RootElement.TryGetProperty("changedBy", out var changedByEl)
                    ? Guid.Parse(changedByEl.GetString() ?? Guid.Empty.ToString())
                    : Guid.Empty;
                var changedByName = details.RootElement.TryGetProperty("changedByDisplayName", out var nameEl)
                    ? nameEl.GetString() ?? "System"
                    : "System";

                return new RoleChangeHistoryDto(
                    ChangedAt: log.CreatedAt,
                    OldRole: oldRole,
                    NewRole: newRole,
                    ChangedBy: changedBy,
                    ChangedByDisplayName: changedByName,
                    IpAddress: log.IpAddress
                );
            })
            .OrderByDescending(rc => rc.ChangedAt)
            .ToList();

        _logger.LogInformation("Retrieved {Count} role changes for user {UserId}",
            roleChanges.Count, query.UserId);

        return roleChanges;
    }
}
