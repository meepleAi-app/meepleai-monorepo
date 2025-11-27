using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Comprehensive tests for CreateApiKeyCommandHandler.
/// Tests API key creation, validation, security requirements, and business rules.
/// </summary>
public class CreateApiKeyCommandHandlerTests
{
    private readonly Mock<IApiKeyRepository> _apiKeyRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateApiKeyCommandHandler _handler;

    public CreateApiKeyCommandHandlerTests()
    {
        _apiKeyRepositoryMock = new Mock<IApiKeyRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _handler = new CreateApiKeyCommandHandler(
            _apiKeyRepositoryMock.Object,
            _unitOfWorkMock.Object
        );
    }

    #region Happy Path Tests

    [Fact]
    public async Task Handle_WithValidData_CreatesApiKey()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateApiKeyCommand(
            UserId: userId,
            KeyName: "Production API Key",
            Scopes: "read,write",
            ExpiresAt: DateTime.UtcNow.AddYears(1),
            Metadata: "{\"environment\":\"production\"}"
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Production API Key", result.KeyName);
        Assert.Equal("read,write", result.Scopes);
        Assert.NotNull(result.PlaintextKey);
        Assert.True(result.PlaintextKey.Length >= 40); // Base64 of 32 bytes
        Assert.NotNull(result.KeyPrefix);
        Assert.Equal(8, result.KeyPrefix.Length);
        Assert.True(result.ExpiresAt.HasValue);
        Assert.True(result.ExpiresAt.Value > DateTime.UtcNow);

        _apiKeyRepositoryMock.Verify(x => x.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutExpiration_CreatesNonExpiringKey()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateApiKeyCommand(
            UserId: userId,
            KeyName: "Non-expiring Key",
            Scopes: "read",
            ExpiresAt: null,
            Metadata: null
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.ExpiresAt);
    }

    [Fact]
    public async Task Handle_WithoutMetadata_CreatesKeySuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateApiKeyCommand(
            UserId: userId,
            KeyName: "Simple Key",
            Scopes: "read",
            ExpiresAt: null,
            Metadata: null
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Simple Key", result.KeyName);
    }

    [Fact]
    public async Task Handle_GeneratesUniqueKeys()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command1 = new CreateApiKeyCommand(userId, "Key 1", "read", null, null);
        var command2 = new CreateApiKeyCommand(userId, "Key 2", "read", null, null);

        // Act
        var result1 = await _handler.Handle(command1, TestContext.Current.CancellationToken);
        var result2 = await _handler.Handle(command2, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(result1.PlaintextKey, result2.PlaintextKey);
        Assert.NotEqual(result1.KeyPrefix, result2.KeyPrefix);
        Assert.NotEqual(result1.Id, result2.Id);
    }

    [Fact]
    public async Task Handle_PlaintextKeyStartsWithPrefix()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateApiKeyCommand(userId, "Test Key", "read", null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.StartsWith(result.KeyPrefix, result.PlaintextKey);
    }

    [Fact]
    public async Task Handle_WithComplexScopes_PreservesScopes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateApiKeyCommand(
            UserId: userId,
            KeyName: "Multi-Scope Key",
            Scopes: "read,write,delete,admin",
            ExpiresAt: null,
            Metadata: null
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("read,write,delete,admin", result.Scopes);
    }

    [Fact]
    public async Task Handle_WithMetadata_PreservesMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var metadata = "{\"env\":\"staging\",\"version\":\"2.0\"}";
        var command = new CreateApiKeyCommand(
            UserId: userId,
            KeyName: "Metadata Key",
            Scopes: "read",
            ExpiresAt: null,
            Metadata: metadata
        );

        ApiKey? capturedApiKey = null;
        _apiKeyRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((apiKey, _) => capturedApiKey = apiKey);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedApiKey);
        Assert.Equal(metadata, capturedApiKey.Metadata);
    }

    #endregion

    #region Key Name Validation Tests

    [Fact]
    public async Task Handle_WithEmptyKeyName_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateApiKeyCommand(
            UserId: userId,
            KeyName: "",
            Scopes: "read",
            ExpiresAt: null,
            Metadata: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        _apiKeyRepositoryMock.Verify(x => x.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithWhitespaceKeyName_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateApiKeyCommand(
            UserId: userId,
            KeyName: "   ",
            Scopes: "read",
            ExpiresAt: null,
            Metadata: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }

    #endregion

    #region Scopes Validation Tests

    [Fact]
    public async Task Handle_WithEmptyScopes_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateApiKeyCommand(
            UserId: userId,
            KeyName: "Test Key",
            Scopes: "",
            ExpiresAt: null,
            Metadata: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        _apiKeyRepositoryMock.Verify(x => x.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithWhitespaceScopes_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateApiKeyCommand(
            UserId: userId,
            KeyName: "Test Key",
            Scopes: "   ",
            ExpiresAt: null,
            Metadata: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }

    #endregion

    #region Expiration Tests

    [Fact]
    public async Task Handle_WithFutureExpiration_CreatesKeyCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(30);
        var command = new CreateApiKeyCommand(
            UserId: userId,
            KeyName: "Expiring Key",
            Scopes: "read",
            ExpiresAt: expiresAt,
            Metadata: null
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ExpiresAt.HasValue);
        Assert.Equal(expiresAt.Date, result.ExpiresAt.Value.Date);
    }

    [Fact]
    public async Task Handle_WithPastExpiration_CreatesKeyButExpired()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(-1);
        var command = new CreateApiKeyCommand(
            UserId: userId,
            KeyName: "Already Expired Key",
            Scopes: "read",
            ExpiresAt: expiresAt,
            Metadata: null
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ExpiresAt.HasValue);
        Assert.True(result.ExpiresAt.Value < DateTime.UtcNow);
    }

    #endregion

    #region Response DTO Tests

    [Fact]
    public async Task Handle_ResponseContainsAllRequiredFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddYears(1);
        var command = new CreateApiKeyCommand(
            UserId: userId,
            KeyName: "Full Response Key",
            Scopes: "read,write",
            ExpiresAt: expiresAt,
            Metadata: "{\"test\":\"value\"}"
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Full Response Key", result.KeyName);
        Assert.NotEmpty(result.KeyPrefix);
        Assert.NotEmpty(result.PlaintextKey);
        Assert.Equal("read,write", result.Scopes);
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
        Assert.True(result.CreatedAt >= DateTime.UtcNow.AddSeconds(-5));
        Assert.Equal(expiresAt.Date, result.ExpiresAt.Value.Date);
    }

    [Fact]
    public async Task Handle_PlaintextKeyIsBase64()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateApiKeyCommand(userId, "Test Key", "read", null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result.PlaintextKey);

        // Verify it's valid Base64
        var isValidBase64 = TryParseBase64(result.PlaintextKey);
        Assert.True(isValidBase64);
    }

    #endregion

    #region Transaction Tests

    [Fact]
    public async Task Handle_CallsSaveChangesOnce()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateApiKeyCommand(userId, "Test Key", "read", null, null);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AddsApiKeyBeforeSaveChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateApiKeyCommand(userId, "Test Key", "read", null, null);

        var callOrder = new List<string>();

        _apiKeyRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("add"));

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save"));

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, callOrder.Count);
        Assert.Equal("add", callOrder[0]);
        Assert.Equal("save", callOrder[1]);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepositories()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateApiKeyCommand(userId, "Test Key", "read", null, null);

        var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        await _handler.Handle(command, token);

        // Assert
        _apiKeyRepositoryMock.Verify(x => x.AddAsync(It.IsAny<ApiKey>(), token), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithLongKeyName_CreatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var longName = new string('A', 200);
        var command = new CreateApiKeyCommand(userId, longName, "read", null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(longName, result.KeyName);
    }

    [Fact]
    public async Task Handle_ApiKeyHasCorrectUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateApiKeyCommand(userId, "Test Key", "read", null, null);

        ApiKey? capturedApiKey = null;
        _apiKeyRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((apiKey, _) => capturedApiKey = apiKey);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedApiKey);
        Assert.Equal(userId, capturedApiKey.UserId);
    }

    #endregion

    #region Helper Methods

    private bool TryParseBase64(string value)
    {
        try
        {
            Convert.FromBase64String(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}

