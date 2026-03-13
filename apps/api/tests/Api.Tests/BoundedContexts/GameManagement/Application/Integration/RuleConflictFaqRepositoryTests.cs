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

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

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

        await _repository.AddAsync(faq1, TestCancellationToken);
        await _context.SaveChangesAsync(TestCancellationToken);

        // Act & Assert
        await _repository.AddAsync(faq2, TestCancellationToken);
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync(TestCancellationToken));
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

        await _repository.AddAsync(faq, TestCancellationToken);
        await _context.SaveChangesAsync(TestCancellationToken);

        // Act - Search with different casing/spacing
        var result = await _repository.FindByPatternAsync(
            game.Id,
            "upper_case_pattern", // Lowercase, no spaces
            TestCancellationToken);

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

        await _repository.AddAsync(faq1, TestCancellationToken);
        await _repository.AddAsync(faq2, TestCancellationToken);
        await _repository.AddAsync(faq3, TestCancellationToken);
        await _context.SaveChangesAsync(TestCancellationToken);

        // Act
        var results = await _repository.GetByGameIdAsync(game.Id, TestCancellationToken);

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
