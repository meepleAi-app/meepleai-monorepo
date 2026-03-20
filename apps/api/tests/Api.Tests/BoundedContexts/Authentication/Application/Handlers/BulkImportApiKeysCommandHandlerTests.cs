using Api.BoundedContexts.Authentication.Application.Commands.ApiKeys;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
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
        result.TotalRequested.Should().Be(2);
        result.SuccessCount.Should().Be(2);
        result.FailedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
        result.Data.Count.Should().Be(2);

        // Verify plaintext keys are returned
        result.Data.Should().OnlyContain(dto => !string.IsNullOrWhiteSpace(dto.PlaintextKey));

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
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Handle_InvalidHeader_ThrowsDomainException()
    {
        // Arrange
        var csvContent = @"userId,keyName,invalid,header,test
guid1,Test Key,read:games,2026-12-31 23:59:59,null";

        var command = new BulkImportApiKeysCommand(csvContent, Guid.NewGuid());

        // Act & Assert
        var act2 = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act2.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("Invalid CSV header");
    }

    [Fact]
    public async Task Handle_ExceedsMaxSize_ThrowsDomainException()
    {
        // Arrange
        var largeContent = new string('x', 11 * 1024 * 1024); // 11MB
        var command = new BulkImportApiKeysCommand(largeContent, Guid.NewGuid());

        // Act & Assert
        var act3 = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act3.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("exceeds maximum limit");
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
        var act4 = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act4.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("exceeds maximum limit of 1000");
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
        var act5 = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act5.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("user IDs do not exist");
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
        var act6 = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act6.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("duplicate key names");
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
        var act7 = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act7.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("already exist");
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
        result.TotalRequested.Should().Be(2);
        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("Invalid user ID format");
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
        result.TotalRequested.Should().Be(1);
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("Key name is required");
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
        result.TotalRequested.Should().Be(1);
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("Scopes are required");
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
        result.TotalRequested.Should().Be(1);
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("must be in the future");
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
        result.TotalRequested.Should().Be(1);
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("Invalid expiry date format");
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
        result.TotalRequested.Should().Be(1);
        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
        result.Data[0].ExpiresAt.Should().BeNull();
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
        result.TotalRequested.Should().Be(1);
        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(0);
        result.Data[0].KeyName.Should().Be("Key with, comma");
        result.Data[0].Scopes.Should().Be("read:games|write:games");
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
        result.TotalRequested.Should().Be(2);
        result.SuccessCount.Should().Be(2);
        result.FailedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
        result.Data.Count.Should().Be(2);

        // Verify different user IDs
        result.Data.Should().Contain(dto => dto.UserId == userId1);
        result.Data.Should().Contain(dto => dto.UserId == userId2);
    }
}
