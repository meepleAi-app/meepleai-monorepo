using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Infrastructure;
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
}
