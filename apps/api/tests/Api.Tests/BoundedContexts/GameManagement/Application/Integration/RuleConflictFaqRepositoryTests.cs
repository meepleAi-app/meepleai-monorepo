using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Integration;

/// <summary>
/// Integration tests for RuleConflictFaqRepository.
/// Tests DB constraints, indexes, and cascade behavior.
/// Issue #3966: Repository integration tests.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Collection("Integration-GroupC")]
public sealed class RuleConflictFaqRepositoryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext _context = null!;
    private RuleConflictFaqRepository _repository = null!;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public RuleConflictFaqRepositoryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_rulefaq_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply migrations with retry
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _context.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }

        var eventCollector = _serviceProvider.GetRequiredService<IDomainEventCollector>();
        _repository = new RuleConflictFaqRepository(_context, eventCollector);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();

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

    [Fact]
    public async Task AddAsync_WithValidFaq_PersistsToDatabase()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var faq = RuleConflictFAQ.Create(
            Guid.NewGuid(),
            game.Id,
            ConflictType.Contradiction,
            "test_pattern",
            "Test resolution",
            5,
            TimeProvider.System);

        // Act
        await _repository.AddAsync(faq, TestCancellationToken);
        await _context.SaveChangesAsync(TestCancellationToken);

        // Assert
        var entity = await _context.RuleConflictFAQs.FindAsync(faq.Id);
        entity.Should().NotBeNull();
        entity.Pattern.Should().Be("test_pattern");
        entity.Priority.Should().Be(5);
    }

    [Fact]
    public async Task AddAsync_WithDuplicatePattern_ThrowsDbUpdateException()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var faq1 = RuleConflictFAQ.Create(
            Guid.NewGuid(),
            game.Id,
            ConflictType.Contradiction,
            "duplicate_pattern",
            "First resolution",
            3,
            TimeProvider.System);

        var faq2 = RuleConflictFAQ.Create(
            Guid.NewGuid(),
            game.Id,
            ConflictType.Ambiguity,
            "duplicate_pattern", // Same pattern, same game
            "Second resolution",
            7,
            TimeProvider.System);

        await _repository.AddAsync(faq1, TestCancellationToken);
        await _context.SaveChangesAsync(TestCancellationToken);

        // Act & Assert
        await _repository.AddAsync(faq2, TestCancellationToken);
        await ((Func<Task>)(() => _context.SaveChangesAsync(TestCancellationToken))).Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenGameDeleted_CascadesDeleteToFaqs()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var faq = RuleConflictFAQ.Create(
            Guid.NewGuid(),
            game.Id,
            ConflictType.MissingRule,
            "cascade_test",
            "Test resolution",
            5,
            TimeProvider.System);

        await _repository.AddAsync(faq, TestCancellationToken);
        await _context.SaveChangesAsync(TestCancellationToken);

        // Act - Delete game (should cascade to FAQ)
        _context.Games.Remove(game);
        await _context.SaveChangesAsync(TestCancellationToken);

        // Assert
        var faqEntity = await _context.RuleConflictFAQs.FindAsync(faq.Id);
        faqEntity.Should().BeNull(); // FAQ deleted via CASCADE
    }

    [Fact]
    public async Task FindByPatternAsync_NormalizesPattern()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var faq = RuleConflictFAQ.Create(
            Guid.NewGuid(),
            game.Id,
            ConflictType.Contradiction,
            "  UPPER_CASE_PATTERN  ", // Will be normalized
            "Test resolution",
            5,
            TimeProvider.System);

        await _repository.AddAsync(faq, TestCancellationToken);
        await _context.SaveChangesAsync(TestCancellationToken);

        // Act - Search with different casing/spacing
        var result = await _repository.FindByPatternAsync(
            game.Id,
            "upper_case_pattern", // Lowercase, no spaces
            TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(faq.Id);
    }

    [Fact]
    public async Task GetByGameIdAsync_ReturnsOrderedByUsageCountDesc()
    {
        // Arrange
        var game = await CreateTestGameAsync();

        var faq1 = CreateAndRecordUsage(game.Id, "pattern1", usageCount: 10);
        var faq2 = CreateAndRecordUsage(game.Id, "pattern2", usageCount: 50);
        var faq3 = CreateAndRecordUsage(game.Id, "pattern3", usageCount: 25);

        await _repository.AddAsync(faq1, TestCancellationToken);
        await _repository.AddAsync(faq2, TestCancellationToken);
        await _repository.AddAsync(faq3, TestCancellationToken);
        await _context.SaveChangesAsync(TestCancellationToken);

        // Act
        var results = await _repository.GetByGameIdAsync(game.Id, TestCancellationToken);

        // Assert
        results.Count.Should().Be(3);
        results[0].UsageCount.Should().Be(50); // Highest first
        results[1].UsageCount.Should().Be(25);
        results[2].UsageCount.Should().Be(10);
    }

    private async Task<GameEntity> CreateTestGameAsync()
    {
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Game",
            Publisher = "Test Publisher",
            CreatedAt = DateTime.UtcNow
        };

        _context.Games.Add(game);
        await _context.SaveChangesAsync(TestCancellationToken);
        return game;
    }

    private RuleConflictFAQ CreateAndRecordUsage(Guid gameId, string pattern, int usageCount)
    {
        var faq = RuleConflictFAQ.Create(
            Guid.NewGuid(),
            gameId,
            ConflictType.Contradiction,
            pattern,
            "Test resolution",
            5,
            TimeProvider.System);

        // Simulate usage recording
        for (int i = 0; i < usageCount; i++)
        {
            faq.RecordUsage(TimeProvider.System);
        }

        return faq;
    }
}
