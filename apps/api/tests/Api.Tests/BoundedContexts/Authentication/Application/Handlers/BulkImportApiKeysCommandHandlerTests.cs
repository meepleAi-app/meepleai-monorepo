using Api.BoundedContexts.Authentication.Application.Commands.ApiKeys;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers;

/// <summary>
/// Tests for BulkImportApiKeysCommandHandler (ISSUE-906).
/// Verifies CSV import functionality with validation and key generation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class BulkImportApiKeysCommandHandlerTests
{
    private readonly Mock<IApiKeyRepository> _mockApiKeyRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<BulkImportApiKeysCommandHandler>> _mockLogger;
    private readonly BulkImportApiKeysCommandHandler _handler;

    public BulkImportApiKeysCommandHandlerTests()
    {
        _mockApiKeyRepository = new Mock<IApiKeyRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<BulkImportApiKeysCommandHandler>>();
        _handler = new BulkImportApiKeysCommandHandler(
            _mockApiKeyRepository.Object,
            _mockUserRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidCsv_ImportsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd HH:mm:ss");

        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{userId},Test Key 1,read:games,{expiresAt},{{""env"":""prod""}}
{userId},Test Key 2,write:games,{expiresAt},null";

        _mockUserRepository
            .Setup(r => r.ExistsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockApiKeyRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey>());

        var command = new BulkImportApiKeysCommand(csvContent, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalRequested);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Data.Count);

        // Verify plaintext keys are returned
        Assert.All(result.Data, dto => Assert.False(string.IsNullOrWhiteSpace(dto.PlaintextKey)));

        // Verify repository was called
        _mockApiKeyRepository.Verify(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyCsv_ThrowsDomainException()
    {
        // Arrange
        var command = new BulkImportApiKeysCommand(string.Empty, Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InvalidHeader_ThrowsDomainException()
    {
        // Arrange
        var csvContent = @"userId,keyName,invalid,header,test
guid1,Test Key,read:games,2026-12-31 23:59:59,null";

        var command = new BulkImportApiKeysCommand(csvContent, Guid.NewGuid());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Invalid CSV header", exception.Message);
    }

    [Fact]
    public async Task Handle_ExceedsMaxSize_ThrowsDomainException()
    {
        // Arrange
        var largeContent = new string('x', 11 * 1024 * 1024); // 11MB
        var command = new BulkImportApiKeysCommand(largeContent, Guid.NewGuid());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("exceeds maximum limit", exception.Message);
    }

    [Fact]
    public async Task Handle_ExceedsMaxBulkSize_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lines = new List<string> { "userId,keyName,scopes,expiresAt,metadata" };

        // Generate 1001 rows
        for (int i = 0; i < 1001; i++)
        {
            lines.Add($"{userId},Key {i},read:games,2026-12-31 23:59:59,null");
        }

        var csvContent = string.Join("\n", lines);
        var command = new BulkImportApiKeysCommand(csvContent, Guid.NewGuid());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("exceeds maximum limit of 1000", exception.Message);
    }

    [Fact]
    public async Task Handle_NonExistentUserId_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{userId},Test Key,read:games,2026-12-31 23:59:59,null";

        _mockUserRepository
            .Setup(r => r.ExistsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new BulkImportApiKeysCommand(csvContent, Guid.NewGuid());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("user IDs do not exist", exception.Message);
    }

    [Fact]
    public async Task Handle_DuplicateKeyNamesInCsv_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{userId},Duplicate Key,read:games,2026-12-31 23:59:59,null
{userId},Duplicate Key,write:games,2026-12-31 23:59:59,null";

        _mockUserRepository
            .Setup(r => r.ExistsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new BulkImportApiKeysCommand(csvContent, Guid.NewGuid());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("duplicate key names", exception.Message);
    }

    [Fact]
    public async Task Handle_ExistingKeyNameInDatabase_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{userId},Existing Key,read:games,2026-12-31 23:59:59,null";

        _mockUserRepository
            .Setup(r => r.ExistsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var (existingKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Existing Key", "read:games", DateTime.UtcNow.AddYears(1));

        _mockApiKeyRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey> { existingKey });

        var command = new BulkImportApiKeysCommand(csvContent, Guid.NewGuid());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("already exist", exception.Message);
    }

    [Fact]
    public async Task Handle_InvalidUserId_SkipsRowWithError()
    {
        // Arrange
        var validUserId = Guid.NewGuid();
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
invalid-guid,Invalid Key,read:games,2026-12-31 23:59:59,null
{validUserId},Valid Key,write:games,2026-12-31 23:59:59,null";

        _mockUserRepository
            .Setup(r => r.ExistsAsync(validUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockApiKeyRepository
            .Setup(r => r.GetByUserIdAsync(validUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey>());

        var command = new BulkImportApiKeysCommand(csvContent, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalRequested);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Invalid user ID format", result.Errors[0]);
    }

    [Fact]
    public async Task Handle_MissingKeyName_SkipsRowWithError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{userId},,read:games,2026-12-31 23:59:59,null";

        _mockUserRepository
            .Setup(r => r.ExistsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new BulkImportApiKeysCommand(csvContent, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalRequested);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Key name is required", result.Errors[0]);
    }

    [Fact]
    public async Task Handle_MissingScopes_SkipsRowWithError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{userId},Test Key,,2026-12-31 23:59:59,null";

        _mockUserRepository
            .Setup(r => r.ExistsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new BulkImportApiKeysCommand(csvContent, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalRequested);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Scopes are required", result.Errors[0]);
    }

    [Fact]
    public async Task Handle_PastExpiryDate_SkipsRowWithError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pastDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss");
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{userId},Test Key,read:games,{pastDate},null";

        _mockUserRepository
            .Setup(r => r.ExistsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new BulkImportApiKeysCommand(csvContent, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalRequested);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Single(result.Errors);
        Assert.Contains("must be in the future", result.Errors[0]);
    }

    [Fact]
    public async Task Handle_InvalidDateFormat_SkipsRowWithError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{userId},Test Key,read:games,31/12/2026,null";

        _mockUserRepository
            .Setup(r => r.ExistsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new BulkImportApiKeysCommand(csvContent, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalRequested);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Invalid expiry date format", result.Errors[0]);
    }

    [Fact]
    public async Task Handle_NullExpiryDate_CreatesKeyWithoutExpiry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{userId},Never Expire,read:games,,null";

        _mockUserRepository
            .Setup(r => r.ExistsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockApiKeyRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey>());

        var command = new BulkImportApiKeysCommand(csvContent, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalRequested);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(result.Errors);
        Assert.Null(result.Data[0].ExpiresAt);
    }

    [Fact]
    public async Task Handle_CsvWithQuotedFields_ParsesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd HH:mm:ss");
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{userId},""Key with, comma"",""read:games|write:games"",{expiresAt},""{{""""env"""":""""prod""""""}}""";

        _mockUserRepository
            .Setup(r => r.ExistsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockApiKeyRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey>());

        var command = new BulkImportApiKeysCommand(csvContent, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalRequested);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal("Key with, comma", result.Data[0].KeyName);
        Assert.Equal("read:games|write:games", result.Data[0].Scopes);
    }

    [Fact]
    public async Task Handle_MultipleUsers_ImportsForAllUsers()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd HH:mm:ss");

        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{userId1},User1 Key,read:games,{expiresAt},null
{userId2},User2 Key,write:games,{expiresAt},null";

        _mockUserRepository
            .Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockApiKeyRepository
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey>());

        var command = new BulkImportApiKeysCommand(csvContent, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalRequested);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Data.Count);

        // Verify different user IDs
        Assert.Contains(result.Data, dto => dto.UserId == userId1);
        Assert.Contains(result.Data, dto => dto.UserId == userId2);
    }
}
