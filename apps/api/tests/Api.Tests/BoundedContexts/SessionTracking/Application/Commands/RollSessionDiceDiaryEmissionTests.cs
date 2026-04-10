using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
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
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Integration test for RollSessionDiceCommand diary emission (Session Flow v2.1 — T7).
/// Verifies that rolling dice persists both the DiceRoll aggregate and a
/// <c>dice_rolled</c> SessionEvent correlated to the parent GameNight.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "SessionFlowV2.1")]
public sealed class RollSessionDiceDiaryEmissionTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private CreateSessionCommandHandler? _createHandler;
    private RollSessionDiceCommandHandler? _rollHandler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public RollSessionDiceDiaryEmissionTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_rolldice_diary_{Guid.NewGuid():N}";
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
        var diceRollRepo = new DiceRollRepository(_dbContext, eventCollector);

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
            TimeProvider.System,
            new DiaryStreamService());

        var syncMock = new Mock<ISessionSyncService>();
        syncMock
            .Setup(s => s.PublishEventAsync(
                It.IsAny<Guid>(),
                It.IsAny<INotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _rollHandler = new RollSessionDiceCommandHandler(
            sessionRepo,
            diceRollRepo,
            unitOfWork,
            syncMock.Object,
            _dbContext,
            TimeProvider.System,
            new DiaryStreamService());
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
    public async Task RollDice_EmitsDiceRolledDiaryEvent()
    {
        // Arrange — create an Active session via CreateSessionCommand so we get a
        // real GameNight envelope and an owner participant in Host role.
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var createResult = await _createHandler!.Handle(
            BuildCreateCommand(userId, gameId),
            TestCancellationToken);

        _dbContext!.ChangeTracker.Clear();
        var ownerParticipantId = await _dbContext.SessionTrackingParticipants
            .AsNoTracking()
            .Where(p => p.SessionId == createResult.SessionId && p.IsOwner)
            .Select(p => p.Id)
            .FirstAsync(TestCancellationToken);

        // Act — owner rolls 2d6.
        var result = await _rollHandler!.Handle(
            new RollSessionDiceCommand(
                SessionId: createResult.SessionId,
                ParticipantId: ownerParticipantId,
                RequesterId: userId,
                Formula: "2d6",
                Label: "attack"),
            TestCancellationToken);

        // Assert — totals are within the expected range for 2d6 (no modifier).
        result.Total.Should().BeInRange(2, 12);
        result.Formula.Should().Be("2D6");
        result.Rolls.Should().HaveCount(2);

        // Assert — DiceRoll persisted.
        _dbContext.ChangeTracker.Clear();
        var persistedRoll = await _dbContext.SessionTrackingDiceRolls
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == result.DiceRollId, TestCancellationToken);
        persistedRoll.Should().NotBeNull();

        // Assert — diary event emitted, correlated with the GameNight envelope.
        var diary = await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == createResult.SessionId && e.EventType == "dice_rolled")
            .ToListAsync(TestCancellationToken);

        diary.Should().HaveCount(1);
        diary[0].GameNightId.Should().Be(createResult.GameNightEventId);
        diary[0].CreatedBy.Should().Be(userId);
        diary[0].Source.Should().Be("user");
        diary[0].Payload.Should().NotBeNullOrWhiteSpace();
        diary[0].Payload!.Should().Contain("2D6");
        diary[0].Payload!.Should().Contain(result.Total.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static CreateSessionCommand BuildCreateCommand(
        Guid userId,
        Guid gameId) =>
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
            GuestNames: null);
}
