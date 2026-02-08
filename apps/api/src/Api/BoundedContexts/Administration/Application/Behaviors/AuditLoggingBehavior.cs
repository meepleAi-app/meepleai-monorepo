using System.Reflection;
using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Attributes;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that automatically logs admin actions for commands
/// decorated with [AuditableAction]. Issue #3691: Audit Log System.
///
/// Resilience pattern: audit logging failures never break business operations.
/// </summary>
internal sealed class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuditService _auditService;
    private readonly ILogger<AuditLoggingBehavior<TRequest, TResponse>> _logger;

    public AuditLoggingBehavior(
        IHttpContextAccessor httpContextAccessor,
        AuditService auditService,
        ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _auditService = auditService;
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

            // Log successful action
            await LogAuditEntryAsync(
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
            // Log failed action before re-throwing
            await LogAuditEntryAsync(
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

    private async Task LogAuditEntryAsync(
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
            await _auditService.LogAsync(
                adminUserId,
                attr.Action,
                attr.Resource,
                resourceId,
                result,
                details,
                ipAddress,
                userAgent,
                cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Resilience pattern: audit failures must never break business operations
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex,
                "Failed to write audit log for action {Action} on {Resource} by admin {AdminUserId}",
                attr.Action, attr.Resource, adminUserId);
        }
    }
}
