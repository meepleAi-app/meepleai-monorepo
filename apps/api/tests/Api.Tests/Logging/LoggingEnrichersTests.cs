using Api.Logging;
using Microsoft.AspNetCore.Http;
using Serilog.Events;
using System.Security.Claims;
using Xunit;

namespace Api.Tests.Logging;

/// <summary>
/// OPS-04: Unit tests for Serilog enrichers that add correlation IDs and context to logs.
/// </summary>
public class LoggingEnrichersTests
{
    [Fact]
    public void CorrelationIdEnricher_WithHttpContext_AddsCorrelationId()
    {
        // Arrange
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "test-trace-id-12345"
        };
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var enricher = new CorrelationIdEnricher(httpContextAccessor);

        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("CorrelationId"));
        var correlationId = logEvent.Properties["CorrelationId"];
        var scalarValue = Assert.IsType<ScalarValue>(correlationId);
        Assert.Equal("test-trace-id-12345", scalarValue.Value);
    }

    [Fact]
    public void CorrelationIdEnricher_WithoutHttpContext_DoesNotAddProperty()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor { HttpContext = null };
        var enricher = new CorrelationIdEnricher(httpContextAccessor);

        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        Assert.False(logEvent.Properties.ContainsKey("CorrelationId"));
    }

    [Fact]
    public void UserContextEnricher_WithAuthenticatedUser_AddsUserProperties()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var enricher = new UserContextEnricher(httpContextAccessor);

        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("UserId"));
        Assert.True(logEvent.Properties.ContainsKey("UserEmail"));
        Assert.True(logEvent.Properties.ContainsKey("UserRole"));

        var userId = logEvent.Properties["UserId"];
        var scalarUserId = Assert.IsType<ScalarValue>(userId);
        Assert.Equal("user-123", scalarUserId.Value);

        var userEmail = logEvent.Properties["UserEmail"];
        var scalarEmail = Assert.IsType<ScalarValue>(userEmail);
        Assert.Equal("test@example.com", scalarEmail.Value);

        var userRole = logEvent.Properties["UserRole"];
        var scalarRole = Assert.IsType<ScalarValue>(userRole);
        Assert.Equal("Admin", scalarRole.Value);
    }

    [Fact]
    public void UserContextEnricher_WithUnauthenticatedUser_DoesNotAddProperties()
    {
        // Arrange
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()) // No authentication
        };
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var enricher = new UserContextEnricher(httpContextAccessor);

        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        Assert.False(logEvent.Properties.ContainsKey("UserId"));
        Assert.False(logEvent.Properties.ContainsKey("UserEmail"));
        Assert.False(logEvent.Properties.ContainsKey("UserRole"));
    }

    [Fact]
    public void UserContextEnricher_WithoutHttpContext_DoesNotAddProperties()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor { HttpContext = null };
        var enricher = new UserContextEnricher(httpContextAccessor);

        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        Assert.False(logEvent.Properties.ContainsKey("UserId"));
        Assert.False(logEvent.Properties.ContainsKey("UserEmail"));
        Assert.False(logEvent.Properties.ContainsKey("UserRole"));
    }

    [Fact]
    public void EnvironmentEnricher_AddsEnvironmentName()
    {
        // Arrange
        var enricher = new EnvironmentEnricher("Production");
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("Environment"));
        var environment = logEvent.Properties["Environment"];
        var scalarValue = Assert.IsType<ScalarValue>(environment);
        Assert.Equal("Production", scalarValue.Value);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void EnvironmentEnricher_WithDifferentEnvironments_AddsCorrectValue(string environmentName)
    {
        // Arrange
        var enricher = new EnvironmentEnricher(environmentName);
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("Environment"));
        var environment = logEvent.Properties["Environment"];
        var scalarValue = Assert.IsType<ScalarValue>(environment);
        Assert.Equal(environmentName, scalarValue.Value);
    }

    [Fact]
    public void RequestContextEnricher_WithHttpContext_AddsRequestProperties()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/games";
        httpContext.Request.Method = "GET";
        httpContext.Request.Headers.UserAgent = "Mozilla/5.0 (Test)";
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var enricher = new RequestContextEnricher(httpContextAccessor);

        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("RequestPath"));
        Assert.True(logEvent.Properties.ContainsKey("RequestMethod"));
        Assert.True(logEvent.Properties.ContainsKey("RemoteIp"));
        Assert.True(logEvent.Properties.ContainsKey("UserAgent"));

        var requestPath = logEvent.Properties["RequestPath"];
        var scalarPath = Assert.IsType<ScalarValue>(requestPath);
        Assert.Equal("/api/v1/games", scalarPath.Value);

        var requestMethod = logEvent.Properties["RequestMethod"];
        var scalarMethod = Assert.IsType<ScalarValue>(requestMethod);
        Assert.Equal("GET", scalarMethod.Value);

        var remoteIp = logEvent.Properties["RemoteIp"];
        var scalarIp = Assert.IsType<ScalarValue>(remoteIp);
        Assert.Equal("192.168.1.1", scalarIp.Value);

        var userAgent = logEvent.Properties["UserAgent"];
        var scalarAgent = Assert.IsType<ScalarValue>(userAgent);
        Assert.Equal("Mozilla/5.0 (Test)", scalarAgent.Value);
    }

    [Fact]
    public void RequestContextEnricher_WithoutHttpContext_DoesNotAddProperties()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor { HttpContext = null };
        var enricher = new RequestContextEnricher(httpContextAccessor);

        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        Assert.False(logEvent.Properties.ContainsKey("RequestPath"));
        Assert.False(logEvent.Properties.ContainsKey("RequestMethod"));
        Assert.False(logEvent.Properties.ContainsKey("RemoteIp"));
        Assert.False(logEvent.Properties.ContainsKey("UserAgent"));
    }

    [Fact]
    public void RequestContextEnricher_WithMissingRemoteIp_UsesUnknown()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/test";
        httpContext.Request.Method = "POST";
        // RemoteIpAddress is null by default

        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var enricher = new RequestContextEnricher(httpContextAccessor);

        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("RemoteIp"));
        var remoteIp = logEvent.Properties["RemoteIp"];
        var scalarIp = Assert.IsType<ScalarValue>(remoteIp);
        Assert.Equal("unknown", scalarIp.Value);
    }

    [Fact]
    public void UserContextEnricher_WithPartialClaims_AddsOnlyAvailableProperties()
    {
        // Arrange - only UserId, no Email or Role
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-456")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var enricher = new UserContextEnricher(httpContextAccessor);

        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("UserId"));
        Assert.False(logEvent.Properties.ContainsKey("UserEmail"));
        Assert.False(logEvent.Properties.ContainsKey("UserRole"));
    }

    private static LogEvent CreateLogEvent()
    {
        var parser = new Serilog.Parsing.MessageTemplateParser();
        var template = parser.Parse("Test message");
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            template,
            new List<LogEventProperty>());
    }
}

/// <summary>
/// Test implementation of ILogEventPropertyFactory for unit testing enrichers.
/// </summary>
internal class TestPropertyFactory : Serilog.Core.ILogEventPropertyFactory
{
    public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
    {
        return new LogEventProperty(name, new ScalarValue(value));
    }
}

/// <summary>
/// Simple HttpContextAccessor for testing.
/// </summary>
internal class HttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
