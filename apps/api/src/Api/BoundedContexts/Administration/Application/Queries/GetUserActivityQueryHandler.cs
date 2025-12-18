using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handles GetUserActivityQuery to retrieve filtered audit logs for a user.
/// Issue #911 - Backend support for UserActivityTimeline component.
/// </summary>
internal sealed class GetUserActivityQueryHandler : IRequestHandler<GetUserActivityQuery, GetUserActivityResult>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<GetUserActivityQueryHandler> _logger;
    private const int MaxLimit = 500;

    public GetUserActivityQueryHandler(
        IAuditLogRepository auditLogRepository,
        ILogger<GetUserActivityQueryHandler> logger)
    {
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GetUserActivityResult> Handle(GetUserActivityQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Validate limit
        var limit = Math.Min(request.Limit > 0 ? request.Limit : 100, MaxLimit);

        _logger.LogInformation(
            "Fetching user activity for UserId={UserId}, ActionFilter={ActionFilter}, Limit={Limit}",
            request.UserId, request.ActionFilter, limit);

        // Fetch all logs for user
        var allLogs = await _auditLogRepository.GetByUserIdAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Apply filters
        var filteredLogs = allLogs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.ActionFilter))
        {
            var actions = request.ActionFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            filteredLogs = filteredLogs.Where(log => actions.Contains(log.Action, StringComparer.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.ResourceFilter))
        {
            filteredLogs = filteredLogs.Where(log =>
                log.Resource.Equals(request.ResourceFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (request.StartDate.HasValue)
        {
            filteredLogs = filteredLogs.Where(log => log.CreatedAt >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            filteredLogs = filteredLogs.Where(log => log.CreatedAt <= request.EndDate.Value);
        }

        var totalCount = filteredLogs.Count();
        var limitedLogs = filteredLogs.Take(limit).ToList();

        var activities = limitedLogs.Select(log => new UserActivityDto(
            Id: log.Id,
            Action: log.Action,
            Resource: log.Resource,
            ResourceId: log.ResourceId,
            Result: log.Result,
            Details: log.Details,
            CreatedAt: log.CreatedAt,
            IpAddress: log.IpAddress
        )).ToList();

        _logger.LogInformation(
            "Retrieved {Count} activities for UserId={UserId} (Total matching: {Total})",
            activities.Count, request.UserId, totalCount);

        return new GetUserActivityResult(activities, totalCount);
    }
}
