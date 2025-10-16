using Serilog.Core;
using Serilog.Events;

namespace Api.Logging;

/// <summary>
/// OPS-04: Enriches log events with correlation ID from HttpContext.
/// Ensures every log entry includes the request's TraceIdentifier for distributed tracing.
/// </summary>
public class CorrelationIdEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            var correlationId = httpContext.TraceIdentifier;
            var property = propertyFactory.CreateProperty("CorrelationId", correlationId);
            logEvent.AddOrUpdateProperty(property);
        }
    }
}

/// <summary>
/// OPS-04: Enriches log events with user information from authenticated context.
/// Adds UserId, UserEmail, and UserRole to all logs for audit trail.
/// </summary>
public class UserContextEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContextEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userEmail = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var userRole = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("UserId", userId));
            }

            if (!string.IsNullOrEmpty(userEmail))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("UserEmail", userEmail));
            }

            if (!string.IsNullOrEmpty(userRole))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("UserRole", userRole));
            }
        }
    }
}

/// <summary>
/// OPS-04: Enriches log events with environment information.
/// Adds deployment environment (Development, Staging, Production) to every log.
/// </summary>
public class EnvironmentEnricher : ILogEventEnricher
{
    private readonly string _environmentName;

    public EnvironmentEnricher(string environmentName)
    {
        _environmentName = environmentName;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var property = propertyFactory.CreateProperty("Environment", _environmentName);
        logEvent.AddOrUpdateProperty(property);
    }
}

/// <summary>
/// OPS-04: Enriches log events with request context information.
/// Adds RequestPath, RequestMethod, RemoteIp, UserAgent for full request context.
/// </summary>
public class RequestContextEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestContextEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("RequestPath", httpContext.Request.Path.Value ?? string.Empty));
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("RequestMethod", httpContext.Request.Method));
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("RemoteIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"));
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("UserAgent", httpContext.Request.Headers.UserAgent.ToString()));
        }
    }
}
