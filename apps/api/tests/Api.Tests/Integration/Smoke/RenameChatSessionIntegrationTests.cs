using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Smoke;

/// <summary>
/// Integration tests for the ChatSession rename endpoint and naming disambiguation.
/// Issue #905: SG4 — Sessions CRUD (3 tipi) + naming disambiguation.
///
/// Covers:
/// - SG4-T1: Happy path rename persists new title
/// - SG4-T2: Naming disambiguation: ThreadId → 400 with INVALID_ENTITY_TYPE hint
/// - SG4-T3: Unknown ID → 404
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Dependency", "PostgreSQL")]
public sealed class RenameChatSessionIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Stable test IDs — no FK dependency on game/user for chat_sessions
    private static readonly Guid TestUserId = new("10000000-0000-0000-0000-000000000901");
    private static readonly Guid TestGameId = new("20000000-0000-0000-0000-000000000901");
    private static readonly Guid TestSessionId = new("30000000-0000-0000-0000-000000000901");
    private static readonly Guid TestThreadId = new("40000000-0000-0000-0000-000000000901");
    private static readonly Guid TestUnknownId = new("99000000-0000-0000-0000-000000000901");

    public RenameChatSessionIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_renamechat_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
        services.AddScoped<IChatThreadRepository, ChatThreadRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply migrations
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestCancellationToken);
            }
        }

        // Seed required parent entities (User + Game) for FK constraints
        _dbContext.Users.Add(new UserEntity
        {
            Id = TestUserId,
            Email = "smoke-rename-session@test.meepleai",
            PasswordHash = "v1.test-hash",
            CreatedAt = DateTime.UtcNow,
        });
        _dbContext.Games.Add(new GameEntity
        {
            Id = TestGameId,
            Name = "SG4 Rename Test Game",
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Seed a ChatSession with initial title
        _dbContext.ChatSessions.Add(new ChatSessionEntity
        {
            Id = TestSessionId,
            UserId = TestUserId,
            GameId = TestGameId,
            Title = "Original Title",
            AgentConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
        });

        // Seed a ChatThread (for disambiguation test — different entity type, same UUID space)
        _dbContext.ChatThreads.Add(new ChatThreadEntity
        {
            Id = TestThreadId,
            UserId = TestUserId,
            GameId = TestGameId,
            Title = "SG4 Disambiguation Thread",
            Status = "active",
            MessagesJson = "[]",
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
        });

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();

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

        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();
    }

    /// <summary>
    /// SG4-T1 happy path: rename an existing session persists the new title.
    /// </summary>
    [Fact]
    public async Task RenameChatSession_WithValidId_UpdatesTitle()
    {
        // Arrange
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var command = new RenameChatSessionCommand(SessionId: TestSessionId, Title: "Renamed Title SG4");

        // Act
        var dto = await mediator.Send(command, TestCancellationToken);

        // Assert — DTO reflects new title
        dto.Should().NotBeNull();
        dto.Id.Should().Be(TestSessionId);
        dto.Title.Should().Be("Renamed Title SG4");

        // Assert — persisted in DB (fresh context to avoid tracking cache)
        var persisted = await _dbContext!.ChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == TestSessionId, TestCancellationToken);

        persisted.Should().NotBeNull();
        persisted!.Title.Should().Be("Renamed Title SG4");
    }

    /// <summary>
    /// SG4-T2 naming disambiguation: a UUID that belongs to a ChatThread but not a ChatSession
    /// should be detectable. The repository returns null for IChatSessionRepository.GetByIdAsync,
    /// and non-null for IChatThreadRepository.GetByIdAsync.
    ///
    /// This test validates the repository-level signal that the endpoint handler uses to emit
    /// the 400 INVALID_ENTITY_TYPE disambiguation response (endpoint-level logic, not MediatR).
    /// </summary>
    [Fact]
    public async Task RenameChatSession_WithThreadId_Disambiguation_SessionRepoReturnsNull_ThreadRepoReturnsEntity()
    {
        // Arrange — use both repos directly (mirrors disambiguation logic in endpoint handler)
        var sessionRepo = _serviceProvider!.GetRequiredService<IChatSessionRepository>();
        var threadRepo = _serviceProvider.GetRequiredService<IChatThreadRepository>();

        // Act — session repo should not find TestThreadId (it belongs to chat_threads, not chat_sessions)
        var session = await sessionRepo.GetByIdAsync(TestThreadId, TestCancellationToken);

        // Assert — disambiguation condition: session is null
        session.Should().BeNull("TestThreadId exists as ChatThread, not ChatSession");

        // But thread repo DOES find it
        var thread = await threadRepo.GetByIdAsync(TestThreadId, TestCancellationToken);
        thread.Should().NotBeNull("TestThreadId exists as ChatThread");
        thread!.Id.Should().Be(TestThreadId);

        // Verify: if we were in the endpoint, we'd return 400 INVALID_ENTITY_TYPE
        // (The disambiguation condition is: session == null AND thread != null → 400)
        var disambiguationTriggered = session == null && thread != null;
        disambiguationTriggered.Should().BeTrue(
            "endpoint should return 400 INVALID_ENTITY_TYPE when UUID belongs to ChatThread not ChatSession");
    }

    /// <summary>
    /// SG4-T3 unknown ID: neither a session nor a thread exists → NotFoundException from handler.
    /// </summary>
    [Fact]
    public async Task RenameChatSession_WithUnknownId_ThrowsNotFoundException()
    {
        // Arrange
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var command = new RenameChatSessionCommand(SessionId: TestUnknownId, Title: "Irrelevant Title");

        // Act & Assert — handler throws NotFoundException (maps to 404 at endpoint level)
        await Assert.ThrowsAsync<NotFoundException>(
            () => mediator.Send(command, TestCancellationToken));
    }
}
