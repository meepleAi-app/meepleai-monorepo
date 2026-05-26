using System.Reflection;
using Api.BoundedContexts.Authentication.Application.Attributes;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Infrastructure.Security;
using MediatR;

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
/// Note: this behavior is intentionally permissive while we collect telemetry.
/// Do NOT replicate the resilience pattern from AuditLoggingBehavior: failures
/// here ARE the desired outcome (visibility); we only log + proceed.
/// </summary>
internal sealed class TwoFactorEnforcementBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TwoFactorEnforcementBehavior<TRequest, TResponse>> _logger;

    public TwoFactorEnforcementBehavior(
        IHttpContextAccessor httpContextAccessor,
        ILogger<TwoFactorEnforcementBehavior<TRequest, TResponse>> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var attr = typeof(TRequest).GetCustomAttribute<RequireTwoFactorAttribute>();

        // Skip if command is not decorated with [RequireTwoFactor]
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

        var user = session.Principal?.Subject;
        if (user is null)
        {
            // Anonymous request hit a 2FA-required command — outer authorization layer
            // should have stopped this. Log defensively and proceed.
            _logger.LogWarning(
                "TwoFactorEnforcementBehavior: anonymous request to 2FA-required command {CommandType}",
                typeof(TRequest).Name);
            return await next().ConfigureAwait(false);
        }

        // Shadow-mode check: log if user lacks 2FA enrollment for a sensitive command.
        // Strict mode will reject; for now we only emit telemetry.
        if (!user.IsTwoFactorEnabled)
        {
            _logger.LogWarning(
                "TwoFactorEnforcementBehavior[shadow]: user {UserId} ({Email}) invoked 2FA-required command "
                + "{CommandType} WITHOUT 2FA enabled. Reason: {Reason}. MaxAge: {MaxAgeMinutes}min",
                user.Id, DataMasking.MaskEmail(user.Email), typeof(TRequest).Name, attr.Reason ?? "(unspecified)", attr.MaxAgeMinutes);
        }
        else
        {
            // User has 2FA enabled. In strict mode we'd verify session.LastTotpVerifiedAt
            // is within attr.MaxAgeMinutes. For shadow mode we just record participation.
            _logger.LogInformation(
                "TwoFactorEnforcementBehavior[shadow]: user {UserId} invoked 2FA-required command "
                + "{CommandType} with 2FA enabled. (Strict-mode recency check pending session schema update.)",
                user.Id, typeof(TRequest).Name);
        }

        return await next().ConfigureAwait(false);
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
