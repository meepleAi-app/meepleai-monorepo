using System.Reflection;
using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Attributes;
using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Infrastructure.Persistence;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that automatically enqueues admin actions to the audit_outbox for
/// commands decorated with <see cref="AuditableActionAttribute"/>. Issue #3691: Audit Log System.
///
/// Flow:
///   1. next() runs the handler (handler's SaveChanges fires — interceptor populates the sink).
///   2. After next() returns, drain <see cref="ScopedAuditSnapshotSink.Snapshots"/> → fold into payload.
///   3. Call <see cref="AuditService.EnqueueAuditAsync"/> → writes a Pending row to audit_outbox.
///   4. The T4 AuditOutboxProcessor materializes the row into a permanent AuditLogEntity.
///
/// Resilience: audit failures never break business operations (best-effort, T3).
/// Atomicity: separate SaveChanges from handler's business SaveChanges (T3b adds AtomicAudit for
/// destructive commands).
/// </summary>
internal sealed class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuditService _auditService;
    private readonly ScopedAuditSnapshotSink _sink;
    private readonly ILogger<AuditLoggingBehavior<TRequest, TResponse>> _logger;

    public AuditLoggingBehavior(
        IHttpContextAccessor httpContextAccessor,
        AuditService auditService,
        ScopedAuditSnapshotSink sink,
        ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _auditService = auditService;
        _sink = sink;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var auditAttribute = typeof(TRequest).GetCustomAttribute<AuditableActionAttribute>();

        // Skip if command is not decorated with [AuditableAction]
        if (auditAttribute is null)
        {
            return await next().ConfigureAwait(false);
        }

        var (adminUserId, adminEmail, ipAddress, userAgent) = ExtractRequestContext();
        var resourceId = ExtractResourceId(request);

        try
        {
            var response = await next().ConfigureAwait(false);

            // next() has completed — the handler's SaveChanges has fired, so the interceptor has
            // populated the sink with any [Auditable] entity snapshots.
            await EnqueueAuditAsync(
                auditAttribute,
                adminUserId,
                resourceId,
                "Success",
                BuildMetadata(auditAttribute, adminEmail, request),
                ipAddress,
                userAgent,
                requestType: typeof(TRequest).Name,
                cancellationToken).ConfigureAwait(false);

            return response;
        }
        catch (Exception ex)
        {
            // Log failed action before re-throwing.
            // The interceptor may have partially populated the sink if SaveChanges was called
            // before the exception (e.g. inside the handler). We include whatever was captured.
            await EnqueueAuditAsync(
                auditAttribute,
                adminUserId,
                resourceId,
                "Error",
                BuildErrorMetadata(auditAttribute, adminEmail, request, ex),
                ipAddress,
                userAgent,
                requestType: typeof(TRequest).Name,
                cancellationToken).ConfigureAwait(false);

            throw;
        }
    }

    private (string? UserId, string? Email, string? IpAddress, string? UserAgent) ExtractRequestContext()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return (null, null, null, null);
        }

        string? userId = null;
        string? email = null;

        if (httpContext.Items.TryGetValue(nameof(SessionStatusDto), out var value)
            && value is SessionStatusDto { IsValid: true, User: not null } session)
        {
            userId = session.User.Id.ToString();
            email = session.User.Email;
        }

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.FirstOrDefault();

        return (userId, email, ipAddress, userAgent);
    }

    private static string? ExtractResourceId(TRequest request)
    {
        // Try common property names for resource identification
        var type = typeof(TRequest);
        var idProp = type.GetProperty("Id") ?? type.GetProperty("UserId") ?? type.GetProperty("TargetUserId");

        return idProp?.GetValue(request)?.ToString();
    }

    private static string BuildMetadata(AuditableActionAttribute attr, string? adminEmail, TRequest request)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["confirmationLevel"] = attr.Level,
            ["adminEmail"] = adminEmail,
            ["commandType"] = typeof(TRequest).Name
        };

        // Extract relevant properties from the command (exclude sensitive data)
        foreach (var prop in typeof(TRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.Name is "Password" or "Token" or "Secret" or "ApiKey")
                continue;

            var val = prop.GetValue(request);
            if (val is not null)
            {
                metadata[prop.Name] = val.ToString();
            }
        }

        return JsonSerializer.Serialize(metadata);
    }

    private static string BuildErrorMetadata(AuditableActionAttribute attr, string? adminEmail, TRequest request, Exception ex)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["confirmationLevel"] = attr.Level,
            ["adminEmail"] = adminEmail,
            ["commandType"] = typeof(TRequest).Name,
            ["errorType"] = ex.GetType().Name,
            ["errorMessage"] = ex.Message
        };

        return JsonSerializer.Serialize(metadata);
    }

    private async Task EnqueueAuditAsync(
        AuditableActionAttribute attr,
        string? adminUserId,
        string? resourceId,
        string result,
        string details,
        string? ipAddress,
        string? userAgent,
        string? requestType,
        CancellationToken cancellationToken)
    {
        try
        {
            // Drain snapshots from the sink (populated by AuditingSaveChangesInterceptor during next()).
            var snapshotPayloads = _sink.Snapshots
                .Select(s => new AuditSnapshotPayload
                {
                    EntityType = s.EntityType,
                    PrimaryKey = s.PrimaryKey,
                    BeforeJson = s.BeforeJson,
                    AfterJson = s.AfterJson,
                    Operation = s.Operation.ToString(),
                })
                .ToList();

            // Detect oversize: if PayloadTruncator flagged any snapshot, mark the payload so the
            // T4 processor can MarkFailed with last_error="payload_oversize" instead of persisting
            // potentially inaccurate truncated data.
            var oversize = snapshotPayloads.Any(s =>
                (s.BeforeJson != null && s.BeforeJson.Contains("\"_oversize\":true", StringComparison.Ordinal)) ||
                (s.AfterJson  != null && s.AfterJson.Contains("\"_oversize\":true",  StringComparison.Ordinal)));

            // Clear sink after draining — per-request scoped so this is defensive cleanup against
            // edge-cases where multiple [AuditableAction] commands run within one scope.
            _sink.Clear();

            var payload = new AuditOutboxPayload
            {
                Action      = attr.Action,
                Resource    = attr.Resource,
                UserId      = adminUserId,
                ResourceId  = resourceId,
                Result      = result,
                IpAddress   = ipAddress,
                UserAgent   = userAgent,
                RequestType = requestType,
                Details     = details,
                Snapshots   = snapshotPayloads,
                // Populated by S2 (impersonation) / S3 (step-up); null until then.
                ImpersonatedUserId = null,
                StepUpTokenId      = null,
                Timestamp   = DateTimeOffset.UtcNow,
                Oversize    = oversize,
            };

            await _auditService.EnqueueAuditAsync(payload, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Resilience pattern: audit failures must never break business operations
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex,
                "Failed to enqueue audit outbox entry for action {Action} on {Resource} by admin {AdminUserId}",
                attr.Action, attr.Resource, adminUserId);
        }
    }
}
