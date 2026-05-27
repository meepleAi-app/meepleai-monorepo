using System.Reflection;
using Api.BoundedContexts.Authentication.Application.Attributes;
using Api.BoundedContexts.Authentication.Application.Configuration;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Infrastructure.Security;
using Api.Middleware.Exceptions;
using Api.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.Authentication.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that enforces 2FA (TOTP) recency for commands
/// decorated with <see cref="RequireTwoFactorAttribute"/>.
///
/// Q2 2026 Security Review (#186) P1.1: 2FA admin enforcement.
///
/// Initial rollout strategy (shadow mode):
/// - When a decorated command is invoked AND the session user has 2FA enabled,
///   the behavior CHECKS whether TOTP was recently verified (per attribute MaxAgeMinutes).
/// - On miss: logs a structured WARNING and proceeds (does NOT block).
/// - This produces observability data for tuning thresholds before strict mode.
///
/// Strict-mode transition (subsequent PR):
/// - Replace the warning-only path with throw of UnauthorizedAccessException
///   (or domain-specific TwoFactorRequiredException).
/// - Add session schema field LastTotpVerifiedAt to track recency precisely.
///
/// SP5 Admin Security S3 (strict cutover):
/// - Gated by the <c>TwoFactor:StrictMode</c> dynamic flag (default false). When false, the
///   shadow behavior above is preserved (log + proceed). When true, a missing 2FA enrollment or
///   stale TOTP recency throws <see cref="TwoFactorRequiredException"/> (mapped to 401 + subcode).
/// - 2FA enforcement is an AUTHORIZATION check, so it reads <c>Principal.EffectiveActor</c> (the
///   real acting admin during impersonation), NOT <c>Subject</c>. The session's
///   <c>LastTotpVerifiedAt</c> is the actor's recency (inherited at impersonation start — S3 T1).
/// - On a strict block, a forensic <c>TwoFactorRequired</c> audit is emitted from a FRESH DI scope
///   so it commits independently of the command's (possibly <c>[AtomicAudit]</c>) transaction,
///   which rolls back when this behavior throws. Consistent with the existing 2FA audit family
///   (<c>TotpService</c> uses <c>AuditService.LogAsync</c> direct-write).
/// </summary>
internal sealed class TwoFactorEnforcementBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITwoFactorEnforcementConfiguration _twoFactorConfig;
    private readonly TimeProvider _timeProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TwoFactorEnforcementBehavior<TRequest, TResponse>> _logger;

    public TwoFactorEnforcementBehavior(
        IHttpContextAccessor httpContextAccessor,
        ITwoFactorEnforcementConfiguration twoFactorConfig,
        TimeProvider timeProvider,
        IServiceScopeFactory scopeFactory,
        ILogger<TwoFactorEnforcementBehavior<TRequest, TResponse>> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _twoFactorConfig = twoFactorConfig;
        _timeProvider = timeProvider;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var attr = typeof(TRequest).GetCustomAttribute<RequireTwoFactorAttribute>();

        // S3-7 regression guard: commands NOT decorated with [RequireTwoFactor] are never gated.
        if (attr is null)
        {
            return await next().ConfigureAwait(false);
        }

        var session = ExtractSession();
        if (session is null)
        {
            // No session in scope (e.g. internal/test caller). Don't block — we don't know.
            _logger.LogDebug(
                "TwoFactorEnforcementBehavior: no session for {CommandType}, skipping check",
                typeof(TRequest).Name);
            return await next().ConfigureAwait(false);
        }

        // 2FA enforcement is an AUTHORIZATION check → read EffectiveActor (the acting admin during
        // an impersonation), NOT Subject. During impersonate, Subject is the target user (who may
        // have no 2FA at all); the security gate applies to the real actor.
        var actor = session.Principal?.EffectiveActor;
        if (actor is null)
        {
            // Anonymous request hit a 2FA-required command — outer authorization layer
            // should have stopped this. Log defensively and proceed.
            _logger.LogWarning(
                "TwoFactorEnforcementBehavior: anonymous request to 2FA-required command {CommandType}",
                typeof(TRequest).Name);
            return await next().ConfigureAwait(false);
        }

        var strictMode = await _twoFactorConfig.GetStrictModeAsync(cancellationToken).ConfigureAwait(false);
        if (!strictMode)
        {
            LogShadow(actor, attr);
            return await next().ConfigureAwait(false);
        }

        // ── STRICT MODE (D-S3-1) ──────────────────────────────────────────────────────────────
        // D-S3-5: hard block when the actor has no 2FA enrolled at all.
        if (!actor.IsTwoFactorEnabled)
        {
            _logger.LogWarning(
                "TwoFactorEnforcementBehavior[strict]: actor {UserId} ({Email}) BLOCKED on {CommandType} — 2FA not enrolled.",
                actor.Id, DataMasking.MaskEmail(actor.Email), typeof(TRequest).Name);
            await EmitTwoFactorRequiredAuditAsync(actor, attr, "enroll_required", cancellationToken).ConfigureAwait(false);
            throw new TwoFactorRequiredException(
                TwoFactorRequiredSubcode.EnrollRequired,
                $"Two-factor authentication must be enabled to perform this action. {attr.Reason}".Trim());
        }

        // D-S3-1/D-S3-7: block when the TOTP recency exceeds the per-command MaxAgeMinutes.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var lastVerified = session.LastTotpVerifiedAt;
        var isRecent = lastVerified is not null
            && (now - lastVerified.Value) <= TimeSpan.FromMinutes(attr.MaxAgeMinutes);
        if (!isRecent)
        {
            _logger.LogWarning(
                "TwoFactorEnforcementBehavior[strict]: actor {UserId} BLOCKED on {CommandType} — TOTP stale "
                + "(lastVerified={LastVerified}, maxAge={MaxAgeMinutes}min). Step-up required.",
                actor.Id, typeof(TRequest).Name, lastVerified, attr.MaxAgeMinutes);
            await EmitTwoFactorRequiredAuditAsync(actor, attr, "step_up_required", cancellationToken).ConfigureAwait(false);
            throw new TwoFactorRequiredException(
                TwoFactorRequiredSubcode.StepUpRequired,
                $"Recent two-factor verification is required for this action. {attr.Reason}".Trim());
        }

        return await next().ConfigureAwait(false);
    }

    private void LogShadow(UserDto actor, RequireTwoFactorAttribute attr)
    {
        if (!actor.IsTwoFactorEnabled)
        {
            _logger.LogWarning(
                "TwoFactorEnforcementBehavior[shadow]: actor {UserId} ({Email}) invoked 2FA-required command "
                + "{CommandType} WITHOUT 2FA enabled. Reason: {Reason}. MaxAge: {MaxAgeMinutes}min",
                actor.Id, DataMasking.MaskEmail(actor.Email), typeof(TRequest).Name, attr.Reason ?? "(unspecified)", attr.MaxAgeMinutes);
        }
        else
        {
            _logger.LogInformation(
                "TwoFactorEnforcementBehavior[shadow]: actor {UserId} invoked 2FA-required command "
                + "{CommandType} with 2FA enabled. (Strict mode OFF — not enforcing recency.)",
                actor.Id, typeof(TRequest).Name);
        }
    }

    /// <summary>
    /// Emits the forensic <c>TwoFactorRequired</c> audit from a FRESH DI scope so it commits
    /// independently of the command's (possibly <c>[AtomicAudit]</c>) transaction — which rolls
    /// back when this behavior throws. Best-effort: <c>AuditService.LogAsync</c> swallows its own
    /// failures, and this wrapper additionally guards against scope/resolution errors so the
    /// security gate (the throw) is never suppressed by an audit-side problem. D-S3-6.
    /// </summary>
    private async Task EmitTwoFactorRequiredAuditAsync(
        UserDto actor, RequireTwoFactorAttribute attr, string subcode, CancellationToken cancellationToken)
    {
        try
        {
            var scope = _scopeFactory.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var auditService = scope.ServiceProvider.GetRequiredService<AuditService>();
                await auditService.LogAsync(
                    userId: actor.Id.ToString(),
                    action: "TwoFactorRequired",
                    resource: typeof(TRequest).Name,
                    resourceId: null,
                    result: "Blocked",
                    details: $"subcode={subcode}; maxAgeMinutes={attr.MaxAgeMinutes}",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
#pragma warning disable CA1031 // forensic audit must never suppress the security gate (the throw)
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex,
                "Failed to emit TwoFactorRequired forensic audit for {CommandType}", typeof(TRequest).Name);
        }
    }

    private SessionStatusDto? ExtractSession()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        if (httpContext.Items.TryGetValue(nameof(SessionStatusDto), out var value)
            && value is SessionStatusDto session)
        {
            return session;
        }

        return null;
    }
}
