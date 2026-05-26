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
using FluentAssertions;
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
        _httpContext.Response.StatusCode.Should().Be(200);
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
        _httpContext.Response.StatusCode.Should().Be(400);
        _httpContext.Response.ContentType.Should().StartWithEquivalentOf("application/json");

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        var errorResponse = JsonSerializer.Deserialize<JsonDocument>(responseBody);

        errorResponse.Should().NotBeNull();
        errorResponse.RootElement.GetProperty("error").GetString().Should().Be("bad_request");
        errorResponse.RootElement.GetProperty("message").GetString().Should().Be("Invalid input");
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
        _httpContext.Response.StatusCode.Should().Be(404);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        using var errorResponse = ParseErrorResponse(responseBody);

        errorResponse.RootElement.GetProperty("error").GetString().Should().Be("not_found");
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
        _httpContext.Response.StatusCode.Should().Be(401);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        using var errorResponse = ParseErrorResponse(responseBody);

        errorResponse.RootElement.GetProperty("error").GetString().Should().Be("unauthorized");
        errorResponse.RootElement.GetProperty("message").GetString().Should().Be("Token expired");
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
        _httpContext.Response.StatusCode.Should().Be(403);
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
        _httpContext.Response.StatusCode.Should().Be(409);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        using var errorResponse = ParseErrorResponse(responseBody);

        errorResponse.RootElement.GetProperty("error").GetString().Should().Be("conflict");
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
        _httpContext.Response.StatusCode.Should().Be(400);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        using var errorResponse = ParseErrorResponse(responseBody);

        errorResponse.RootElement.GetProperty("error").GetString().Should().Be("domain_error");
    }

    [Fact]
    public async Task InvokeAsync_DomainException_PropagatesMessage()
    {
        // Arrange
        var exception = new DomainException("Invalid email or password");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(400);
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        using var errorResponse = ParseErrorResponse(responseBody);

        errorResponse.RootElement.GetProperty("error").GetString().Should().Be("domain_error");
        errorResponse.RootElement.GetProperty("message").GetString().Should().Be("Invalid email or password");
    }

    [Theory]
    [InlineData("Invalid email or password")]
    [InlineData("Account is locked. Please try again in 3 minute(s).")]
    [InlineData("Account has been locked due to too many failed login attempts. Please try again in 15 minutes.")]
    [InlineData("Account is suspended")]
    public async Task InvokeAsync_DomainException_PropagatesAllLoginMessages(string loginErrorMessage)
    {
        // Arrange
        var exception = new DomainException(loginErrorMessage);
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        using var errorResponse = ParseErrorResponse(responseBody);

        _httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        errorResponse.RootElement.GetProperty("message").GetString().Should().Be(loginErrorMessage);
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
        _httpContext.Response.StatusCode.Should().Be(400);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        using var errorResponse = ParseErrorResponse(responseBody);

        errorResponse.RootElement.GetProperty("error").GetString().Should().Be("validation_error");
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
        _httpContext.Response.StatusCode.Should().Be(400);
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
        _httpContext.Response.StatusCode.Should().Be(403);
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
        _httpContext.Response.StatusCode.Should().Be(404);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        using var errorResponse = ParseErrorResponse(responseBody);

        errorResponse.RootElement.GetProperty("error").GetString().Should().Be("not_found");
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
        _httpContext.Response.StatusCode.Should().Be(504);
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
        _httpContext.Response.StatusCode.Should().Be(500);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        using var errorResponse = ParseErrorResponse(responseBody);

        errorResponse.RootElement.GetProperty("error").GetString().Should().Be("internal_server_error");
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
        await ((Func<Task>)(() => middleware.InvokeAsync(_httpContext))).Should().ThrowAsync<InvalidOperationException>();
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
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        var errorResponse = JsonSerializer.Deserialize<JsonDocument>(responseBody);

        errorResponse!.RootElement.GetProperty("correlationId").GetString().Should().Be("test-trace-id");
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
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        var errorResponse = JsonSerializer.Deserialize<JsonDocument>(responseBody);

        errorResponse!.RootElement.TryGetProperty("stackTrace", out var stackTrace).Should().BeTrue();
        stackTrace.GetString().Should().NotBeNull();
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
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        var errorResponse = JsonSerializer.Deserialize<JsonDocument>(responseBody);

        // In production, stackTrace should be null or not present
        if (errorResponse!.RootElement.TryGetProperty("stackTrace", out var stackTrace))
        {
            stackTrace.ValueKind.Should().Be(JsonValueKind.Null);
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
        _httpContext.Response.StatusCode.Should().Be(expectedStatusCode);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        var errorResponse = JsonSerializer.Deserialize<JsonDocument>(responseBody);

        errorResponse!.RootElement.GetProperty("error").GetString().Should().Be(expectedErrorCode);
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
        _httpContext.Response.StatusCode.Should().Be(expectedStatusCode);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        var errorResponse = JsonSerializer.Deserialize<JsonDocument>(responseBody);

        errorResponse!.RootElement.GetProperty("error").GetString().Should().Be(expectedErrorCode);
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
        _httpContext.Response.StatusCode.Should().Be(expectedStatusCode);
    }

    [Fact]
    public async Task InvokeAsync_TwoFactorRequiredException_StepUp_Returns401WithSubcodeAndChallenge()
    {
        // SP5 S3 — D-S3-2: strict TwoFactorEnforcementBehavior throws when TOTP recency is
        // missing/stale. The middleware maps to 401 + structured body + WWW-Authenticate header.
        var exception = new TwoFactorRequiredException(
            TwoFactorRequiredSubcode.StepUpRequired,
            "TOTP verification required for this command.");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(_httpContext);

        _httpContext.Response.StatusCode.Should().Be(401);
        _httpContext.Response.Headers["WWW-Authenticate"].ToString()
            .Should().Be("TOTP-StepUp realm=\"meepleai-admin\"",
                "RFC 7235 challenge header lets non-browser clients distinguish from session-invalid 401");

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        using var errorResponse = ParseErrorResponse(responseBody);

        errorResponse.RootElement.GetProperty("error").GetString().Should().Be("two_factor_required");
        errorResponse.RootElement.GetProperty("subcode").GetString().Should().Be("step_up_required",
            "FE routes on subcode: step_up_required opens the TOTP modal");
        errorResponse.RootElement.GetProperty("message").GetString().Should().Contain("TOTP");
    }

    [Fact]
    public async Task InvokeAsync_TwoFactorRequiredException_EnrollRequired_Returns401WithEnrollSubcode()
    {
        var exception = new TwoFactorRequiredException(
            TwoFactorRequiredSubcode.EnrollRequired,
            "Two-factor authentication must be enabled for this action.");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(_httpContext);

        _httpContext.Response.StatusCode.Should().Be(401);
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        using var errorResponse = ParseErrorResponse(responseBody);

        errorResponse.RootElement.GetProperty("subcode").GetString().Should().Be("enroll_required",
            "FE routes on subcode: enroll_required redirects to /settings/2fa/enroll");
    }

    [Fact]
    public async Task InvokeAsync_TwoFactorRequiredException_LockedOut_Returns401WithRetryAfter()
    {
        var exception = new TwoFactorRequiredException(
            TwoFactorRequiredSubcode.LockedOut,
            "Account locked due to excessive failed step-up attempts.",
            retryAfterSeconds: 900);
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(_httpContext);

        _httpContext.Response.StatusCode.Should().Be(401);
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        using var errorResponse = ParseErrorResponse(responseBody);

        errorResponse.RootElement.GetProperty("subcode").GetString().Should().Be("locked_out");
        errorResponse.RootElement.GetProperty("retryAfterSeconds").GetInt32().Should().Be(900,
            "the FE shows a retry-after toast with the wait duration");
    }

    [Fact]
    public async Task InvokeAsync_TwoFactorUnavailableException_Returns503()
    {
        // SP5 S3 — D-S3-4 (Option B): when the TOTP store / rate-limit backend is unreachable, the
        // step-up cannot complete; the middleware maps it to 503 (transient) rather than a generic
        // 500, so the FE shows a retryable error toast instead of a failed-code / step-up signal.
        var exception = new TwoFactorUnavailableException("Two-factor service is temporarily unavailable.");
        var middleware = new ApiExceptionHandlerMiddleware(
            next: (context) => throw exception,
            _loggerMock.Object,
            _environmentMock.Object);

        _httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(_httpContext);

        _httpContext.Response.StatusCode.Should().Be(503);

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
        using var errorResponse = ParseErrorResponse(responseBody);

        errorResponse.RootElement.GetProperty("error").GetString().Should().Be("two_factor_unavailable");
    }

    private static JsonDocument ParseErrorResponse(string responseBody)
    {
        return JsonDocument.Parse(responseBody);
    }
}

