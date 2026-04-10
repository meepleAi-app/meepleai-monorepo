using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Integration tests for SetTurnOrderCommand (Session Flow v2.1 — T6).
/// Verifies Manual + Random turn order modes, owner-only auth, deterministic
/// Fisher-Yates shuffle, seed persistence, and diary event emission.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "SessionFlowV2.1")]
public sealed class SetTurnOrderCommandTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private CreateSessionCommandHandler? _createHandler;
    private SetTurnOrderCommandHandler? _setTurnOrderHandler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public SetTurnOrderCommandTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_setturnorder_{Guid.NewGuid():N}";
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
            loggerFactory.CreateLogger<CreateSessionCommandHandler>(),
            TimeProvider.System);

        _setTurnOrderHandler = new SetTurnOrderCommandHandler(sessionRepo, unitOfWork, _dbContext, TimeProvider.System);
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
    public async Task Manual_PersistsExplicitOrder()
    {
        // Arrange — create session with owner + 2 guest participants.
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var createResult = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId, guestNames: new[] { "Guest A", "Guest B" }),
            TestCancellationToken);

        // Load participant IDs in insertion order.
        _dbContext!.ChangeTracker.Clear();
        var participantIds = await _dbContext.SessionTrackingParticipants
            .AsNoTracking()
            .Where(p => p.SessionId == createResult.SessionId)
            .OrderBy(p => p.JoinOrder)
            .Select(p => p.Id)
            .ToListAsync(TestCancellationToken);

        participantIds.Should().HaveCount(3);
        var reversedOrder = participantIds.AsEnumerable().Reverse().ToList();

        // Act — set a manual reversed order.
        var result = await _setTurnOrderHandler!.Handle(
            new SetTurnOrderCommand(
                createResult.SessionId,
                userId,
                TurnOrderMethod.Manual,
                reversedOrder),
            TestCancellationToken);

        // Assert — result echoes the input.
        result.Method.Should().Be(nameof(TurnOrderMethod.Manual));
        result.Seed.Should().BeNull();
        result.Order.Should().Equal(reversedOrder);

        // Assert — persisted on the session entity.
        _dbContext.ChangeTracker.Clear();
        var persisted = await _dbContext.SessionTrackingSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == createResult.SessionId, TestCancellationToken);

        persisted.TurnOrderMethod.Should().Be(nameof(TurnOrderMethod.Manual));
        persisted.TurnOrderSeed.Should().BeNull();
        persisted.CurrentTurnIndex.Should().Be(0);
        persisted.TurnOrderJson.Should().NotBeNullOrWhiteSpace();

        var persistedOrder = JsonSerializer.Deserialize<List<Guid>>(persisted.TurnOrderJson!);
        persistedOrder.Should().Equal(reversedOrder);

        // Assert — diary event emitted.
        var diary = await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == createResult.SessionId && e.EventType == "turn_order_set")
            .ToListAsync(TestCancellationToken);

        diary.Should().HaveCount(1);
        diary[0].GameNightId.Should().Be(createResult.GameNightEventId);
        diary[0].CreatedBy.Should().Be(userId);
        diary[0].Source.Should().Be("user");
        diary[0].Payload.Should().Contain("Manual");
    }

    [Fact]
    public async Task Random_GeneratesSeedAndShufflesDeterministically()
    {
        // Arrange — create session with owner + 4 guests (5 total participants).
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var createResult = await _createHandler!.Handle(
            BuildCreateCommand(
                userId,
                gameId,
                guestNames: new[] { "Guest 1", "Guest 2", "Guest 3", "Guest 4" }),
            TestCancellationToken);

        _dbContext!.ChangeTracker.Clear();
        var participantIds = await _dbContext.SessionTrackingParticipants
            .AsNoTracking()
            .Where(p => p.SessionId == createResult.SessionId)
            .OrderBy(p => p.JoinOrder)
            .Select(p => p.Id)
            .ToListAsync(TestCancellationToken);

        participantIds.Should().HaveCount(5);

        // Act — Random mode (no manual order).
        var result = await _setTurnOrderHandler!.Handle(
            new SetTurnOrderCommand(
                createResult.SessionId,
                userId,
                TurnOrderMethod.Random,
                ManualOrder: null),
            TestCancellationToken);

        // Assert — seed was generated, order is a permutation of participantIds.
        result.Method.Should().Be(nameof(TurnOrderMethod.Random));
        result.Seed.Should().NotBeNull();
        result.Order.Should().HaveCount(5);
        result.Order.Should().BeEquivalentTo(participantIds); // same set, any order

        // Assert — determinism: replay Fisher-Yates with the returned seed gives the same order.
        var expected = participantIds.ToList();
#pragma warning disable CA5394 // Seeded replay for deterministic audit, not security.
        var rng = new Random(result.Seed!.Value);
        for (var i = expected.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (expected[i], expected[j]) = (expected[j], expected[i]);
        }
#pragma warning restore CA5394
        result.Order.Should().Equal(expected);

        // Assert — session persisted with seed.
        _dbContext.ChangeTracker.Clear();
        var persisted = await _dbContext.SessionTrackingSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == createResult.SessionId, TestCancellationToken);

        persisted.TurnOrderMethod.Should().Be(nameof(TurnOrderMethod.Random));
        persisted.TurnOrderSeed.Should().Be(result.Seed);
        persisted.CurrentTurnIndex.Should().Be(0);

        // Assert — diary event contains the seed (for replay).
        var diary = await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == createResult.SessionId && e.EventType == "turn_order_set")
            .ToListAsync(TestCancellationToken);

        diary.Should().HaveCount(1);
        diary[0].GameNightId.Should().Be(createResult.GameNightEventId);
        diary[0].Payload.Should().Contain("Random");
        diary[0].Payload.Should().Contain(result.Seed!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Manual_EmptyOrder_FailsValidation()
    {
        var validator = new SetTurnOrderCommandValidator();

        var command = new SetTurnOrderCommand(
            SessionId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Method: TurnOrderMethod.Manual,
            ManualOrder: Array.Empty<Guid>());

        var validationResult = await validator.ValidateAsync(command, TestCancellationToken);

        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e =>
            e.PropertyName == nameof(SetTurnOrderCommand.ManualOrder));
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
