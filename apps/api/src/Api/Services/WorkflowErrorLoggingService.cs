using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.Services;

internal class WorkflowErrorLoggingService : IWorkflowErrorLoggingService
{
    private readonly MeepleAiDbContext _db;
    private readonly HybridCache _cache;
    private readonly ILogger<WorkflowErrorLoggingService> _logger;
    private readonly TimeProvider _timeProvider;

    public WorkflowErrorLoggingService(
        MeepleAiDbContext db,
        HybridCache cache,
        ILogger<WorkflowErrorLoggingService> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task LogErrorAsync(LogWorkflowErrorRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var entity = new WorkflowErrorLogEntity
            {
                Id = Guid.NewGuid(),
                WorkflowId = request.WorkflowId,
                ExecutionId = request.ExecutionId,
                ErrorMessage = SanitizeErrorMessage(request.ErrorMessage),
                NodeName = request.NodeName,
                RetryCount = request.RetryCount,
                StackTrace = request.StackTrace,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            _db.WorkflowErrorLogs.Add(entity);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogWarning(
                "Workflow error logged: WorkflowId={WorkflowId}, ExecutionId={ExecutionId}, NodeName={NodeName}, RetryCount={RetryCount}",
                request.WorkflowId, request.ExecutionId, request.NodeName, request.RetryCount);

            // Invalidate cache for workflow errors list
            await _cache.RemoveAsync($"workflow-errors-list", ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // RESILIENCE PATTERN: Error logging must never fail n8n webhook operations
            // Rationale: n8n workflow error logging is telemetry for debugging - failing the
            // webhook response because we cannot persist error logs would create a cascading
            // failure (error handler fails → n8n retries → more errors). We log the meta-failure
            // for monitoring but maintain webhook reliability.
            // Context: Logging failures are typically DB-related (connection loss, disk full)
            _logger.LogError(ex,
                "Failed to log workflow error for WorkflowId={WorkflowId}, ExecutionId={ExecutionId}",
                request.WorkflowId, request.ExecutionId);
        }
    }

    public async Task<PagedResult<WorkflowErrorDto>> GetErrorsAsync(
        WorkflowErrorsQueryParams queryParams,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(queryParams);
        var cacheKey = $"workflow-errors-{queryParams.WorkflowId}-{queryParams.FromDate}-{queryParams.ToDate}-{queryParams.Page}-{queryParams.Limit}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                var query = _db.WorkflowErrorLogs.AsNoTracking();

                // Apply filters
                if (!string.IsNullOrWhiteSpace(queryParams.WorkflowId))
                {
                    query = query.Where(e => e.WorkflowId == queryParams.WorkflowId);
                }

                if (queryParams.FromDate.HasValue)
                {
                    query = query.Where(e => e.CreatedAt >= queryParams.FromDate.Value);
                }

                if (queryParams.ToDate.HasValue)
                {
                    query = query.Where(e => e.CreatedAt <= queryParams.ToDate.Value);
                }

                // Get total count
                var totalCount = await query.CountAsync(cancel).ConfigureAwait(false);

                // Apply pagination and ordering
                var errors = await query
                    .OrderByDescending(e => e.CreatedAt)
                    .Skip((queryParams.Page - 1) * queryParams.Limit)
                    .Take(queryParams.Limit)
                    .Select(e => new WorkflowErrorDto(
                        e.Id,
                        e.WorkflowId,
                        e.ExecutionId,
                        e.ErrorMessage,
                        e.NodeName,
                        e.RetryCount,
                        e.StackTrace,
                        e.CreatedAt))
                    .ToListAsync(cancel).ConfigureAwait(false);

                return new PagedResult<WorkflowErrorDto>(
                    errors,
                    totalCount,
                    queryParams.Page,
                    queryParams.Limit);
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            },
            cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<WorkflowErrorDto?> GetErrorByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cacheKey = $"workflow-error-{id}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                var entity = await _db.WorkflowErrorLogs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == id, cancel).ConfigureAwait(false);

                if (entity == null)
                    return null;

                return new WorkflowErrorDto(
                    entity.Id,
                    entity.WorkflowId,
                    entity.ExecutionId,
                    entity.ErrorMessage,
                    entity.NodeName,
                    entity.RetryCount,
                    entity.StackTrace,
                    entity.CreatedAt);
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(5)
            },
            cancellationToken: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sanitizes error messages to remove potential sensitive data (security-engineer requirement).
    /// </summary>
    private static string SanitizeErrorMessage(string message)
    {
        // Remove common sensitive patterns
        var sanitized = message;

        // Remove potential API keys, tokens, passwords in error messages
        // FIX MA0009: Add timeout to prevent ReDoS attacks
        // FIX MA0023: Add ExplicitCapture to prevent capturing unneeded groups
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"(api[_-]?key|token|password|secret)[""']?\s*[:=]\s*[""']?[\w\-]{8,}",
            "$1=***REDACTED***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));

        // Truncate if too long (defense against log injection)
        if (sanitized.Length > 5000)
        {
            sanitized = sanitized.Substring(0, 5000) + "... [truncated]";
        }

        return sanitized;
    }
}
