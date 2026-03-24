using Api.BoundedContexts.KnowledgeBase.Application.Configuration;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Integration tests for ConversationMemoryCleanupJob with real PostgreSQL.
/// Issue #3985: Integration Tests for Context Engineering Repositories.
/// Validates cleanup job behavior against a real database instead of mocks.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "3985")]
public sealed class ConversationMemoryCleanupJobIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IConversationMemoryRepository? _repository;
    private IServiceProvider? _serviceProvider;
    private Guid _userId;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ConversationMemoryCleanupJobIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_cleanup_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

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

        var eventCollector = _serviceProvider.GetRequiredService<IDomainEventCollector>();
        _repository = new ConversationMemoryRepository(_dbContext, eventCollector);

        await SeedTestUserAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

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

    private async Task SeedTestUserAsync()
    {
        var user = new Api.Infrastructure.Entities.UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "cleanup-test@test.com",
            Role = "user",
            Tier = "premium",
            CreatedAt = DateTime.UtcNow
        };
        _userId = user.Id;
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private ConversationMemoryCleanupJob CreateJob(ConversationMemoryCleanupOptions? options = null)
    {
        var opts = options ?? new ConversationMemoryCleanupOptions
        {
            Enabled = true,
            RetentionPeriod = TimeSpan.FromDays(90),
            CleanupInterval = TimeSpan.FromHours(24),
            BatchSize = 1000
        };

        var optionsMock = new Mock<IOptions<ConversationMemoryCleanupOptions>>();
        optionsMock.Setup(o => o.Value).Returns(opts);

        var logger = _serviceProvider!.GetRequiredService<ILoggerFactory>()
            .CreateLogger<ConversationMemoryCleanupJob>();

        return new ConversationMemoryCleanupJob(
            _repository!,
            optionsMock.Object,
            logger);
    }

    private static Mock<IJobExecutionContext> CreateJobContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        mock.Setup(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        return mock;
    }

    #region Integration Tests

    [Fact]
    public async Task Execute_DeletesOldMemories_FromRealDatabase()
    {
        // Arrange - Create old and recent memories
        var oldMemory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), _userId, null,
            "Old memory to delete", "user",
            DateTime.UtcNow.AddDays(-120)); // 120 days old

        var recentMemory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), _userId, null,
            "Recent memory to keep", "user",
            DateTime.UtcNow.AddDays(-30)); // 30 days old

        await _repository!.AddAsync(oldMemory, TestCancellationToken);
        await _repository.AddAsync(recentMemory, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        var job = CreateJob();
        var jobContext = CreateJobContext();

        // Act
        await job.Execute(jobContext.Object);

        // Assert
        var remaining = await _dbContext.ConversationMemories
            .ToListAsync(TestCancellationToken);
        remaining.Should().HaveCount(1);
        remaining[0].Content.Should().Be("Recent memory to keep");
    }

    [Fact]
    public async Task Execute_WithCustomRetentionPeriod_RespectsConfiguration()
    {
        // Arrange - Create memories at different ages
        var memory60DaysOld = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), _userId, null,
            "60 days old", "user",
            DateTime.UtcNow.AddDays(-60));

        var memory10DaysOld = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), _userId, null,
            "10 days old", "user",
            DateTime.UtcNow.AddDays(-10));

        await _repository!.AddAsync(memory60DaysOld, TestCancellationToken);
        await _repository.AddAsync(memory10DaysOld, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // 30-day retention: should delete 60-day-old, keep 10-day-old
        var job = CreateJob(new ConversationMemoryCleanupOptions
        {
            Enabled = true,
            RetentionPeriod = TimeSpan.FromDays(30),
            CleanupInterval = TimeSpan.FromHours(24),
            BatchSize = 1000
        });
        var jobContext = CreateJobContext();

        // Act
        await job.Execute(jobContext.Object);

        // Assert
        var remaining = await _dbContext.ConversationMemories
            .ToListAsync(TestCancellationToken);
        remaining.Should().HaveCount(1);
        remaining[0].Content.Should().Be("10 days old");
    }

    [Fact]
    public async Task Execute_WhenDisabled_PreservesAllMemories()
    {
        // Arrange
        var oldMemory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), _userId, null,
            "Very old memory", "user",
            DateTime.UtcNow.AddDays(-365));

        await _repository!.AddAsync(oldMemory, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        var job = CreateJob(new ConversationMemoryCleanupOptions { Enabled = false });
        var jobContext = CreateJobContext();

        // Act
        await job.Execute(jobContext.Object);

        // Assert
        var remaining = await _dbContext.ConversationMemories
            .ToListAsync(TestCancellationToken);
        remaining.Should().HaveCount(1);
        remaining[0].Content.Should().Be("Very old memory");
    }

    [Fact]
    public async Task Execute_WithNoOldMemories_DeletesNothing()
    {
        // Arrange - All memories are recent
        var memory1 = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), _userId, null,
            "Recent 1", "user", DateTime.UtcNow.AddDays(-5));
        var memory2 = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), _userId, null,
            "Recent 2", "assistant", DateTime.UtcNow.AddDays(-10));

        await _repository!.AddAsync(memory1, TestCancellationToken);
        await _repository.AddAsync(memory2, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        var job = CreateJob();
        var jobContext = CreateJobContext();

        // Act
        await job.Execute(jobContext.Object);

        // Assert
        var remaining = await _dbContext.ConversationMemories
            .ToListAsync(TestCancellationToken);
        remaining.Should().HaveCount(2);
    }

    [Fact]
    public async Task Execute_WithEmptyDatabase_CompletesSuccessfully()
    {
        // Arrange - No memories exist
        var job = CreateJob();
        var jobContext = CreateJobContext();

        // Act & Assert - Should not throw
        var exception = await Record.ExceptionAsync(() => job.Execute(jobContext.Object));
        exception.Should().BeNull();
    }

    [Fact]
    public async Task Execute_DeletesMultipleOldMemories_PreservesRecentOnes()
    {
        // Arrange - Mix of old and recent memories
        var oldTimestamp = DateTime.UtcNow.AddDays(-100);
        var recentTimestamp = DateTime.UtcNow.AddDays(-10);

        for (int i = 0; i < 5; i++)
        {
            var old = new ConversationMemory(
                Guid.NewGuid(), Guid.NewGuid(), _userId, null,
                $"Old {i}", "user", oldTimestamp.AddMinutes(-i));
            await _repository!.AddAsync(old, TestCancellationToken);
        }

        for (int i = 0; i < 3; i++)
        {
            var recent = new ConversationMemory(
                Guid.NewGuid(), Guid.NewGuid(), _userId, null,
                $"Recent {i}", "user", recentTimestamp.AddMinutes(-i));
            await _repository!.AddAsync(recent, TestCancellationToken);
        }

        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        var job = CreateJob();
        var jobContext = CreateJobContext();

        // Act
        await job.Execute(jobContext.Object);

        // Assert
        var remaining = await _dbContext.ConversationMemories
            .ToListAsync(TestCancellationToken);
        remaining.Should().HaveCount(3);
        remaining.Should().AllSatisfy(m => m.Content.Should().StartWith("Recent"));
    }

    [Fact]
    public async Task Execute_SetsSuccessResult_WithCorrectDeleteCount()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
        {
            var old = new ConversationMemory(
                Guid.NewGuid(), Guid.NewGuid(), _userId, null,
                $"Old memory {i}", "user",
                DateTime.UtcNow.AddDays(-100 - i));
            await _repository!.AddAsync(old, TestCancellationToken);
        }
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        var job = CreateJob();
        var jobContext = CreateJobContext();

        // Act
        await job.Execute(jobContext.Object);

        // Assert - Verify result was set on job context
        jobContext.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(true) &&
            r.GetType().GetProperty("DeletedCount")!.GetValue(r)!.Equals(3)));
    }

    #endregion
}
