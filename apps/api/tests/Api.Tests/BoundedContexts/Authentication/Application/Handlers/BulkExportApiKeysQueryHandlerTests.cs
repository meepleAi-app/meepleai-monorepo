using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
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
        result.Should().NotBeNull();
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(3); // Header + 2 data rows
        lines[0].Should().Be("userId,keyName,scopes,expiresAt,metadata");
        lines[1].Should().Contain(apiKey1.KeyName);
        lines[2].Should().Contain(apiKey2.KeyName);
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
        result.Should().NotBeNull();
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(2); // Header + 1 data row (filtered)
        result.Should().Contain("User1 Key");
        result.Should().NotContain("User2 Key");
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
        result.Should().NotBeNull();
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(2); // Header + 1 data row (active only)
        result.Should().Contain("Active Key");
        result.Should().NotContain("Revoked Key");
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
        result.Should().NotBeNull();
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(3); // Header + 2 data rows (matching "api")
        result.Should().Contain("Production API");
        result.Should().Contain("Development API");
        result.Should().NotContain("Test Key");
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
        result.Should().NotBeNull();
        // Fields with commas or quotes should be wrapped in quotes
        result.Should().Contain("\"Key with, comma\"");
        result.Should().Contain("\"description\"\""); // Escaped quotes
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
        result.Should().NotBeNull();
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().ContainSingle(); // Only header
        lines[0].Should().Be("userId,keyName,scopes,expiresAt,metadata");
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
        result.Should().NotBeNull();
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(2);

        // Check that null values are represented as empty strings
        var dataLine = lines[1];
        var fields = dataLine.Split(',');
        fields.Length.Should().Be(5);
        fields[3].Should().BeEmpty(); // expiresAt is null
        fields[4].Should().BeEmpty(); // metadata is null
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
        result.Should().NotBeNull();
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(2); // Header + 1 matching row
        result.Should().Contain("Prod API Active");
        result.Should().NotContain("Prod API Revoked");
        result.Should().NotContain("Other User API");
    }
}