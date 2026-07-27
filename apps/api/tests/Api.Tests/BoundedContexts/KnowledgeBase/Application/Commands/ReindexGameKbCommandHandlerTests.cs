using Api.BoundedContexts.KnowledgeBase.Application.Channels;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Bug B6: <see cref="ReindexGameKbCommandHandler"/> must enforce per-game RAG access
/// (owner / admin / public game) before performing any reindex work. Without this gate any
/// authenticated user could trigger an LLM-cost reindex on — and mutate — another user's KB.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ReindexGameKbCommandHandlerTests
{
    private static MeepleAiDbContext CreateInMemoryDb(string testName)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ReindexGameKbTests_{testName}_{Guid.NewGuid()}")
            .Options;
        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    [Fact]
    public async Task Handle_NonOwner_ThrowsForbidden_AndDoesNotReindex()
    {
        // Arrange — a user with NO RAG access to the game.
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var db = CreateInMemoryDb(nameof(Handle_NonOwner_ThrowsForbidden_AndDoesNotReindex));

        var jobRepo = new Mock<IKbReindexJobRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var channel = new KbReindexChannel();

        var ragAccess = new Mock<IRagAccessService>();
        ragAccess
            .Setup(s => s.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new ReindexGameKbCommandHandler(
            db, jobRepo.Object, channel, unitOfWork.Object, ragAccess.Object,
            Mock.Of<ILogger<ReindexGameKbCommandHandler>>());

        var command = new ReindexGameKbCommand(GameId: gameId, UserId: userId, UserRole: "User");

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));

        ragAccess.Verify(
            s => s.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // No reindex side effects: no job persisted, no work enqueued.
        jobRepo.Verify(r => r.AddAsync(It.IsAny<KbReindexJob>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        channel.Reader.TryRead(out _).Should().BeFalse("no reindex request must be enqueued when access is denied");
    }

    [Fact]
    public async Task Handle_Admin_IsAllowed_AndProceeds()
    {
        // Arrange — admin passes the access gate; with no indexable PDFs the handler returns a
        // completed no-op job (proving it proceeded past the gate).
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var db = CreateInMemoryDb(nameof(Handle_Admin_IsAllowed_AndProceeds));

        var jobRepo = new Mock<IKbReindexJobRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var channel = new KbReindexChannel();

        var ragAccess = new Mock<IRagAccessService>();
        ragAccess
            .Setup(s => s.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new ReindexGameKbCommandHandler(
            db, jobRepo.Object, channel, unitOfWork.Object, ragAccess.Object,
            Mock.Of<ILogger<ReindexGameKbCommandHandler>>());

        var command = new ReindexGameKbCommand(GameId: gameId, UserId: userId, UserRole: "Admin");

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        ragAccess.Verify(
            s => s.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()),
            Times.Once);
        result.Should().NotBeNull();
        result.Status.Should().Be("completed");
        result.PdfCount.Should().Be(0);
    }
}
