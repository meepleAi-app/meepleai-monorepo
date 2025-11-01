using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Api.Middleware;
using Api.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Comprehensive tests for ApiExceptionHandlerMiddleware covering:
/// - Exception type mapping (ArgumentException, UnauthorizedAccessException, etc.)
/// - Path filtering (only /api/* paths)
/// - Development vs Production environment behavior (stack trace inclusion)
/// - Correlation ID propagation
/// - Error response structure
/// </summary>
public class ApiExceptionHandlerMiddlewareTests
{
    private readonly ITestOutputHelper _output;

    [Fact]
    public async Task InvokeAsync_WithArgumentException_Returns400BadRequest()
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new ArgumentException("Invalid parameter"),
            isDevelopment: false);

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content);

        errorResponse.Should().NotBeNull();
        errorResponse.Error.Should().Be("bad_request");
        errorResponse.Message.Should().Be("Invalid request parameters");
        errorResponse.CorrelationId.Should().NotBeNull();
        errorResponse.StackTrace.Should().BeNull(); // Production mode
    }

    [Fact]
    public async Task InvokeAsync_WithArgumentNullException_Returns400BadRequest()
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new ArgumentNullException("param"),
            isDevelopment: false);

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content);

        errorResponse.Should().NotBeNull();
        errorResponse.Error.Should().Be("bad_request");
        errorResponse.Message.Should().Be("Invalid request parameters");
    }

    [Fact]
    public async Task InvokeAsync_WithUnauthorizedAccessException_Returns403Forbidden()
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new UnauthorizedAccessException("Access denied"),
            isDevelopment: false);

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content);

        errorResponse.Should().NotBeNull();
        errorResponse.Error.Should().Be("forbidden");
        errorResponse.Message.Should().Be("Access denied");
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidOperationExceptionContainingNotFound_Returns404NotFound()
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new InvalidOperationException("Resource not found"),
            isDevelopment: false);

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content);

        errorResponse.Should().NotBeNull();
        errorResponse.Error.Should().Be("not_found");
        errorResponse.Message.Should().Be("Resource not found");
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidOperationExceptionNotContainingNotFound_Returns500InternalServerError()
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new InvalidOperationException("Something went wrong"),
            isDevelopment: false);

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content);

        errorResponse.Should().NotBeNull();
        errorResponse.Error.Should().Be("internal_server_error");
        errorResponse.Message.Should().Be("An unexpected error occurred");
    }

    [Fact]
    public async Task InvokeAsync_WithTimeoutException_Returns504GatewayTimeout()
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new TimeoutException("Operation timed out"),
            isDevelopment: false);

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content);

        errorResponse.Should().NotBeNull();
        errorResponse.Error.Should().Be("timeout");
        errorResponse.Message.Should().Be("Request timed out");
    }

    [Fact]
    public async Task InvokeAsync_WithGenericException_Returns500InternalServerError()
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new Exception("Unexpected error"),
            isDevelopment: false);

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content);

        errorResponse.Should().NotBeNull();
        errorResponse.Error.Should().Be("internal_server_error");
        errorResponse.Message.Should().Be("An unexpected error occurred");
    }

    [Fact]
    public async Task InvokeAsync_LogsSanitizedPath_WhenPathContainsControlCharacters()
    {
        // Arrange
        const string maliciousPath = "/api/attack\r\nSet-Cookie:evil";
        var logger = new TestLogger<ApiExceptionHandlerMiddleware>();
        var middleware = new ApiExceptionHandlerMiddleware(
            _ => throw new InvalidOperationException("Boom"),
            logger,
            new TestHostEnvironment());

        var context = new DefaultHttpContext
        {
            TraceIdentifier = Guid.NewGuid().ToString()
        };
        context.Request.Path = maliciousPath;
        context.Request.Method = HttpMethods.Get;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var logEntry = logger.Records.Should().ContainSingle().Which;
        logEntry.Message.Should().NotContain("\r");
        logEntry.Message.Should().NotContain("\n");

        var sanitizedPath = maliciousPath.Replace("\r", string.Empty).Replace("\n", string.Empty);
        logEntry.Message.Should().Contain(sanitizedPath);
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task InvokeAsync_InDevelopmentMode_IncludesStackTrace()
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new InvalidOperationException("Test error"),
            isDevelopment: true); // Development mode

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content);

        errorResponse.Should().NotBeNull();
        errorResponse.StackTrace.Should().NotBeNull(); // Stack trace included in development
        errorResponse.StackTrace.Should().NotBeEmpty(); // Verify stack trace is present
    }

    [Fact]
    public async Task InvokeAsync_InProductionMode_ExcludesStackTrace()
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new InvalidOperationException("Test error"),
            isDevelopment: false); // Production mode

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content);

        errorResponse.Should().NotBeNull();
        errorResponse.StackTrace.Should().BeNull(); // No stack trace in production
    }

    [Fact]
    public async Task InvokeAsync_WithNonApiPath_DoesNotHandleException()
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new InvalidOperationException("Test error"),
            isDevelopment: false);

        // Act & Assert
        // Non-/api/* paths should throw the exception instead of handling it
        // The test framework will catch the exception thrown by the endpoint
        var act = async () => await client.GetAsync("/health");
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Be("Test error");
    }

    [Fact]
    public async Task InvokeAsync_WithSuccessfulRequest_PassesThrough()
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: null, // No exception
            isDevelopment: false);

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Success");
    }

    [Fact]
    public async Task InvokeAsync_SetsCorrelationIdFromTraceIdentifier()
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new Exception("Test error"),
            isDevelopment: false);

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content);

        errorResponse.Should().NotBeNull();
        errorResponse.CorrelationId.Should().NotBeNull();
        errorResponse.CorrelationId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_SetsTimestampToUtcNow()
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new Exception("Test error"),
            isDevelopment: false);

        var beforeRequest = DateTime.UtcNow;

        // Act
        var response = await client.GetAsync("/api/test");

        var afterRequest = DateTime.UtcNow;

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content);

        errorResponse.Should().NotBeNull();
        errorResponse.Timestamp.Should().BeCloseTo(beforeRequest.AddSeconds(-1), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InvokeAsync_SetsContentTypeToApplicationJson()
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new Exception("Test error"),
            isDevelopment: false);

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Theory]
    [InlineData("/api/v1/games")]
    [InlineData("/api/test")]
    [InlineData("/api/users/123")]
    [InlineData("/api/admin/logs")]
    public async Task InvokeAsync_WithVariousApiPaths_HandlesExceptions(string path)
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new Exception("Test error"),
            isDevelopment: false);

        // Act
        var response = await client.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content);

        errorResponse.Should().NotBeNull();
        errorResponse.Error.Should().Be("internal_server_error");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/")]
    [InlineData("/swagger")]
    [InlineData("/metrics")]
    public async Task InvokeAsync_WithNonApiPaths_DoesNotHandleExceptions(string path)
    {
        // Arrange
        var (client, _) = CreateTestServer(
            throwException: new InvalidOperationException("Test error"),
            isDevelopment: false);

        // Act & Assert
        var act = async () => await client.GetAsync(path);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Be("Test error");
    }

    [Fact]
    public async Task InvokeAsync_LogsSanitizedPathForExceptions()
    {
        // Arrange
        var logger = new TestLogger<ApiExceptionHandlerMiddleware>();
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.IsDevelopment()).Returns(false);

        RequestDelegate throwingNext = _ => throw new InvalidOperationException("Boom");
        var middleware = new ApiExceptionHandlerMiddleware(throwingNext, logger, environment.Object);

        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            TraceIdentifier = "trace-123"
        };
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/unsafe\r\npath";
        context.Request.Method = "GET";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var entry = logger.Records.Where(e => e.Level == LogLevel.Error).Should().ContainSingle().Subject;
        var sanitizedPath = entry.GetStateValue("Path");

        sanitizedPath.Should().Be("/api/unsafepath");
    }

    /// <summary>
    /// Creates a test server with ApiExceptionHandlerMiddleware configured
    /// </summary>
    private (HttpClient Client, TestServer Server) CreateTestServer(
        Exception? throwException,
        bool isDevelopment)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddLogging();
                });
                webHost.Configure(app =>
                {
                    // Set environment
                    var env = app.ApplicationServices.GetRequiredService<IHostEnvironment>();
                    if (isDevelopment)
                    {
                        typeof(IHostEnvironment).GetProperty("EnvironmentName")
                            ?.SetValue(env, Environments.Development);
                    }
                    else
                    {
                        typeof(IHostEnvironment).GetProperty("EnvironmentName")
                            ?.SetValue(env, Environments.Production);
                    }

                    // Add middleware
                    app.UseApiExceptionHandler();

                    // Add test endpoints
                    app.Run(async context =>
                    {
                        if (throwException != null)
                        {
                            throw throwException;
                        }

                        context.Response.StatusCode = 200;
                        await context.Response.WriteAsync("Success");
                    });
                });
            });

        var host = hostBuilder.Start();
        var server = host.GetTestServer();
        var client = server.CreateClient();

        return (client, server);
    }

    /// <summary>
    /// DTO for deserializing error responses (camelCase from JSON)
    /// </summary>
    private class ErrorResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("correlationId")]
        public string CorrelationId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("stackTrace")]
        public string? StackTrace { get; set; }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = nameof(ApiExceptionHandlerMiddlewareTests);

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        private readonly List<LogEntry> _entries = new();

        public IReadOnlyList<LogEntry> Records => _entries;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var stateDict = state as IEnumerable<KeyValuePair<string, object?>>;
            _entries.Add(new LogEntry(logLevel, eventId, message, exception, stateDict?.ToDictionary(x => x.Key, x => x.Value)));
        }

        internal sealed record LogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception, IReadOnlyDictionary<string, object?>? State = null)
        {
            public object? GetStateValue(string key) => State?.TryGetValue(key, out var value) == true ? value : null;
        };
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}