using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// Integration tests for DocumentCollectionRepository.
/// Tests CRUD operations and query methods with real PostgreSQL database.
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// Issue #2051: Multi-document collection persistence
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Issue", "2031")]
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "2051")]
public sealed class DocumentCollectionRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IDocumentCollectionRepository? _repository;
    private IUnitOfWork? _unitOfWork;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private static readonly Guid TestUserId = new("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TestGameId1 = new("20000000-0000-0000-0000-000000000001");
    private static readonly Guid TestGameId2 = new("20000000-0000-0000-0000-000000000002");
    private static readonly Guid TestPdfId1 = new("30000000-0000-0000-0000-000000000001");
    private static readonly Guid TestPdfId2 = new("30000000-0000-0000-0000-000000000002");

    public DocumentCollectionRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #2031: Migrated to SharedTestcontainersFixture for Docker hijack prevention and performance
        _databaseName = $"test_docrepo_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IDocumentCollectionRepository, DocumentCollectionRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // MediatR (required by MeepleAiDbContext)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        var serviceProvider = services.BuildServiceProvider();
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = serviceProvider.GetRequiredService<IDocumentCollectionRepository>();
        _unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        // Create database
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }

        // Seed test data
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        // Issue #2031: Use SharedTestcontainersFixture for cleanup
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region CRUD Tests

    [Fact]
    public async Task AddAsync_ValidCollection_PersistsToDatabase()
    {
        // Arrange
        var collection = new DocumentCollection(
            Guid.NewGuid(),
            TestGameId1,
            new CollectionName("Test Collection"),
            TestUserId,
            "Test description");

        // Act
        await _repository!.AddAsync(collection, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _repository.GetByIdAsync(collection.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(collection.Id);
        retrieved.Name.Value.Should().Be("Test Collection");
        retrieved.Description.Should().Be("Test description");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingCollection_ReturnsCollection()
    {
        // Arrange
        var collectionId = new Guid("11111111-1111-1111-1111-111111111111");

        // Act
        var result = await _repository!.GetByIdAsync(collectionId, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(collectionId);
        result.GameId.Should().Be(TestGameId1);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentCollection_ReturnsNull()
    {
        // Act
        var result = await _repository!.GetByIdAsync(Guid.NewGuid(), TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ExistingCollection_PersistsChanges()
    {
        // Arrange
        var collection = await _repository!.GetByIdAsync(
            new Guid("11111111-1111-1111-1111-111111111111"),
            TestCancellationToken);
        collection.Should().NotBeNull();

        // Act
        collection!.UpdateMetadata(
            new CollectionName("Updated Name"),
            "Updated description");
        await _repository.UpdateAsync(collection, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert - Retrieve fresh from DB
        var updated = await _repository.GetByIdAsync(collection.Id, TestCancellationToken);
        updated.Should().NotBeNull();
        updated!.Name.Value.Should().Be("Updated Name");
        updated.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task DeleteAsync_ExistingCollection_RemovesFromDatabase()
    {
        // Arrange
        var collection = new DocumentCollection(
            Guid.NewGuid(),
            TestGameId2,
            new CollectionName("To Delete"),
            TestUserId);

        await _repository!.AddAsync(collection, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        await _repository.DeleteAsync(collection, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var result = await _repository.GetByIdAsync(collection.Id, TestCancellationToken);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ExistingCollection_ReturnsTrue()
    {
        // Arrange
        var collectionId = new Guid("11111111-1111-1111-1111-111111111111");

        // Act
        var exists = await _repository!.ExistsAsync(collectionId, TestCancellationToken);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistentCollection_ReturnsFalse()
    {
        // Act
        var exists = await _repository!.ExistsAsync(Guid.NewGuid(), TestCancellationToken);

        // Assert
        exists.Should().BeFalse();
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task FindByGameIdAsync_GameWithCollection_ReturnsCollection()
    {
        // Act
        var result = await _repository!.FindByGameIdAsync(TestGameId1, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.GameId.Should().Be(TestGameId1);
    }

    [Fact]
    public async Task FindByGameIdAsync_GameWithoutCollection_ReturnsNull()
    {
        // Act
        var result = await _repository!.FindByGameIdAsync(Guid.NewGuid(), TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindByGameIdAsync_MultipleGames_ReturnsCorrectCollection()
    {
        // Arrange - Create second collection for different game
        var collection2 = new DocumentCollection(
            Guid.NewGuid(),
            TestGameId2,
            new CollectionName("Second Collection"),
            TestUserId,
            "For game 2");

        await _repository!.AddAsync(collection2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var game1Collection = await _repository.FindByGameIdAsync(TestGameId1, TestCancellationToken);
        var game2Collection = await _repository.FindByGameIdAsync(TestGameId2, TestCancellationToken);

        // Assert
        game1Collection.Should().NotBeNull();
        game1Collection!.GameId.Should().Be(TestGameId1);
        game2Collection.Should().NotBeNull();
        game2Collection!.GameId.Should().Be(TestGameId2);
        game1Collection.Id.Should().NotBe(game2Collection.Id);
    }

    [Fact]
    public async Task FindByUserIdAsync_UserWithCollections_ReturnsAllCollections()
    {
        // Act
        var results = await _repository!.FindByUserIdAsync(TestUserId, TestCancellationToken);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(c => c.CreatedByUserId.Should().Be(TestUserId));
    }

    [Fact]
    public async Task FindByUserIdAsync_UserWithoutCollections_ReturnsEmptyList()
    {
        // Act
        var results = await _repository!.FindByUserIdAsync(Guid.NewGuid(), TestCancellationToken);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task FindByUserIdAsync_OrdersByCreatedAtDescending()
    {
        // Arrange
        var user2Id = new Guid("10000000-0000-0000-0000-000000000002");

        // Create user2 for this test
        var user2 = new UserEntity
        {
            Id = user2Id,
            Email = "user2@test.com",
            DisplayName = "User 2",
            Role = "User"
        };
        _dbContext!.Users.Add(user2);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var game1Id = Guid.NewGuid();
        var game1 = new GameEntity { Id = game1Id, Name = "Test Game 1 for Ordering" };
        _dbContext.Games.Add(game1);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var collection1 = new DocumentCollection(
            Guid.NewGuid(),
            game1Id,
            new CollectionName("First Collection"),
            user2Id);

        await _repository!.AddAsync(collection1, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        await Task.Delay(TimeSpan.FromMilliseconds(100), TestCancellationToken); // Ensure different timestamps

        var game2Id = Guid.NewGuid();
        var game2 = new GameEntity { Id = game2Id, Name = "Test Game 2 for Ordering" };
        _dbContext!.Games.Add(game2);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var collection2 = new DocumentCollection(
            Guid.NewGuid(),
            game2Id,
            new CollectionName("Second Collection"),
            user2Id);

        await _repository!.AddAsync(collection2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var results = await _repository.FindByUserIdAsync(user2Id, TestCancellationToken);

        // Assert
        results.Should().HaveCount(2);
        results[0].CreatedAt.Should().BeOnOrAfter(results[1].CreatedAt);
    }

    [Fact]
    public async Task ExistsForGameAsync_GameWithCollection_ReturnsTrue()
    {
        // Act
        var exists = await _repository!.ExistsForGameAsync(TestGameId1, TestCancellationToken);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsForGameAsync_GameWithoutCollection_ReturnsFalse()
    {
        // Act
        var exists = await _repository!.ExistsForGameAsync(Guid.NewGuid(), TestCancellationToken);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllCollections()
    {
        // Act
        var results = await _repository!.GetAllAsync(TestCancellationToken);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(c => c.Id.Should().NotBeEmpty());
    }

    [Fact]
    public async Task GetAllAsync_OrdersByCreatedAtDescending()
    {
        // Act
        var results = await _repository!.GetAllAsync(TestCancellationToken);

        // Assert
        for (int i = 0; i < results.Count - 1; i++)
        {
            results[i].CreatedAt.Should().BeOnOrAfter(results[i + 1].CreatedAt);
        }
    }

    #endregion

    #region Document Management Tests

    [Fact]
    public async Task AddAsync_CollectionWithDocuments_PersistsDocuments()
    {
        // Arrange - Use TestGameId1 to match TestPdfId1 and TestPdfId2
        var collection = new DocumentCollection(
            Guid.NewGuid(),
            TestGameId1,
            new CollectionName("Collection with Docs"),
            TestUserId);

        collection.AddDocument(TestPdfId1, DocumentType.Base, 0);
        collection.AddDocument(TestPdfId2, DocumentType.Expansion, 1);

        // Act
        await _repository!.AddAsync(collection, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _repository.GetByIdAsync(collection.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.DocumentCount.Should().Be(2);
        retrieved.Documents.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_RemoveDocument_PersistsRemoval()
    {
        // Arrange - Use TestGameId1 to match TestPdfId1 and TestPdfId2
        var collection = new DocumentCollection(
            Guid.NewGuid(),
            TestGameId1,
            new CollectionName("For Removal"),
            TestUserId);

        collection.AddDocument(TestPdfId1, DocumentType.Base, 0);
        collection.AddDocument(TestPdfId2, DocumentType.Expansion, 1);

        await _repository!.AddAsync(collection, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        collection.RemoveDocument(TestPdfId1);
        await _repository.UpdateAsync(collection, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _repository.GetByIdAsync(collection.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.DocumentCount.Should().Be(1);
        retrieved.GetDocument(TestPdfId1).Should().BeNull();
        retrieved.GetDocument(TestPdfId2).Should().NotBeNull();
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task UpdateAsync_ConcurrentUpdates_LastWriteWins()
    {
        // Arrange - Use TestGameId2 (seeded game)
        var collection = new DocumentCollection(
            Guid.NewGuid(),
            TestGameId2,
            new CollectionName("Concurrent Test"),
            TestUserId);

        await _repository!.AddAsync(collection, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Load same collection in two "requests"
        var collection1 = await _repository.GetByIdAsync(collection.Id, TestCancellationToken);
        var collection2 = await _repository.GetByIdAsync(collection.Id, TestCancellationToken);

        // Act - Modify first copy
        collection1!.UpdateMetadata(new CollectionName("Updated by 1"), "Description 1");
        await _repository.UpdateAsync(collection1, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Modify second copy (simulates race condition)
        collection2!.UpdateMetadata(new CollectionName("Updated by 2"), "Description 2");
        await _repository.UpdateAsync(collection2, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert - Last update should win
        var final = await _repository.GetByIdAsync(collection.Id, TestCancellationToken);
        final.Should().NotBeNull();
        final!.Name.Value.Should().Be("Updated by 2");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task AddAsync_MaxDocuments_EnforcesByDomain()
    {
        // Arrange - Use TestGameId2 (seeded game)
        var collection = new DocumentCollection(
            Guid.NewGuid(),
            TestGameId2,
            new CollectionName("Max Docs Test"),
            TestUserId);

        // Add maximum documents
        for (int i = 0; i < 5; i++)
        {
            var pdfId = new Guid($"30000000-0000-0000-0000-00000000000{i:X1}");
            collection.AddDocument(pdfId, DocumentType.Base, i);
        }

        await _repository!.AddAsync(collection, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Retrieve and verify persistence
        var retrieved = await _repository.GetByIdAsync(collection.Id, TestCancellationToken);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.DocumentCount.Should().Be(5);
        retrieved.IsFull.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_LoadsCompleteAggregateState()
    {
        // Arrange
        var collectionId = new Guid("11111111-1111-1111-1111-111111111111");

        // Act
        var collection = await _repository!.GetByIdAsync(collectionId, TestCancellationToken);

        // Assert
        collection.Should().NotBeNull();
        collection!.Id.Should().NotBeEmpty();
        collection.GameId.Should().NotBeEmpty();
        collection.CreatedByUserId.Should().NotBeEmpty();
        collection.CreatedAt.Should().NotBe(default);
        collection.UpdatedAt.Should().NotBe(default);
    }

    #endregion

    #region Test Helpers

    private async Task SeedTestDataAsync()
    {
        // Create test user
        var testUser = new UserEntity
        {
            Id = TestUserId,
            Email = "collectionrepo@test.com",
            DisplayName = "Collection Test User",
            Role = "User"
        };
        _dbContext!.Users.Add(testUser);

        // Create test games
        var game1 = new GameEntity
        {
            Id = TestGameId1,
            Name = "Test Game 1"
        };
        var game2 = new GameEntity
        {
            Id = TestGameId2,
            Name = "Test Game 2"
        };
        _dbContext.Games.Add(game1);
        _dbContext.Games.Add(game2);

        // Create test PDF documents
        var testPdf1 = new PdfDocumentEntity
        {
            Id = TestPdfId1,
            GameId = TestGameId1,
            FileName = "test1.pdf",
            FilePath = "/test/path/test1.pdf",
            FileSizeBytes = 5000,
            PageCount = 10,
            ProcessingStatus = "completed",
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = TestUserId
        };
        var testPdf2 = new PdfDocumentEntity
        {
            Id = TestPdfId2,
            GameId = TestGameId1,
            FileName = "test2.pdf",
            FilePath = "/test/path/test2.pdf",
            FileSizeBytes = 5000,
            PageCount = 10,
            ProcessingStatus = "completed",
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = TestUserId
        };
        _dbContext.PdfDocuments.Add(testPdf1);
        _dbContext.PdfDocuments.Add(testPdf2);

        // Create test collection
        var testCollection = new Api.Infrastructure.Entities.DocumentCollectionEntity
        {
            Id = new Guid("11111111-1111-1111-1111-111111111111"),
            GameId = TestGameId1,
            Name = "Test Collection",
            Description = "For repository tests",
            CreatedByUserId = TestUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DocumentsJson = "[]"
        };
        _dbContext.DocumentCollections.Add(testCollection);

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    #endregion
}
