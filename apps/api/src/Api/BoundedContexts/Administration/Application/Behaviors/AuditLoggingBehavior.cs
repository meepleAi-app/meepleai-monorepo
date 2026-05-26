using System.Reflection;
using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Attributes;
using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Api.BoundedContexts.Administration.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that automatically enqueues admin actions to the audit_outbox for
/// commands decorated with <see cref="AuditableActionAttribute"/>. Issue #3691: Audit Log System.
///
/// Flow (best-effort, non-atomic):
///   1. next() runs the handler (handler's SaveChanges fires — interceptor populates the sink).
///   2. After next() returns, drain <see cref="ScopedAuditSnapshotSink.Snapshots"/> → fold into payload.
///   3. Call <see cref="AuditService.EnqueueAuditAsync"/> → writes a Pending row to audit_outbox.
///   4. The T4 AuditOutboxProcessor materializes the row into a permanent AuditLogEntity.
///
/// Flow (atomic, for commands also decorated with <see cref="AtomicAuditAttribute"/>):
///   1. Open a transaction via CreateExecutionStrategy().ExecuteAsync (required for prod retry strategy).
///   2. next() runs the handler (handler's SaveChanges fires into the open tx; interceptor → sink).
///   3. Build payload from sink snapshots; call <see cref="AuditService.EnqueueAuditAtomicAsync"/>
///      (flushes outbox row into the same tx — does NOT swallow; any failure aborts the tx).
///   4. CommitAsync — mutation + outbox row commit atomically.
///   If next() throws, the enqueue throws, or commit fails → tx disposes without commit → everything
///   rolls back (mutation AND audit row). No Error audit is written on failure — the whole tx rolled
///   back, so there is no committed mutation to record. This differs from the best-effort path, which
///   writes an Error audit row on handler failure.
///
/// Resilience: audit failures in the best-effort path never break business operations (T3).
/// Atomicity: destructive commands (T3b) use [AtomicAudit] + transaction-wrapping (Q1 design).
///
/// Re-run-on-retry note (atomic path): the prod NpgsqlRetryingExecutionStrategy may re-invoke the
/// delegate on transient connection errors. For destructive admin commands (low frequency, DB-only,
/// idempotent on rollback) this is acceptable — a rolled-back first attempt leaves no committed state,
/// and EF retries only on transient connection errors, not on business-logic failures.
///
/// Domain-events caveat (atomic path): MeepleAiDbContext.SaveChangesAsync dispatches collected
/// events via MediatR.Publish INSIDE base.SaveChangesAsync (before our outer Commit). If the outer
/// transaction subsequently rolls back, event side-effects already happened. To mitigate the retry
/// case the behavior calls IDomainEventCollector.Clear() at the START of each strategy attempt so a
/// retried handler does not see stale events from a failed attempt — but it cannot undo dispatches
/// that already occurred. See [AtomicAudit] doc-comment for the formal constraint.
/// </summary>
internal sealed class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseRequest
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuditService _auditService;
    private readonly ScopedAuditSnapshotSink _sink;
    private readonly ILogger<AuditLoggingBehavior<TRequest, TResponse>> _logger;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IDomainEventCollector _eventCollector;

    public AuditLoggingBehavior(
        IHttpContextAccessor httpContextAccessor,
        AuditService auditService,
        ScopedAuditSnapshotSink sink,
        ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger,
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector)
    {
        _httpContextAccessor = httpContextAccessor;
        _auditService = auditService;
        _sink = sink;
        _logger = logger;
        _dbContext = dbContext;
        _eventCollector = eventCollector;
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

        var isAtomic = typeof(TRequest).GetCustomAttribute<AtomicAuditAttribute>() is not null;

        if (isAtomic)
        {
            return await HandleAtomicAsync(
                request, next, auditAttribute, adminUserId, adminEmail, resourceId, ipAddress, userAgent, cancellationToken)
                .ConfigureAwait(false);
        }

        return await HandleBestEffortAsync(
            request, next, auditAttribute, adminUserId, adminEmail, resourceId, ipAddress, userAgent, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Atomic path: wraps the handler's SaveChanges + audit outbox INSERT in a single transaction.
    /// Exceptions propagate (no swallow) — the transaction rolls back mutation + outbox row together.
    /// No Error audit is written on failure: the rolled-back mutation has no committed state to record.
    ///
    /// Design note: CreateExecutionStrategy() is used so the outer lambda is compatible with the
    /// NpgsqlRetryingExecutionStrategy used in production (which requires all user-opened transactions
    /// to be wrapped in ExecuteAsync). In Testcontainers integration tests (no EnableRetryOnFailure),
    /// the strategy is non-retrying and executes the delegate exactly once.
    /// </summary>
    private async Task<TResponse> HandleAtomicAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        AuditableActionAttribute auditAttribute,
        string? adminUserId,
        string? adminEmail,
        string? resourceId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        // InMemory databases (unit tests) do not support transactions. Detect here so
        // unit tests can verify routing without a real DB transaction. The real atomicity
        // guarantee is proved by integration tests (Testcontainers PostgreSQL).
        var isInMemory = string.Equals(
            _dbContext.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

        if (isInMemory)
        {
            // No-transaction path for unit tests only.
            // Clear sink + collector so a previous test/save in the same scope does not bleed in.
            _sink.Clear();
            _eventCollector.Clear();
            var result = await next().ConfigureAwait(false);
            var payload = BuildOutboxPayload(
                auditAttribute, adminUserId, resourceId, ipAddress, userAgent,
                "Success", BuildMetadata(auditAttribute, adminEmail, request));
            await _auditService.EnqueueAuditAtomicAsync(payload, cancellationToken).ConfigureAwait(false);
            _sink.Clear();
            return result;
        }

        // Production / Testcontainers path: explicit transaction wrapping via CreateExecutionStrategy.
        // CreateExecutionStrategy() is mandatory for NpgsqlRetryingExecutionStrategy (prod). The
        // strategy wraps the delegate so that transient errors trigger a retry of the whole lambda.
        // In test environments (no retry config), the strategy is non-retrying — one execution.
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // Clear sink AND domain-event collector at the start of EVERY strategy attempt. If the
            // strategy retries (transient connection error), this ensures the retried handler does
            // not observe stale snapshots/events from the failed attempt. NOTE: this does NOT undo
            // MediatR.Publish dispatches that already occurred during the failed attempt's
            // base.SaveChangesAsync — that's a known constraint of [AtomicAudit] (see attribute doc).
            _sink.Clear();
            _eventCollector.Clear();

            // BeginTransactionAsync opens a real DB transaction. The handler's UoW.SaveChangesAsync
            // flushes the mutation into this tx without committing. EnqueueAuditAtomicAsync then
            // flushes the outbox row into the same tx. CommitAsync commits both atomically.
            IDbContextTransaction? tx = null;
            try
            {
                tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

                // next() runs the full inner pipeline (validators, the handler itself, and its
                // UoW.SaveChangesAsync). The handler is UNCHANGED — it keeps its own SaveChangesAsync
                // which flushes into the open tx. The AuditingSaveChangesInterceptor fires here and
                // records [Auditable] entity snapshots into the sink.
                var txResult = await next().ConfigureAwait(false);

                // next() returned successfully; sink now contains the mutation snapshots.
                var txPayload = BuildOutboxPayload(
                    auditAttribute, adminUserId, resourceId, ipAddress, userAgent,
                    "Success", BuildMetadata(auditAttribute, adminEmail, request));

                // EnqueueAuditAtomicAsync flushes the outbox row into the open tx — does NOT swallow.
                // If it throws, the catch block below rolls back.
                await _auditService.EnqueueAuditAtomicAsync(txPayload, cancellationToken).ConfigureAwait(false);

                // Commit both the mutation and the outbox row atomically.
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

                return txResult;
            }
            catch
            {
                // Roll back on any failure — mutation + outbox row both rolled back.
                // Intentionally no Error audit: there is no committed mutation to record.
                if (tx is not null)
                {
                    try
                    {
                        await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError(rollbackEx,
                            "Rollback failed during atomic audit for action {Action} on {Resource}",
                            auditAttribute.Action, auditAttribute.Resource);
                    }
                }
                throw;
            }
            finally
            {
                // Always clear the sink — on success, on a retryable failure (the next attempt also
                // clears at its start), and on a terminal/non-retried failure (where no further
                // attempt runs). This mirrors the best-effort path's finally and prevents stale
                // snapshots from bleeding into a subsequent SaveChanges in the same request scope.
                _sink.Clear();

                if (tx is not null)
                {
                    await tx.DisposeAsync().ConfigureAwait(false);
                }
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Best-effort path (original T3 behaviour): best-effort audit enqueue, fires after handler's
    /// SaveChanges. Errors are swallowed so audit failures never break business operations.
    /// </summary>
    private async Task<TResponse> HandleBestEffortAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        AuditableActionAttribute auditAttribute,
        string? adminUserId,
        string? adminEmail,
        string? resourceId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        // The interceptor records into the per-request sink on EVERY SaveChanges. Clear here so we capture
        // ONLY the snapshots produced by this audited command's mutation — not earlier saves in the same
        // request (session/cache writes, or a prior [AuditableAction] command in a composite request).
        _sink.Clear();

        try
        {
            var response = await next().ConfigureAwait(false);

            // next() has completed — the handler's SaveChanges has fired, so the interceptor has
            // populated the sink with any [Auditable] entity snapshots.
            await EnqueueBestEffortAuditAsync(
                auditAttribute,
                adminUserId,
                resourceId,
                "Success",
                BuildMetadata(auditAttribute, adminEmail, request),
                ipAddress,
                userAgent,
                cancellationToken).ConfigureAwait(false);

            return response;
        }
        catch (Exception ex)
        {
            // Log failed action before re-throwing.
            // The interceptor may have partially populated the sink if SaveChanges was called
            // before the exception (e.g. inside the handler). We include whatever was captured.
            await EnqueueBestEffortAuditAsync(
                auditAttribute,
                adminUserId,
                resourceId,
                "Error",
                BuildErrorMetadata(auditAttribute, adminEmail, request, ex),
                ipAddress,
                userAgent,
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
            && value is SessionStatusDto { IsValid: true, Principal: not null } session)
        {
            userId = session.Principal!.Subject.Id.ToString();
            email = session.Principal!.Subject.Email;
        }

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.FirstOrDefault();

        return (userId, email, ipAddress, userAgent);
    }

    /// <summary>
    /// SP5 S2 D-S2-3: returns the impersonation ACTOR id when the current session is an active
    /// impersonation, else null. The behavior pairs this into <c>impersonated_user_id</c> so any
    /// audited command executed DURING an impersonation is attributed to both the subject
    /// (user_id) and the acting admin (impersonated_user_id). This is the S2 wiring of the column
    /// S1 left null ("wired in S2").
    /// </summary>
    private Guid? ExtractImpersonationActorId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(nameof(SessionStatusDto), out var value) == true
            && value is SessionStatusDto { IsValid: true, Principal: { Actor: not null } principal })
        {
            return principal.Actor!.Id;
        }
        return null;
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

    /// <summary>
    /// Builds the <see cref="AuditOutboxPayload"/> from the current sink state, request context,
    /// and the given <paramref name="result"/>/<paramref name="details"/>. Shared by both the
    /// atomic and best-effort paths — caller pre-builds the metadata JSON via
    /// <see cref="BuildMetadata"/> (Success) or <see cref="BuildErrorMetadata"/> (Error).
    /// </summary>
    private AuditOutboxPayload BuildOutboxPayload(
        AuditableActionAttribute attr,
        string? adminUserId,
        string? resourceId,
        string? ipAddress,
        string? userAgent,
        string result,
        string details)
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
        // potentially inaccurate truncated data. Marker contract is owned by PayloadTruncator —
        // see PayloadTruncator.OversizeMarkerJson for the verbatim string.
        var oversize = snapshotPayloads.Any(s =>
            PayloadTruncator.IsOversizeJson(s.BeforeJson) || PayloadTruncator.IsOversizeJson(s.AfterJson));

        // SP5 S2 D-S2-3: select user_id source per [AuditableAction(UserIdSource=...)].
        //   • Default (Caller): user_id = caller (e.g. UserUpdateProfile — Bob updates Bob).
        //   • ResourceId: user_id = the resource id (target of the command), and the caller is
        //     promoted to impersonated_user_id. Used for management commands like
        //     ImpersonationStartCommand where the row's natural subject is the target user.
        //
        // ⚠ Limitation: when ResourceId is selected but the request has no extractable resource id,
        // we fall back to the caller (Caller semantics). The behavior logs a warning so a misuse
        // surfaces in observability rather than producing a misleading audit row.
        // SP5 S2: actor of an active impersonation (null for regular sessions / management commands
        // whose caller is acting directly).
        var impersonationActorId = ExtractImpersonationActorId();

        string? userIdForRow;
        Guid? impersonatedUserIdForRow;
        if (attr.UserIdSource == AuditUserIdSource.ResourceId && !string.IsNullOrEmpty(resourceId))
        {
            // Management command (e.g. ImpersonationStartCommand): user_id = the target resource,
            // impersonated_user_id = the caller (the admin performing the action). D-S2-3.
            userIdForRow = resourceId;
            impersonatedUserIdForRow = Guid.TryParse(adminUserId, out var actorGuid) ? actorGuid : null;
        }
        else
        {
            if (attr.UserIdSource == AuditUserIdSource.ResourceId)
            {
                _logger.LogWarning(
                    "AuditLoggingBehavior: [AuditableAction(UserIdSource=ResourceId)] on {Command} "
                    + "but the request has no extractable ResourceId. Falling back to Caller semantics.",
                    typeof(TRequest).Name);
            }
            // Caller source: user_id = the caller (the session subject). When the session is an
            // active impersonation, the real admin (Principal.Actor) is paired into
            // impersonated_user_id — so a command executed AS the impersonated user is forensically
            // attributable to the acting admin. D-S2-3.
            userIdForRow = adminUserId;
            impersonatedUserIdForRow = impersonationActorId;
        }

        return new AuditOutboxPayload
        {
            Action      = attr.Action,
            Resource    = attr.Resource,
            UserId      = userIdForRow,
            ResourceId  = resourceId,
            Result      = result,
            IpAddress   = ipAddress,
            UserAgent   = userAgent,
            RequestType = typeof(TRequest).Name,
            Details     = details,
            Snapshots   = snapshotPayloads,
            ImpersonatedUserId = impersonatedUserIdForRow,
            StepUpTokenId      = null,  // populated by S3
            Timestamp   = DateTimeOffset.UtcNow,
            Oversize    = oversize,
        };
    }

    private async Task EnqueueBestEffortAuditAsync(
        AuditableActionAttribute attr,
        string? adminUserId,
        string? resourceId,
        string result,
        string details,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = BuildOutboxPayload(
                attr, adminUserId, resourceId, ipAddress, userAgent, result, details);

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
        finally
        {
            // Always clear the sink — even if snapshot mapping/serialization throws (the CA1031 catch
            // above swallows the error, but Clear() must still fire so stale snapshots don't bleed into
            // subsequent commands in the same request scope).
            _sink.Clear();
        }
    }
}
