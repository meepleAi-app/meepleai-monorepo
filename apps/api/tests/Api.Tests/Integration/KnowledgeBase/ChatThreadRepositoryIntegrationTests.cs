using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
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

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for ChatThreadRepository using SharedTestcontainersFixture.
/// Tests PostgreSQL persistence, thread lifecycle, message operations, and query methods.
/// Issue #2307: Week 3 - Repository integration testing
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "2307")]
public sealed class ChatThreadRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IChatThreadRepository? _repository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private static readonly Guid TestThreadId1 = new("40000000-0000-0000-0000-000000000001");
    private static readonly Guid TestThreadId2 = new("40000000-0000-0000-0000-000000000002");
    private static readonly Guid TestThreadId3 = new("40000000-0000-0000-0000-000000000003");
    private static readonly Guid TestUserId1 = new("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUserId2 = new("10000000-0000-0000-0000-000000000002");

    public ChatThreadRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_chatthreadrepo_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        services.AddScoped<IChatThreadRepository, ChatThreadRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IChatThreadRepository>();
        _unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        // Create database schema with Polly retry
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

    #region Helper Methods

    private static User CreateTestUser(
        Guid id,
        string email,
        string displayName = "Test User",
        Role? role = null,
        UserTier? tier = null)
    {
        var user = new User(
            id: id,
            email: new Email(email),
            displayName: displayName,
            passwordHash: PasswordHash.Create("UniqueT3stPwd!"),
            role: role ?? Role.User,
            tier: tier ?? UserTier.Free
        );
        return user;
    }

    private static ChatThread CreateTestChatThread(
        Guid threadId,
        Guid userId,
        Guid? gameId = null,
        string? title = null)
    {
        return new ChatThread(
            id: threadId,
            userId: userId,
            gameId: gameId,
            title: title
        );
    }

    private async Task EnsureUserExistsAsync(Guid userId, string? email = null)
    {
        using var scope = _serviceProvider!.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var existingUser = await userRepo.GetByIdAsync(userId, TestCancellationToken);
        if (existingUser == null)
        {
            var user = CreateTestUser(userId, email ?? $"user{userId:N}@test.com");
            await userRepo.AddAsync(user, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
        }
    }

    private async Task CleanDatabaseAsync()
    {
        if (_dbContext == null) return;

        _dbContext.ChatThreads.RemoveRange(_dbContext.ChatThreads);
        _dbContext.SharedGames.RemoveRange(_dbContext.SharedGames);
        _dbContext.Users.RemoveRange(_dbContext.Users);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_NewThread_ShouldPersistToDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var thread = CreateTestChatThread(TestThreadId1, TestUserId1, null, "Catan Rules Discussion");

        // Act
        await _repository!.AddAsync(thread, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _dbContext!.ChatThreads
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == TestThreadId1, TestCancellationToken);

        persisted.Should().NotBeNull();
        persisted!.UserId.Should().Be(TestUserId1);
        persisted.GameId.Should().BeNull();
        persisted.Title.Should().Be("Catan Rules Discussion");
    }

    #endregion

    #region FindByUserIdAsync Tests

    [Fact]
    public async Task FindByUserIdAsync_MultipleThreads_ShouldReturnAllForUser()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        await EnsureUserExistsAsync(TestUserId2);

        var thread1 = CreateTestChatThread(TestThreadId1, TestUserId1, null);
        var thread2 = CreateTestChatThread(TestThreadId2, TestUserId1, null);
        var thread3 = CreateTestChatThread(TestThreadId3, TestUserId2, null);

        await _repository!.AddAsync(thread1, TestCancellationToken);
        await _repository.AddAsync(thread2, TestCancellationToken);
        await _repository.AddAsync(thread3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.FindByUserIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(t => t.UserId == TestUserId1);
    }

    [Fact]
    public async Task FindByUserIdAsync_NoThreads_ShouldReturnEmptyList()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.FindByUserIdAsync(Guid.NewGuid(), TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region FindByUserIdAndGameIdAsync — catalog game

    /// <summary>
    /// #4244: il gioco di un thread sta in una di due colonne. Un thread su un gioco del catalogo
    /// resta nella colonna con la chiave esterna verso <c>shared_games</c>, andata e ritorno, e
    /// la colonna del gioco privato resta vuota. (Qui c'erano i test di <c>FindByGameIdAsync</c>,
    /// tolto: non aveva chiamanti e restituiva i thread di tutti gli utenti su un gioco.)
    /// </summary>
    [Fact]
    public async Task FindByUserIdAndGameIdAsync_ThreadsOnACatalogGame_KeepTheGameInTheCatalogColumn()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);

        var gameId = Guid.NewGuid();
        _dbContext!.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = gameId,
            Title = "Test Game for ChatThread FK",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var thread1 = CreateTestChatThread(TestThreadId1, TestUserId1, gameId);
        var thread2 = CreateTestChatThread(TestThreadId2, TestUserId1, gameId);
        await _repository!.AddAsync(thread1, TestCancellationToken);
        await _repository.AddAsync(thread2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.FindByUserIdAndGameIdAsync(TestUserId1, gameId, TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(t =>
        {
            t.GameId.Should().Be(gameId);
            t.GameIsPrivate.Should().BeFalse();
        });

        _dbContext.ChangeTracker.Clear();
        var rows = await _dbContext.ChatThreads.AsNoTracking()
            .Where(t => t.Id == TestThreadId1 || t.Id == TestThreadId2)
            .Select(t => new { t.GameId, t.PrivateGameId })
            .ToListAsync(TestCancellationToken);
        rows.Should().HaveCount(2).And.OnlyContain(r => r.GameId == gameId && r.PrivateGameId == null);
    }

    #endregion

    #region GetRecentAsync Tests

    [Fact]
    public async Task GetRecentAsync_MultipleThreads_ShouldReturnOrderedByLastMessage()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);

        var thread1 = CreateTestChatThread(TestThreadId1, TestUserId1, null);
        await Task.Delay(TimeSpan.FromMilliseconds(10), TestCancellationToken);
        var thread2 = CreateTestChatThread(TestThreadId2, TestUserId1, null);
        await Task.Delay(TimeSpan.FromMilliseconds(10), TestCancellationToken);
        var thread3 = CreateTestChatThread(TestThreadId3, TestUserId1, null);

        await _repository!.AddAsync(thread1, TestCancellationToken);
        await _repository.AddAsync(thread2, TestCancellationToken);
        await _repository.AddAsync(thread3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetRecentAsync(limit: 20, TestCancellationToken);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInDescendingOrder(t => t.LastMessageAt);
        result[0].Id.Should().Be(TestThreadId3); // Most recent
    }

    [Fact]
    public async Task GetRecentAsync_WithLimit_ShouldRespectLimit()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);

        for (int i = 1; i <= 5; i++)
        {
            var threadId = new Guid($"40000000-0000-0000-0000-00000000000{i}");
            var thread = CreateTestChatThread(threadId, TestUserId1);
            await _repository!.AddAsync(thread, TestCancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(10), TestCancellationToken);
        }
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository!.GetRecentAsync(limit: 3, TestCancellationToken);

        // Assert
        result.Should().HaveCount(3);
    }

    #endregion

    #region CountByUserIdAsync Tests

    [Fact]
    public async Task CountByUserIdAsync_MultipleThreads_ShouldReturnCorrectCount()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        await EnsureUserExistsAsync(TestUserId2);

        var thread1 = CreateTestChatThread(TestThreadId1, TestUserId1);
        var thread2 = CreateTestChatThread(TestThreadId2, TestUserId1);
        var thread3 = CreateTestChatThread(TestThreadId3, TestUserId2);

        await _repository!.AddAsync(thread1, TestCancellationToken);
        await _repository.AddAsync(thread2, TestCancellationToken);
        await _repository.AddAsync(thread3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var count = await _repository.CountByUserIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task CountByUserIdAsync_NoThreads_ShouldReturnZero()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var count = await _repository!.CountByUserIdAsync(Guid.NewGuid(), TestCancellationToken);

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public async Task ComplexScenario_ThreadWithMessages_ShouldMaintainConsistency()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);

        // Use null GameId to avoid FK violation (game-agnostic thread)
        var thread = CreateTestChatThread(TestThreadId1, TestUserId1, null, "Rules Question");
        thread.AddUserMessage("How many victory points to win?");
        thread.AddAssistantMessage("You need 10 victory points to win Settlers of Catan.");

        await _repository!.AddAsync(thread, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var retrieved = await _dbContext!.ChatThreads
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == TestThreadId1, TestCancellationToken);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.MessagesJson.Should().NotBeNullOrEmpty();
        // MessageCount is calculated property on domain entity, not stored on entity
    }

    #endregion

    #region LiveSessionId (#4276)

    private static readonly Guid TestLiveSessionId = new("50000000-0000-0000-0000-000000000001");

    private static ChatThread CreateAssistantThread(Guid threadId, Guid userId, Guid liveSessionId) =>
        new(id: threadId, userId: userId, gameId: null, title: null, agentId: null,
            agentType: "session-assistant", liveSessionId: liveSessionId);

    [Fact]
    public async Task FindByLiveSessionIdAsync_ReturnsTheThreadOfTheSession_WithItsMessageIds()
    {
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var thread = CreateAssistantThread(TestThreadId1, TestUserId1, TestLiveSessionId);
        thread.AddUserMessage("Marco fa 5 punti");
        var answerId = Guid.NewGuid();
        thread.AddMessage(new Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.ChatMessage(
            "Ho preparato la proposta.", "assistant", thread.Messages.Count, id: answerId));
        await _repository!.AddAsync(thread, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var found = await _repository.FindByLiveSessionIdAsync(TestLiveSessionId, TestCancellationToken);

        found.Should().NotBeNull();
        found!.Id.Should().Be(TestThreadId1);
        found.LiveSessionId.Should().Be(TestLiveSessionId);
        found.GameId.Should().BeNull("un gioco privato violerebbe la chiave esterna verso shared_games");
        found.Messages.Should().HaveCount(2);
        found.Messages[1].Id.Should().Be(answerId, "le proposte puntano al messaggio dell'assistente per id");
        found.DomainEvents.Should().BeEmpty(
            "ricaricare un thread non è un evento: senza, ogni turno riscriverebbe nell'outbox tutta la sua storia");
        (await _repository.FindByLiveSessionIdAsync(Guid.NewGuid(), TestCancellationToken)).Should().BeNull();
    }

    /// <summary>
    /// <c>UpdateAsync</c> riscrive l'intera riga da <c>MapToPersistence</c>: una colonna che la mappatura
    /// dimentica viene azzerata al secondo turno, e il thread smette di appartenere alla sessione.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_KeepsTheLiveSessionId()
    {
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        await _repository!.AddAsync(CreateAssistantThread(TestThreadId1, TestUserId1, TestLiveSessionId), TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var loaded = await _repository.FindByLiveSessionIdAsync(TestLiveSessionId, TestCancellationToken);
        loaded!.AddUserMessage("a chi tocca?");
        await _repository.UpdateAsync(loaded, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        var persisted = await _dbContext!.ChatThreads.AsNoTracking()
            .SingleAsync(t => t.Id == TestThreadId1, TestCancellationToken);
        persisted.LiveSessionId.Should().Be(TestLiveSessionId);
        (await _repository.FindByLiveSessionIdAsync(TestLiveSessionId, TestCancellationToken))!.Messages.Should().HaveCount(1);
    }

    [Fact]
    public async Task TheUserChatLists_LeaveOutTheAssistantThreads_ButTheExportStillSeesThem()
    {
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        await _repository!.AddAsync(CreateTestChatThread(TestThreadId1, TestUserId1, null, "Regole di Azul"), TestCancellationToken);
        await _repository.AddAsync(CreateAssistantThread(TestThreadId2, TestUserId1, TestLiveSessionId), TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var (items, total) = await _repository.FindByUserIdFilteredAsync(TestUserId1, cancellationToken: TestCancellationToken);
        var counted = await _repository.CountByUserIdAsync(TestUserId1, TestCancellationToken);
        var exported = await _repository.FindByUserIdAsync(TestUserId1, TestCancellationToken);

        items.Select(t => t.Id).Should().Equal(TestThreadId1);
        total.Should().Be(1);
        counted.Should().Be(1, "è il totale dello storico delle chat");
        exported.Should().HaveCount(2, "l'export dei dati dell'utente comprende ogni sua conversazione");
    }

    #endregion
}
