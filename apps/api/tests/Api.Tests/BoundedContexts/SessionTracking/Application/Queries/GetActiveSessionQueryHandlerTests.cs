using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameToolkit;
using Api.Infrastructure.Entities.SessionTracking;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "BlockA-StoreSignalR")]
public sealed class GetActiveSessionQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly GetActiveSessionQueryHandler _handler;

    public GetActiveSessionQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _handler = new GetActiveSessionQueryHandler(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Handle_WhenSessionHasScoringConfigured_ReturnsScoringTypeAndScoreData()
    {
        // Arrange — seed an Active session with ScoringType=Points and a payload.
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string scoreDataJson = "{\"scores\":[{\"playerId\":\"p1\",\"points\":10}]}";

        _db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = sessionId,
            UserId = userId,
            GameId = Guid.NewGuid(),
            SessionCode = "SCR001",
            SessionType = "Generic",
            Status = "Active",
            SessionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            ScoringType = "Points",
            ScoreData = scoreDataJson
        });
        await _db.SaveChangesAsync();

        // Act
        var dto = await _handler.Handle(
            new GetActiveSessionQuery(userId),
            CancellationToken.None);

        // Assert
        dto.Should().NotBeNull();
        dto!.ScoringType.Should().Be("Points");
        dto.ScoreData.Should().Be(scoreDataJson);
    }

    // -------------------------------------------------------------------------
    // Issue #2483: TurnOrderType wiring (Path B — derived from GameToolkit)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_WhenSessionGameHasToolkitWithTurnTemplate_ReturnsTurnOrderType()
    {
        // Arrange — game has a published toolkit whose TurnTemplate is Sequential (int=3).
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _db.GameToolkits.Add(new GameToolkitEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Name = "Test Toolkit",
            CreatedByUserId = userId,
            IsPublished = true,
            RowVersion = [0],
            // camelCase JSON matching JsonNamingPolicy.CamelCase used by repository
            TurnTemplateJson = "{\"turnOrderType\":3,\"phases\":[]}"
        });

        _db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            SessionCode = "TOT001",
            SessionType = "Generic",
            Status = "Active",
            SessionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            ScoringType = "Points",
            ScoreData = "{}"
        });
        await _db.SaveChangesAsync();

        // Act
        var dto = await _handler.Handle(new GetActiveSessionQuery(userId), CancellationToken.None);

        // Assert
        dto.Should().NotBeNull();
        dto!.TurnOrderType.Should().Be("Sequential");
    }

    [Fact]
    public async Task Handle_WhenSessionGameHasNoMatchingToolkit_TurnOrderTypeIsNull()
    {
        // Arrange — session linked to a game that has no published toolkit at all.
        var userId = Guid.NewGuid();
        var gameWithNoToolkit = Guid.NewGuid();

        _db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameWithNoToolkit,
            SessionCode = "TOT002",
            SessionType = "Generic",
            Status = "Active",
            SessionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            ScoringType = "Points",
            ScoreData = "{}"
        });
        await _db.SaveChangesAsync();

        // Act
        var dto = await _handler.Handle(new GetActiveSessionQuery(userId), CancellationToken.None);

        // Assert
        dto.Should().NotBeNull();
        dto!.TurnOrderType.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenToolkitHasNoTurnTemplate_TurnOrderTypeIsNull()
    {
        // Arrange — toolkit exists for the game but TurnTemplateJson is null.
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _db.GameToolkits.Add(new GameToolkitEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Name = "Toolkit No Turn",
            CreatedByUserId = userId,
            IsPublished = true,
            RowVersion = [0],
            TurnTemplateJson = null
        });

        _db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            SessionCode = "TOT003",
            SessionType = "Generic",
            Status = "Active",
            SessionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            ScoringType = "Points",
            ScoreData = "{}"
        });
        await _db.SaveChangesAsync();

        // Act
        var dto = await _handler.Handle(new GetActiveSessionQuery(userId), CancellationToken.None);

        // Assert
        dto.Should().NotBeNull();
        dto!.TurnOrderType.Should().BeNull();
    }
}
