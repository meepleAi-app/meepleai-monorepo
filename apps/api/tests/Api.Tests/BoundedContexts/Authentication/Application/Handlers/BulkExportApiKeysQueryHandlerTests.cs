using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers;

/// <summary>
/// Tests for BulkExportApiKeysQueryHandler (ISSUE-906).
/// Verifies CSV export functionality with filtering and CSV escaping.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class BulkExportApiKeysQueryHandlerTests
{
    private readonly Mock<IApiKeyRepository> _mockApiKeyRepository;
    private readonly Mock<ILogger<BulkExportApiKeysQueryHandler>> _mockLogger;
    private readonly BulkExportApiKeysQueryHandler _handler;

    public BulkExportApiKeysQueryHandlerTests()
    {
        _mockApiKeyRepository = new Mock<IApiKeyRepository>();
        _mockLogger = new Mock<ILogger<BulkExportApiKeysQueryHandler>>();
        _handler = new BulkExportApiKeysQueryHandler(_mockApiKeyRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidQuery_ReturnsAllApiKeysAsCsv()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey1, _) = ApiKey.Create(Guid.NewGuid(), userId, "Test Key 1", "read:games", DateTime.UtcNow.AddYears(1), "{\"env\":\"prod\"}");
        var (apiKey2, _) = ApiKey.Create(Guid.NewGuid(), userId, "Test Key 2", "write:games", DateTime.UtcNow.AddYears(1), null);

        _mockApiKeyRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey> { apiKey1, apiKey2 });

        var query = new BulkExportApiKeysQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length); // Header + 2 data rows
        Assert.Equal("userId,keyName,scopes,expiresAt,metadata", lines[0]);
        Assert.Contains(apiKey1.KeyName, lines[1]);
        Assert.Contains(apiKey2.KeyName, lines[2]);
    }

    [Fact]
    public async Task Handle_FilterByUserId_ReturnsFilteredKeys()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var (apiKey1, _) = ApiKey.Create(Guid.NewGuid(), userId1, "User1 Key", "read:games", DateTime.UtcNow.AddYears(1));
        var (apiKey2, _) = ApiKey.Create(Guid.NewGuid(), userId2, "User2 Key", "write:games", DateTime.UtcNow.AddYears(1));

        _mockApiKeyRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey> { apiKey1, apiKey2 });

        var query = new BulkExportApiKeysQuery(UserId: userId1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // Header + 1 data row (filtered)
        Assert.Contains("User1 Key", result);
        Assert.DoesNotContain("User2 Key", result);
    }

    [Fact]
    public async Task Handle_FilterByIsActive_ReturnsOnlyActiveKeys()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey1, _) = ApiKey.Create(Guid.NewGuid(), userId, "Active Key", "read:games", DateTime.UtcNow.AddYears(1));
        var (apiKey2, _) = ApiKey.Create(Guid.NewGuid(), userId, "Revoked Key", "write:games", DateTime.UtcNow.AddYears(1));
        apiKey2.Revoke(userId, "Test revocation");

        _mockApiKeyRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey> { apiKey1, apiKey2 });

        var query = new BulkExportApiKeysQuery(IsActive: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // Header + 1 data row (active only)
        Assert.Contains("Active Key", result);
        Assert.DoesNotContain("Revoked Key", result);
    }

    [Fact]
    public async Task Handle_SearchTerm_ReturnsMatchingKeys()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey1, _) = ApiKey.Create(Guid.NewGuid(), userId, "Production API", "read:games", DateTime.UtcNow.AddYears(1));
        var (apiKey2, _) = ApiKey.Create(Guid.NewGuid(), userId, "Development API", "write:games", DateTime.UtcNow.AddYears(1));
        var (apiKey3, _) = ApiKey.Create(Guid.NewGuid(), userId, "Test Key", "read:analytics", DateTime.UtcNow.AddYears(1));

        _mockApiKeyRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey> { apiKey1, apiKey2, apiKey3 });

        var query = new BulkExportApiKeysQuery(SearchTerm: "api");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length); // Header + 2 data rows (matching "api")
        Assert.Contains("Production API", result);
        Assert.Contains("Development API", result);
        Assert.DoesNotContain("Test Key", result);
    }

    [Fact]
    public async Task Handle_CsvEscaping_EscapesSpecialCharacters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(
            Guid.NewGuid(),
            userId,
            "Key with, comma",
            "read:games|write:games",
            DateTime.UtcNow.AddYears(1),
            "{\"description\":\"Test with \"quotes\" and, comma\"}");

        _mockApiKeyRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey> { apiKey });

        var query = new BulkExportApiKeysQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // Fields with commas or quotes should be wrapped in quotes
        Assert.Contains("\"Key with, comma\"", result);
        Assert.Contains("\"description\"\"", result); // Escaped quotes
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsHeaderOnly()
    {
        // Arrange
        _mockApiKeyRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey>());

        var query = new BulkExportApiKeysQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines); // Only header
        Assert.Equal("userId,keyName,scopes,expiresAt,metadata", lines[0]);
    }

    [Fact]
    public async Task Handle_NullExpiryAndMetadata_HandlesNullValues()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Test Key", "read:games", null, null);

        _mockApiKeyRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey> { apiKey });

        var query = new BulkExportApiKeysQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);

        // Check that null values are represented as empty strings
        var dataLine = lines[1];
        var fields = dataLine.Split(',');
        Assert.Equal(5, fields.Length);
        Assert.Empty(fields[3]); // expiresAt is null
        Assert.Empty(fields[4]); // metadata is null
    }

    [Fact]
    public async Task Handle_CombinedFilters_AppliesAllFilters()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var (apiKey1, _) = ApiKey.Create(Guid.NewGuid(), userId1, "Prod API Active", "read:games", DateTime.UtcNow.AddYears(1));
        var (apiKey2, _) = ApiKey.Create(Guid.NewGuid(), userId1, "Prod API Revoked", "write:games", DateTime.UtcNow.AddYears(1));
        apiKey2.Revoke(userId1, "Test");
        var (apiKey3, _) = ApiKey.Create(Guid.NewGuid(), userId2, "Other User API", "read:analytics", DateTime.UtcNow.AddYears(1));

        _mockApiKeyRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey> { apiKey1, apiKey2, apiKey3 });

        var query = new BulkExportApiKeysQuery(UserId: userId1, IsActive: true, SearchTerm: "prod");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // Header + 1 matching row
        Assert.Contains("Prod API Active", result);
        Assert.DoesNotContain("Prod API Revoked", result);
        Assert.DoesNotContain("Other User API", result);
    }
}
