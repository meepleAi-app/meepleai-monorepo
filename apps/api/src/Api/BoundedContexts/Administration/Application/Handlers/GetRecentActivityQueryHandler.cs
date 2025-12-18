using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Issue #874, #878: Activity Feed Service - Handler for GetRecentActivityQuery.
/// Serves as ActivityFeedService per Issue #878 (DDD/CQRS: Handlers ARE application services).
///
/// Functionality:
/// - GetRecentActivityAsync(int count): Implemented via Handle() method
/// - Merges data sources by timestamp: User logins, PDF uploads, critical errors, config changes
/// - Returns ActivityEventDto: Wrapped in RecentActivityDto with metadata
///
/// Data Sources:
/// 1. User registrations (UserRegistered events)
/// 2. PDF uploads (PdfUploaded events)
/// 3. System alerts (AlertCreated/AlertResolved events)
/// 4. AI Request errors (ErrorOccurred events)
/// </summary>
internal class GetRecentActivityQueryHandler : IQueryHandler<GetRecentActivityQuery, RecentActivityDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GetRecentActivityQueryHandler> _logger;

    public GetRecentActivityQueryHandler(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<GetRecentActivityQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RecentActivityDto> Handle(GetRecentActivityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var since = query.Since ?? now.AddDays(-7); // Default: last 7 days
        var limit = Math.Min(query.Limit, 100); // Cap at 100 events

        _logger.LogInformation("Fetching recent activity: limit={Limit}, since={Since}", limit, since);

        // Aggregate events from multiple sources
        // Aggregate events from multiple sources
        var events = new List<ActivityEvent>();

        // 1. User registrations
        events.AddRange(await GetUserRegistrationEventsAsync(since, limit, cancellationToken).ConfigureAwait(false));

        // 2. PDF uploads
        events.AddRange(await GetPdfUploadEventsAsync(since, limit, cancellationToken).ConfigureAwait(false));

        // 3. Alerts
        events.AddRange(await GetAlertEventsAsync(since, limit, cancellationToken).ConfigureAwait(false));

        // 4. AI Request errors (sample recent errors)
        events.AddRange(await GetAiErrorEventsAsync(since, limit, cancellationToken).ConfigureAwait(false));

        // Sort all events by timestamp descending and take top N
        var sortedEvents = events
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToList();

        return new RecentActivityDto(
            Events: sortedEvents,
            TotalCount: sortedEvents.Count,
            GeneratedAt: now
        );
    }

    private async Task<List<ActivityEvent>> GetUserRegistrationEventsAsync(DateTime since, int limit, CancellationToken cancellationToken)
    {
        var userRegistrations = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.CreatedAt >= since)
            .OrderByDescending(u => u.CreatedAt)
            .Take(limit)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.CreatedAt
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return userRegistrations.Select(u => new ActivityEvent(
            u.Id.ToString(),
            ActivityEventType.UserRegistered,
            $"New user registered: {u.Email}",
            u.Id.ToString(),
            u.Email,
            u.Id.ToString(),
            "User",
            u.CreatedAt,
            ActivitySeverity.Info
        )).ToList();
    }

    private async Task<List<ActivityEvent>> GetPdfUploadEventsAsync(DateTime since, int limit, CancellationToken cancellationToken)
    {
        var pdfUploads = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(pdf => pdf.UploadedAt >= since)
            .OrderByDescending(pdf => pdf.UploadedAt)
            .Take(limit)
            .Select(pdf => new
            {
                pdf.Id,
                pdf.FileName,
                pdf.FileSizeBytes,
                pdf.UploadedByUserId,
                pdf.UploadedAt
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return pdfUploads.Select(pdf => new ActivityEvent(
            pdf.Id.ToString(),
            ActivityEventType.PdfUploaded,
            $"PDF uploaded: {pdf.FileName} ({pdf.FileSizeBytes} bytes)",
            pdf.UploadedByUserId.ToString(),
            null,
            pdf.Id.ToString(),
            "PdfDocument",
            pdf.UploadedAt,
            ActivitySeverity.Info
        )).ToList();
    }

    private async Task<List<ActivityEvent>> GetAlertEventsAsync(DateTime since, int limit, CancellationToken cancellationToken)
    {
        var alerts = await _dbContext.Alerts
            .AsNoTracking()
            .Where(a => a.TriggeredAt >= since)
            .OrderByDescending(a => a.TriggeredAt)
            .Take(limit)
            .Select(a => new
            {
                a.Id,
                a.Message,
                a.Severity,
                a.IsActive,
                a.ResolvedAt,
                a.TriggeredAt
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return alerts.Select(a => new ActivityEvent(
            a.Id.ToString(),
            !a.IsActive && a.ResolvedAt.HasValue ? ActivityEventType.AlertResolved : ActivityEventType.AlertCreated,
            $"Alert: {a.Message} (Severity: {a.Severity})",
            null,
            null,
            a.Id.ToString(),
            "Alert",
            !a.IsActive && a.ResolvedAt.HasValue ? a.ResolvedAt.Value : a.TriggeredAt,
            MapAlertSeverity(a.Severity)
        )).ToList();
    }

    private async Task<List<ActivityEvent>> GetAiErrorEventsAsync(DateTime since, int limit, CancellationToken cancellationToken)
    {
        var recentErrors = await _dbContext.AiRequestLogs
            .AsNoTracking()
            .Where(log => log.CreatedAt >= since && log.Status != "Success")
            .OrderByDescending(log => log.CreatedAt)
            .Take(limit / 2) // Fewer errors in feed
            .Select(log => new
            {
                log.Id,
                log.ErrorMessage,
                log.Endpoint,
                log.UserId,
                log.CreatedAt
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return recentErrors.Select(log => new ActivityEvent(
            log.Id.ToString(),
            ActivityEventType.ErrorOccurred,
            $"AI Request failed: {log.ErrorMessage ?? "Unknown error"} (Endpoint: {log.Endpoint})",
            log.UserId?.ToString(),
            null,
            log.Id.ToString(),
            "AiRequestLog",
            log.CreatedAt,
            ActivitySeverity.Error
        )).ToList();
    }

    private static ActivitySeverity MapAlertSeverity(string alertSeverity)
    {
        return alertSeverity?.ToLowerInvariant() switch
        {
            "critical" => ActivitySeverity.Critical,
            "error" => ActivitySeverity.Error,
            "warning" => ActivitySeverity.Warning,
            _ => ActivitySeverity.Info
        };
    }
}
