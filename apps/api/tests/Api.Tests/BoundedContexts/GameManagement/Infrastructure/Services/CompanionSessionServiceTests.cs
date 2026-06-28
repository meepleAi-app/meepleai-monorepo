using Api.BoundedContexts.GameManagement.Infrastructure.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Unit tests for CompanionSessionService (ADR-083 SP0 anti-corruption layer).
/// Verifies that CreateCompanionAsync adds a Session companion via ISessionRepository
/// and returns its Id, without calling SaveChanges (that is the caller's responsibility).
/// Issue #2501 SP0.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CompanionSessionServiceTests
{
    [Fact]
    public async Task CreateCompanionAsync_AddsSession_AndReturnsItsId()
    {
        var repo = new Mock<ISessionRepository>();
        Session? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((s, _) => captured = s)
            .Returns(Task.CompletedTask);

        var sut = new CompanionSessionService(repo.Object);
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var trackingId = await sut.CreateCompanionAsync(userId, gameId, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(captured!.Id, trackingId);
        Assert.Equal(userId, captured.UserId);
        Assert.Equal(gameId, captured.GameId);
        repo.Verify(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
