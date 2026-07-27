using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Bug B6: <see cref="RebuildRaptorCommandHandler"/> must enforce per-game RAG access
/// (owner / admin / public game) before the tier gate and before any rebuild work. Without this
/// gate any authenticated user could trigger LLM-cost RAPTOR rebuilds on another user's KB.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RebuildRaptorCommandHandlerTests
{
    private static MeepleAiDbContext CreateInMemoryDb(string testName)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"RebuildRaptorTests_{testName}_{Guid.NewGuid()}")
            .Options;
        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    [Fact]
    public async Task Handle_NonOwner_ThrowsForbidden_AndDoesNotCheckTierOrRebuild()
    {
        // Arrange — a user with NO RAG access to the game.
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var db = CreateInMemoryDb(nameof(Handle_NonOwner_ThrowsForbidden_AndDoesNotCheckTierOrRebuild));

        var tier = new Mock<ITierEnforcementService>();
        var ragAccess = new Mock<IRagAccessService>();
        ragAccess
            .Setup(s => s.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new RebuildRaptorCommandHandler(
            db, tier.Object, ragAccess.Object,
            Mock.Of<ILogger<RebuildRaptorCommandHandler>>(),
            raptorIndexer: null);

        var command = new RebuildRaptorCommand(GameId: gameId, UserId: userId, UserRole: "User");

        // Act & Assert — access is checked FIRST, so the tier gate is never consulted.
        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));

        ragAccess.Verify(
            s => s.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()),
            Times.Once);
        tier.Verify(
            t => t.CanPerformAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "the tier gate must not be reached when RAG access is denied");
    }

    [Fact]
    public async Task Handle_Admin_IsAllowed_AndProceedsToTierGate()
    {
        // Arrange — admin passes the access gate; tier gate allows; no RAPTOR indexer registered →
        // handler returns completed with 0 nodes (proving it proceeded past the access gate).
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var db = CreateInMemoryDb(nameof(Handle_Admin_IsAllowed_AndProceedsToTierGate));

        var tier = new Mock<ITierEnforcementService>();
        tier
            .Setup(t => t.CanPerformAsync(userId, TierAction.RaptorRebuild, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ragAccess = new Mock<IRagAccessService>();
        ragAccess
            .Setup(s => s.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new RebuildRaptorCommandHandler(
            db, tier.Object, ragAccess.Object,
            Mock.Of<ILogger<RebuildRaptorCommandHandler>>(),
            raptorIndexer: null);

        var command = new RebuildRaptorCommand(GameId: gameId, UserId: userId, UserRole: "Admin");

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        ragAccess.Verify(
            s => s.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()),
            Times.Once);
        tier.Verify(
            t => t.CanPerformAsync(userId, TierAction.RaptorRebuild, It.IsAny<CancellationToken>()),
            Times.Once);
        result.Should().NotBeNull();
        result.Status.Should().Be("completed");
    }
}
