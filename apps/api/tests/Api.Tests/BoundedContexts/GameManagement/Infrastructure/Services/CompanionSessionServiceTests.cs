using Api.BoundedContexts.GameManagement.Infrastructure.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Unit tests for CompanionSessionService (ADR-083 SP0 anti-corruption layer).
/// Verifies that CreateCompanionAsync adds a Session companion via ISessionRepository
/// and returns its Id, without calling SaveChanges (that is the caller's responsibility).
/// Issue #2501 SP0. #2552: pre-flight game-existence guard mapping a missing GameId to a
/// 404 NotFoundException instead of letting the FK violation surface as a 500 at SaveChanges.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class CompanionSessionServiceTests
{
    private readonly Mock<ISessionRepository> _sessionRepo = new();
    private readonly Mock<ISharedGameRepository> _sharedGameRepo = new();

    private CompanionSessionService CreateSut() => new(_sessionRepo.Object, _sharedGameRepo.Object);

    [Fact]
    public async Task CreateCompanionAsync_GameExists_AddsSession_AndReturnsItsId()
    {
        Session? captured = null;
        _sessionRepo.Setup(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((s, _) => captured = s)
            .Returns(Task.CompletedTask);

        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _sharedGameRepo.Setup(r => r.ExistsByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = CreateSut();
        var trackingId = await sut.CreateCompanionAsync(userId, gameId, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Id.Should().Be(trackingId);
        captured.UserId.Should().Be(userId);
        captured.GameId.Should().Be(gameId);
        _sessionRepo.Verify(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCompanionAsync_GameDoesNotExist_ThrowsNotFound_AndDoesNotAddSession()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _sharedGameRepo.Setup(r => r.ExistsByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = CreateSut();
        var act = () => sut.CreateCompanionAsync(userId, gameId, CancellationToken.None);

        // A GameId that points at no catalog row must surface as 404, not a 500 FK violation
        // at the downstream SaveChanges (#2552, respects #2568 "never InvalidOperationException").
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("Game");
        ex.ResourceId.Should().Be(gameId.ToString());
        _sessionRepo.Verify(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
