using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Integration;

/// <summary>
/// Integration tests for RuleConflictFaqRepository.
/// Tests DB constraints, indexes, and cascade behavior.
/// Issue #3966: Repository integration tests.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Collection(nameof(DatabaseCollection))]
public sealed class RuleConflictFaqRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private MeepleAiDbContext _context = null!;
    private RuleConflictFaqRepository _repository = null!;

    public RuleConflictFaqRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _context = await _fixture.CreateDbContextAsync();
        var eventCollector = new DomainEventCollector();
        _repository = new RuleConflictFaqRepository(_context, eventCollector);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
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
        await _repository.AddAsync(faq, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Assert
        var entity = await _context.RuleConflictFAQs.FindAsync(faq.Id);
        Assert.NotNull(entity);
        Assert.Equal("test_pattern", entity.Pattern);
        Assert.Equal(5, entity.Priority);
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

        await _repository.AddAsync(faq1, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Act & Assert
        await _repository.AddAsync(faq2, CancellationToken.None);
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
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

        await _repository.AddAsync(faq, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Act - Delete game (should cascade to FAQ)
        _context.Games.Remove(game);
        await _context.SaveChangesAsync();

        // Assert
        var faqEntity = await _context.RuleConflictFAQs.FindAsync(faq.Id);
        Assert.Null(faqEntity); // FAQ deleted via CASCADE
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

        await _repository.AddAsync(faq, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Act - Search with different casing/spacing
        var result = await _repository.FindByPatternAsync(
            game.Id,
            "upper_case_pattern", // Lowercase, no spaces
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(faq.Id, result.Id);
    }

    [Fact]
    public async Task GetByGameIdAsync_ReturnsOrderedByUsageCountDesc()
    {
        // Arrange
        var game = await CreateTestGameAsync();

        var faq1 = CreateAndRecordUsage(game.Id, "pattern1", usageCount: 10);
        var faq2 = CreateAndRecordUsage(game.Id, "pattern2", usageCount: 50);
        var faq3 = CreateAndRecordUsage(game.Id, "pattern3", usageCount: 25);

        await _repository.AddAsync(faq1, CancellationToken.None);
        await _repository.AddAsync(faq2, CancellationToken.None);
        await _repository.AddAsync(faq3, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Act
        var results = await _repository.GetByGameIdAsync(game.Id, CancellationToken.None);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal(50, results[0].UsageCount); // Highest first
        Assert.Equal(25, results[1].UsageCount);
        Assert.Equal(10, results[2].UsageCount);
    }

    private async Task<GameEntity> CreateTestGameAsync()
    {
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Test Game",
            Publisher = "Test Publisher",
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Games.Add(game);
        await _context.SaveChangesAsync();
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
