using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Unit.KnowledgeBase;

/// <summary>
/// Tests for RAG access enforcement on agent creation handlers.
/// Ownership/RAG access feature: Tasks 11-12.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class RagAccessEnforcementTests
{
    // ========================================================================
    // CreateUserAgentCommandHandler — RAG access enforcement
    // ========================================================================

    [Fact]
    public async Task CreateUserAgent_NoRagAccess_ThrowsForbiddenException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var ragService = new Mock<IRagAccessService>();
        ragService.Setup(r => r.CanAccessRagAsync(userId, gameId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateUserAgentHandler(ragService.Object);
        var command = new CreateUserAgentCommand(
            UserId: userId,
            UserTier: "free",
            UserRole: "User",
            GameId: gameId,
            AgentType: "RAG",
            Name: "Test",
            StrategyName: null,
            StrategyParameters: null);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ForbiddenException>()).Which;
        ex.Message.Should().Contain("possedere il gioco");
    }

    [Fact]
    public async Task CreateUserAgent_WithRagAccess_ProceedsNormally()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var ragService = new Mock<IRagAccessService>();
        ragService.Setup(r => r.CanAccessRagAsync(userId, gameId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var agentRepo = new Mock<IAgentRepository>();
        agentRepo.Setup(r => r.ResolveGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => id);
        agentRepo.Setup(r => r.ExistsByNameForUserAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateUserAgentHandler(ragService.Object, agentRepo: agentRepo.Object);
        var command = new CreateUserAgentCommand(
            UserId: userId,
            UserTier: "free",
            UserRole: "User",
            GameId: gameId,
            AgentType: "RAG",
            Name: "Test",
            StrategyName: null,
            StrategyParameters: null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — did not throw, handler proceeded
        result.Should().NotBeNull();
        result.Name.Should().Be("Test");
    }

    [Fact]
    public async Task CreateUserAgent_AdminRole_BypassesRagAccess()
    {
        // Arrange — admin always passes RAG access check via role cascade
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var ragService = new Mock<IRagAccessService>();
        ragService.Setup(r => r.CanAccessRagAsync(userId, gameId, UserRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Admin always returns true

        var agentRepo = new Mock<IAgentRepository>();
        agentRepo.Setup(r => r.ResolveGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => id);
        agentRepo.Setup(r => r.ExistsByNameForUserAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateUserAgentHandler(ragService.Object, agentRepo: agentRepo.Object);
        var command = new CreateUserAgentCommand(
            UserId: userId,
            UserTier: "free",
            UserRole: "Admin",
            GameId: gameId,
            AgentType: "RAG",
            Name: "Admin Test",
            StrategyName: null,
            StrategyParameters: null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    // ========================================================================
    // CreateAgentWithSetupCommandHandler — RAG access enforcement
    // ========================================================================

    [Fact]
    public async Task CreateAgentWithSetup_NoRagAccess_ThrowsForbiddenException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var ragService = new Mock<IRagAccessService>();
        ragService.Setup(r => r.CanAccessRagAsync(userId, gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateSetupHandler(ragService.Object);
        var command = new CreateAgentWithSetupCommand(
            UserId: userId,
            UserTier: "free",
            UserRole: "User",
            GameId: gameId,
            AddToCollection: false,
            AgentType: "RAG",
            AgentName: "Test",
            StrategyName: null,
            StrategyParameters: null);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ForbiddenException>()).Which;
        ex.Message.Should().Contain("possedere il gioco");
    }

    [Fact]
    public async Task CreateAgentWithSetup_WithRagAccess_ProceedsNormally()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var ragService = new Mock<IRagAccessService>();
        ragService.Setup(r => r.CanAccessRagAsync(userId, gameId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var agentRepo = new Mock<IAgentRepository>();
        agentRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());
        agentRepo.Setup(r => r.ExistsByNameForUserAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateSetupHandler(ragService.Object, agentRepo: agentRepo.Object);
        var command = new CreateAgentWithSetupCommand(
            UserId: userId,
            UserTier: "free",
            UserRole: "User",
            GameId: gameId,
            AddToCollection: false,
            AgentType: "RAG",
            AgentName: "Test",
            StrategyName: null,
            StrategyParameters: null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AgentId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAgentWithSetup_RagPublicGame_ProceedsWithoutOwnership()
    {
        // Arrange — game is RAG-public, so CanAccessRagAsync returns true even without ownership
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var ragService = new Mock<IRagAccessService>();
        ragService.Setup(r => r.CanAccessRagAsync(userId, gameId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // RAG-public game

        var agentRepo = new Mock<IAgentRepository>();
        agentRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());
        agentRepo.Setup(r => r.ExistsByNameForUserAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateSetupHandler(ragService.Object, agentRepo: agentRepo.Object);
        var command = new CreateAgentWithSetupCommand(
            UserId: userId,
            UserTier: "free",
            UserRole: "User",
            GameId: gameId,
            AddToCollection: false,
            AgentType: "RAG",
            AgentName: "Public Game Agent",
            StrategyName: null,
            StrategyParameters: null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AgentName.Should().Be("Public Game Agent");
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static CreateUserAgentCommandHandler CreateUserAgentHandler(
        IRagAccessService ragService,
        IAgentRepository? agentRepo = null)
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var repo = agentRepo ?? CreateDefaultAgentRepo();
        var tierService = new Mock<ITierEnforcementService>();
        tierService.Setup(t => t.CanPerformAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var logger = new Mock<ILogger<CreateUserAgentCommandHandler>>();

        return new CreateUserAgentCommandHandler(repo, db, tierService.Object, ragService, logger.Object);
    }

    private static CreateAgentWithSetupCommandHandler CreateSetupHandler(
        IRagAccessService ragService,
        IAgentRepository? agentRepo = null)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);

        var repo = agentRepo ?? CreateDefaultSetupAgentRepo();
        var chatRepo = new Mock<IChatThreadRepository>();
        chatRepo.Setup(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var libraryRepo = new Mock<IUserLibraryRepository>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        uow.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var mediator = new Mock<IMediator>();
        var logger = new Mock<ILogger<CreateAgentWithSetupCommandHandler>>();

        return new CreateAgentWithSetupCommandHandler(
            repo, chatRepo.Object, libraryRepo.Object, ragService,
            uow.Object, db, mediator.Object, logger.Object);
    }

    private static IAgentRepository CreateDefaultAgentRepo()
    {
        var mock = new Mock<IAgentRepository>();
        mock.Setup(r => r.ResolveGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => id);
        mock.Setup(r => r.ExistsByNameForUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return mock.Object;
    }

    private static IAgentRepository CreateDefaultSetupAgentRepo()
    {
        var mock = new Mock<IAgentRepository>();
        mock.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());
        mock.Setup(r => r.ExistsByNameForUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return mock.Object;
    }
}
