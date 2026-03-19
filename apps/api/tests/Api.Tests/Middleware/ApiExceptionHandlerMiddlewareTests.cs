using Api.Middleware;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.SharedKernel.Domain.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using System.Threading;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Middleware;

/// <summary>
/// Tests for ApiExceptionHandlerMiddleware (Issue #1194).
/// Validates centralized error handling for all exception types.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ApiExceptionHandlerMiddlewareTests
{
    private readonly Mock<ILogger<ApiExceptionHandlerMiddleware>> _loggerMock;
    private readonly Mock<IHostEnvironment> _environmentMock;
    private readonly DefaultHttpContext _httpContext;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ApiExceptionHandlerMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ApiExceptionHandlerMiddleware>>();
        _environmentMock = new Mock<IHostEnvironment>();
        _httpContext = new DefaultHttpContext();
        _httpContext.Request.Path = "/api/v1/test";
        _httpContext.Request.Method = "GET";
    }

    [Fact]
    public async Task InvokeAsync_NoException_PassesThrough()
    {
        // Arrange
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => Task.CompletedTask,
            _loggerMock.Object,
            _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(200, _httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_HttpException_ReturnsCorrectStatusAndError()
    {
        // Arrange
        var exception = new BadRequestException("Invalid input");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(400, _httpContext.Response.StatusCode);
        Assert.StartsWith("application/json", _httpContext.Response.ContentType, StringComparison.OrdinalIgnoreCase);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        var errorResponse = JsonSerializer.Deserialize<JsonDocument>(responseBody);

        Assert.NotNull(errorResponse);
        Assert.Equal("bad_request", errorResponse.RootElement.GetProperty("error").GetString());
        Assert.Equal("Invalid input", errorResponse.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task InvokeAsync_NotFoundException_Returns404()
    {
        // Arrange
        var exception = new NotFoundException("Game", "abc-123");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(404, _httpContext.Response.StatusCode);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        using var errorResponse = ParseErrorResponse(responseBody);

        Assert.Equal("not_found", errorResponse.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedHttpException_Returns401()
    {
        // Arrange
        var exception = new UnauthorizedHttpException("Token expired");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(401, _httpContext.Response.StatusCode);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);

        Assert.Equal("unauthorized", errorResponse.RootElement.GetProperty("error").GetString());
        Assert.Equal("Token expired", errorResponse.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task InvokeAsync_ForbiddenException_Returns403()
    {
        // Arrange
        var exception = new ForbiddenException("Insufficient permissions");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(403, _httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ConflictException_Returns409()
    {
        // Arrange
        var exception = new ConflictException("Resource already exists");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(409, _httpContext.Response.StatusCode);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);

        Assert.Equal("conflict", errorResponse.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task InvokeAsync_DomainException_Returns400()
    {
        // Arrange
        var exception = new DomainException("Invalid domain operation");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(400, _httpContext.Response.StatusCode);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);

        Assert.Equal("domain_error", errorResponse.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task InvokeAsync_ValidationException_Returns400WithValidationError()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            ["email"] = new[] { "Email is required", "Email format invalid" }
        };
        var exception = new ValidationException("Validation failed", errors);
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(400, _httpContext.Response.StatusCode);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);

        Assert.Equal("validation_error", errorResponse.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_Returns400()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(400, _httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_Returns403()
    {
        // Arrange
        var exception = new UnauthorizedAccessException("Access denied");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(403, _httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_KeyNotFoundException_Returns404()
    {
        // Arrange
        var exception = new KeyNotFoundException("Key not found");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(404, _httpContext.Response.StatusCode);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);

        Assert.Equal("not_found", errorResponse.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task InvokeAsync_TimeoutException_Returns504()
    {
        // Arrange
        var exception = new TimeoutException("Request timed out");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(504, _httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UnknownException_Returns500()
    {
        // Arrange
        var exception = new InvalidCastException("Unexpected error");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(500, _httpContext.Response.StatusCode);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);

        Assert.Equal("internal_server_error", errorResponse.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task InvokeAsync_NonApiPath_RethrowsException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Request.Path = "/non-api/path";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(_httpContext));
    }

    [Fact]
    public async Task InvokeAsync_IncludesCorrelationId()
    {
        // Arrange
        var exception = new BadRequestException("Test error");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();
        _httpContext.TraceIdentifier = "test-trace-id";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        var errorResponse = JsonSerializer.Deserialize<JsonDocument>(responseBody);

        Assert.Equal("test-trace-id", errorResponse!.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task InvokeAsync_DevelopmentEnvironment_IncludesStackTrace()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var exception = new InvalidOperationException("Test error");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        var errorResponse = JsonSerializer.Deserialize<JsonDocument>(responseBody);

        Assert.True(errorResponse!.RootElement.TryGetProperty("stackTrace", out var stackTrace));
        Assert.NotNull(stackTrace.GetString());
    }

    [Fact]
    public async Task InvokeAsync_ProductionEnvironment_ExcludesStackTrace()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var exception = new InvalidOperationException("Test error");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        var errorResponse = JsonSerializer.Deserialize<JsonDocument>(responseBody);

        // In production, stackTrace should be null or not present
        if (errorResponse!.RootElement.TryGetProperty("stackTrace", out var stackTrace))
        {
            Assert.Equal(JsonValueKind.Null, stackTrace.ValueKind);
        }
    }

    [Fact]
    public async Task InvokeAsync_LogsException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Theory]
    [InlineData(typeof(BadRequestException), 400, "bad_request")]
    [InlineData(typeof(NotFoundException), 404, "not_found")]
    [InlineData(typeof(UnauthorizedHttpException), 401, "unauthorized")]
    [InlineData(typeof(ForbiddenException), 403, "forbidden")]
    [InlineData(typeof(ConflictException), 409, "conflict")]
    public async Task InvokeAsync_CustomHttpExceptions_ReturnCorrectCodes(
        Type exceptionType,
        int expectedStatusCode,
        string expectedErrorCode)
    {
        // Arrange
        var exception = exceptionType == typeof(NotFoundException)
            ? new NotFoundException("Resource", "123")
            : (Exception)Activator.CreateInstance(exceptionType, "Test message")!;

        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(expectedStatusCode, _httpContext.Response.StatusCode);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        var errorResponse = JsonSerializer.Deserialize<JsonDocument>(responseBody);

        Assert.Equal(expectedErrorCode, errorResponse!.RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData(typeof(DomainException), 400, "domain_error")]
    [InlineData(typeof(ValidationException), 400, "validation_error")]
    public async Task InvokeAsync_DomainExceptions_ReturnCorrectCodes(
        Type exceptionType,
        int expectedStatusCode,
        string expectedErrorCode)
    {
        // Arrange
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Test message")!;
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(expectedStatusCode, _httpContext.Response.StatusCode);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        var errorResponse = JsonSerializer.Deserialize<JsonDocument>(responseBody);

        Assert.Equal(expectedErrorCode, errorResponse!.RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData(typeof(ArgumentException), 400)]
    [InlineData(typeof(ArgumentNullException), 400)]
    [InlineData(typeof(UnauthorizedAccessException), 403)]
    [InlineData(typeof(KeyNotFoundException), 404)]
    [InlineData(typeof(TimeoutException), 504)]
    public async Task InvokeAsync_SystemExceptions_ReturnCorrectCodes(
        Type exceptionType,
        int expectedStatusCode)
    {
        // Arrange
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Test message")!;
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(expectedStatusCode, _httpContext.Response.StatusCode);
    }

    private static JsonDocument ParseErrorResponse(string responseBody)
    {
        return JsonDocument.Parse(responseBody);
    }
}

