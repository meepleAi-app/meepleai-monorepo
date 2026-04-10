using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Integration tests for AdvanceTurnCommand (Session Flow v2.1 — Plan 1bis T1).
/// Covers cyclic turn progression, owner-only authorisation, conflict when
/// turn order is not set, and diary (turn_advanced) event emission with the
/// correct game-night correlation.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "SessionFlowV2.1")]
public sealed class AdvanceTurnCommandTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private CreateSessionCommandHandler? _createHandler;
    private SetTurnOrderCommandHandler? _setTurnOrderHandler;
    private AdvanceTurnCommandHandler? _advanceTurnHandler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AdvanceTurnCommandTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_advanceturn_{Guid.NewGuid():N}";
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
                await Task.Delay(500, TestCancellationToken);
            }
        }

        var eventCollector = _serviceProvider.GetRequiredService<IDomainEventCollector>();
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var sessionRepo = new SessionRepository(_dbContext, eventCollector);

        var quotaMock = new Mock<ISessionQuotaService>();
        quotaMock
            .Setup(s => s.CheckQuotaAsync(
                It.IsAny<Guid>(),
                It.IsAny<UserTier>(),
                It.IsAny<Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Allowed(0, 100));

        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
        _createHandler = new CreateSessionCommandHandler(
            sessionRepo,
            unitOfWork,
            quotaMock.Object,
            _dbContext,
            mediator,
            loggerFactory.CreateLogger<CreateSessionCommandHandler>());

        _setTurnOrderHandler = new SetTurnOrderCommandHandler(sessionRepo, unitOfWork, _dbContext);
        _advanceTurnHandler = new AdvanceTurnCommandHandler(sessionRepo, unitOfWork, _dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public async Task Advance_CyclesThroughPlayersAndEmitsDiary()
    {
        // Arrange — session with owner + 2 guests (3 total), manual turn order.
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var createResult = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId, guestNames: new[] { "Guest A", "Guest B" }),
            TestCancellationToken);

        _dbContext!.ChangeTracker.Clear();
        var participantIds = await _dbContext.SessionTrackingParticipants
            .AsNoTracking()
            .Where(p => p.SessionId == createResult.SessionId)
            .OrderBy(p => p.JoinOrder)
            .Select(p => p.Id)
            .ToListAsync(TestCancellationToken);
        participantIds.Should().HaveCount(3);

        await _setTurnOrderHandler!.Handle(
            new SetTurnOrderCommand(createResult.SessionId, userId, TurnOrderMethod.Manual, participantIds),
            TestCancellationToken);

        // Act — advance 4 times: 0→1, 1→2, 2→0 (wrap), 0→1
        var r1 = await _advanceTurnHandler!.Handle(new AdvanceTurnCommand(createResult.SessionId, userId), TestCancellationToken);
        var r2 = await _advanceTurnHandler.Handle(new AdvanceTurnCommand(createResult.SessionId, userId), TestCancellationToken);
        var r3 = await _advanceTurnHandler.Handle(new AdvanceTurnCommand(createResult.SessionId, userId), TestCancellationToken);
        var r4 = await _advanceTurnHandler.Handle(new AdvanceTurnCommand(createResult.SessionId, userId), TestCancellationToken);

        // Assert — command results reflect cyclic progression and correct participant Ids.
        r1.FromIndex.Should().Be(0); r1.ToIndex.Should().Be(1);
        r1.FromParticipantId.Should().Be(participantIds[0]);
        r1.ToParticipantId.Should().Be(participantIds[1]);

        r2.FromIndex.Should().Be(1); r2.ToIndex.Should().Be(2);
        r2.FromParticipantId.Should().Be(participantIds[1]);
        r2.ToParticipantId.Should().Be(participantIds[2]);

        r3.FromIndex.Should().Be(2); r3.ToIndex.Should().Be(0);
        r3.FromParticipantId.Should().Be(participantIds[2]);
        r3.ToParticipantId.Should().Be(participantIds[0]);

        r4.FromIndex.Should().Be(0); r4.ToIndex.Should().Be(1);

        // Assert — persisted CurrentTurnIndex matches the final state.
        _dbContext.ChangeTracker.Clear();
        var persisted = await _dbContext.SessionTrackingSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == createResult.SessionId, TestCancellationToken);
        persisted.CurrentTurnIndex.Should().Be(1);

        // Assert — 4 turn_advanced diary events, correlated with game night.
        var diary = await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == createResult.SessionId && e.EventType == "turn_advanced")
            .OrderBy(e => e.Timestamp)
            .ToListAsync(TestCancellationToken);

        diary.Should().HaveCount(4);
        diary.Should().OnlyContain(e => e.GameNightId == createResult.GameNightEventId);
        diary.Should().OnlyContain(e => e.CreatedBy == userId);
        diary.Should().OnlyContain(e => e.Source == "user");

        // Payload of the first event should reference the expected Guids.
        diary[0].Payload.Should().Contain(participantIds[0].ToString());
        diary[0].Payload.Should().Contain(participantIds[1].ToString());

        // The wrap event should expose fromIndex=2 and toIndex=0 in the JSON.
        using var wrapDoc = JsonDocument.Parse(diary[2].Payload);
        wrapDoc.RootElement.GetProperty("fromIndex").GetInt32().Should().Be(2);
        wrapDoc.RootElement.GetProperty("toIndex").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Advance_WithoutTurnOrderSet_ThrowsConflict()
    {
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var createResult = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId, guestNames: new[] { "Alone" }),
            TestCancellationToken);

        var act = async () => await _advanceTurnHandler!.Handle(
            new AdvanceTurnCommand(createResult.SessionId, userId),
            TestCancellationToken);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Advance_NotOwner_ThrowsForbidden()
    {
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var createResult = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId, guestNames: new[] { "Guest A", "Guest B" }),
            TestCancellationToken);

        _dbContext!.ChangeTracker.Clear();
        var participantIds = await _dbContext.SessionTrackingParticipants
            .AsNoTracking()
            .Where(p => p.SessionId == createResult.SessionId)
            .OrderBy(p => p.JoinOrder)
            .Select(p => p.Id)
            .ToListAsync(TestCancellationToken);

        await _setTurnOrderHandler!.Handle(
            new SetTurnOrderCommand(createResult.SessionId, userId, TurnOrderMethod.Manual, participantIds),
            TestCancellationToken);

        var otherUser = Guid.NewGuid();

        var act = async () => await _advanceTurnHandler!.Handle(
            new AdvanceTurnCommand(createResult.SessionId, otherUser),
            TestCancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Advance_SessionNotFound_ThrowsNotFound()
    {
        var unknown = Guid.NewGuid();

        var act = async () => await _advanceTurnHandler!.Handle(
            new AdvanceTurnCommand(unknown, Guid.NewGuid()),
            TestCancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static CreateSessionCommand BuildCreateCommand(
        Guid userId,
        Guid gameId,
        IReadOnlyList<string>? guestNames = null) =>
        new(
            UserId: userId,
            GameId: gameId,
            SessionType: nameof(SessionType.Generic),
            SessionDate: null,
            Location: null,
            Participants: new List<ParticipantDto>
            {
                new() { DisplayName = "Owner", IsOwner = true, UserId = userId }
            },
            GameNightEventId: null,
            GuestNames: guestNames);
}
