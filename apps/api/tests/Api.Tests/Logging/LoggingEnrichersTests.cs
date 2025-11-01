using Api.Logging;
using Microsoft.AspNetCore.Http;
using Serilog.Events;
using System.Security.Claims;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Logging;

/// <summary>
/// OPS-04: Unit tests for Serilog enrichers that add correlation IDs and context to logs.
/// </summary>
public class LoggingEnrichersTests
{
    private readonly ITestOutputHelper _output;

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
        logEvent.Properties.ContainsKey("CorrelationId").Should().BeTrue();
        var correlationId = logEvent.Properties["CorrelationId"];
        var scalarValue = correlationId.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be("test-trace-id-12345");
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
        logEvent.Properties.ContainsKey("CorrelationId").Should().BeFalse();
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
        logEvent.Properties.ContainsKey("UserId").Should().BeTrue();
        logEvent.Properties.ContainsKey("UserEmail").Should().BeTrue();
        logEvent.Properties.ContainsKey("UserRole").Should().BeTrue();

        var userId = logEvent.Properties["UserId"];
        var scalarUserId = userId.Should().BeOfType<ScalarValue>().Subject;
        scalarUserId.Value.Should().Be("user-123");

        var userEmail = logEvent.Properties["UserEmail"];
        var scalarEmail = userEmail.Should().BeOfType<ScalarValue>().Subject;
        scalarEmail.Value.Should().Be("test@example.com");

        var userRole = logEvent.Properties["UserRole"];
        var scalarRole = userRole.Should().BeOfType<ScalarValue>().Subject;
        scalarRole.Value.Should().Be("Admin");
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
        logEvent.Properties.ContainsKey("UserId").Should().BeFalse();
        logEvent.Properties.ContainsKey("UserEmail").Should().BeFalse();
        logEvent.Properties.ContainsKey("UserRole").Should().BeFalse();
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
        logEvent.Properties.ContainsKey("UserId").Should().BeFalse();
        logEvent.Properties.ContainsKey("UserEmail").Should().BeFalse();
        logEvent.Properties.ContainsKey("UserRole").Should().BeFalse();
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
        logEvent.Properties.ContainsKey("Environment").Should().BeTrue();
        var environment = logEvent.Properties["Environment"];
        var scalarValue = environment.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be("Production");
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
        logEvent.Properties.ContainsKey("Environment").Should().BeTrue();
        var environment = logEvent.Properties["Environment"];
        var scalarValue = environment.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be(environmentName);
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
        logEvent.Properties.ContainsKey("RequestPath").Should().BeTrue();
        logEvent.Properties.ContainsKey("RequestMethod").Should().BeTrue();
        logEvent.Properties.ContainsKey("RemoteIp").Should().BeTrue();
        logEvent.Properties.ContainsKey("UserAgent").Should().BeTrue();

        var requestPath = logEvent.Properties["RequestPath"];
        var scalarPath = requestPath.Should().BeOfType<ScalarValue>().Subject;
        scalarPath.Value.Should().Be("/api/v1/games");

        var requestMethod = logEvent.Properties["RequestMethod"];
        var scalarMethod = requestMethod.Should().BeOfType<ScalarValue>().Subject;
        scalarMethod.Value.Should().Be("GET");

        var remoteIp = logEvent.Properties["RemoteIp"];
        var scalarIp = remoteIp.Should().BeOfType<ScalarValue>().Subject;
        scalarIp.Value.Should().Be("192.168.1.1");

        var userAgent = logEvent.Properties["UserAgent"];
        var scalarAgent = userAgent.Should().BeOfType<ScalarValue>().Subject;
        scalarAgent.Value.Should().Be("Mozilla/5.0 (Test)");
    }

    [Theory]
    [InlineData("/api/v1/games\r\n/details", "/api/v1/games/details")]
    [InlineData("/api/v1/games\r\r", "/api/v1/games")]
    [InlineData("/api/v1/games\n", "/api/v1/games")]
    public void RequestContextEnricher_WithControlCharactersInPath_SanitizesRequestPath(string requestPath, string expected)
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = requestPath;
        httpContext.Request.Method = "GET";

        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var enricher = new RequestContextEnricher(httpContextAccessor);

        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        logEvent.Properties.TryGetValue("RequestPath", out var requestPathProperty).Should().BeTrue();
        var scalarPath = requestPathProperty.Should().BeOfType<ScalarValue>().Subject;
        scalarPath.Value.Should().Be(expected);
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
        logEvent.Properties.ContainsKey("RequestPath").Should().BeFalse();
        logEvent.Properties.ContainsKey("RequestMethod").Should().BeFalse();
        logEvent.Properties.ContainsKey("RemoteIp").Should().BeFalse();
        logEvent.Properties.ContainsKey("UserAgent").Should().BeFalse();
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
        logEvent.Properties.ContainsKey("RemoteIp").Should().BeTrue();
        var remoteIp = logEvent.Properties["RemoteIp"];
        var scalarIp = remoteIp.Should().BeOfType<ScalarValue>().Subject;
        scalarIp.Value.Should().Be("unknown");
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
        logEvent.Properties.ContainsKey("UserId").Should().BeTrue();
        logEvent.Properties.ContainsKey("UserEmail").Should().BeFalse();
        logEvent.Properties.ContainsKey("UserRole").Should().BeFalse();
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
