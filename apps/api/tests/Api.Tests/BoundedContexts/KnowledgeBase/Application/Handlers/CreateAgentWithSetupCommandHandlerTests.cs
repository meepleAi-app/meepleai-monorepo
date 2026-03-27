using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for CreateAgentWithSetupCommandHandler.
/// Issue #4772: Agent Creation Orchestration Flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class CreateAgentWithSetupCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _mockAgentRepo;
    private readonly Mock<IChatThreadRepository> _mockChatRepo;
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepo;
    private readonly Mock<IRagAccessService> _mockRagAccessService;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<CreateAgentWithSetupCommandHandler>> _mockLogger;
    private readonly CreateAgentWithSetupCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public CreateAgentWithSetupCommandHandlerTests()
    {
        _mockAgentRepo = new Mock<IAgentRepository>();
        _mockChatRepo = new Mock<IChatThreadRepository>();
        _mockLibraryRepo = new Mock<IUserLibraryRepository>();
        _mockRagAccessService = new Mock<IRagAccessService>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<CreateAgentWithSetupCommandHandler>>();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);

        _handler = new CreateAgentWithSetupCommandHandler(
            _mockAgentRepo.Object,
            _mockChatRepo.Object,
            _mockLibraryRepo.Object,
            _mockRagAccessService.Object,
            _mockUnitOfWork.Object,
            _db,
            _mockMediator.Object,
            _mockLogger.Object);

        // Default: no existing agents, name doesn't exist
        _mockAgentRepo
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());
        _mockAgentRepo
            .Setup(r => r.ExistsByNameForUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Default: RAG access allowed
        _mockRagAccessService
            .Setup(r => r.CanAccessRagAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task Handle_BasicFlow_CreatesAgentAndThread()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.AgentId.Should().NotBe(Guid.Empty);
        result.ThreadId.Should().NotBe(Guid.Empty);
        result.SlotUsed.Should().Be(1);
        result.GameAddedToCollection.Should().BeFalse();

        _mockAgentRepo.Verify(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockChatRepo.Verify(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AddToCollection_True_GameNotInLibrary_AddsGame()
    {
        // Arrange
        _mockLibraryRepo
            .Setup(r => r.IsGameInLibraryAsync(_userId, _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = CreateCommand(addToCollection: true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.GameAddedToCollection.Should().BeTrue();
        _mockLibraryRepo.Verify(
            r => r.AddAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AddToCollection_True_GameAlreadyInLibrary_SkipsAdd()
    {
        // Arrange
        _mockLibraryRepo
            .Setup(r => r.IsGameInLibraryAsync(_userId, _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = CreateCommand(addToCollection: true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.GameAddedToCollection.Should().BeFalse();
        _mockLibraryRepo.Verify(
            r => r.AddAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AddToCollection_False_NeverChecksLibrary()
    {
        // Arrange
        var command = CreateCommand(addToCollection: false);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockLibraryRepo.Verify(
            r => r.IsGameInLibraryAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SlotsFull_ThrowsConflictException()
    {
        // Arrange - 3 active agents for free tier (max 3)
        var existingAgents = Enumerable.Range(0, 3)
            .Select(i => CreateAgent($"Agent{i}"))
            .ToList();

        _mockAgentRepo
            .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAgents);

        var command = CreateCommand(tier: "free");

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_SlotAvailable_WithExistingAgents_CorrectSlotNumber()
    {
        // Arrange - 2 active agents for free tier (max 3)
        var existingAgents = new List<Agent>
        {
            CreateAgent("Agent1"),
            CreateAgent("Agent2")
        };

        _mockAgentRepo
            .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAgents);

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.SlotUsed.Should().Be(3);
    }

    [Fact]
    public async Task Handle_InactiveAgents_NotCountedForQuota()
    {
        // Arrange - 3 agents but 1 inactive = 2 active, 1 slot available for free tier
        var agents = new List<Agent>
        {
            CreateAgent("Active1"),
            CreateAgent("Active2"),
            CreateAgent("Inactive1", isActive: false)
        };

        _mockAgentRepo
            .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - should succeed (2 active + 1 new = 3 = max for free)
        result.SlotUsed.Should().Be(3);
    }

    [Fact]
    public async Task Handle_CustomAgentName_UsesProvidedName()
    {
        // Arrange
        var command = CreateCommand(agentName: "MyCustomAgent");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.AgentName.Should().Be("MyCustomAgent");
    }

    [Fact]
    public async Task Handle_NoAgentName_GeneratesDefault()
    {
        // Arrange
        var command = CreateCommand(agentName: null!);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.AgentName.Should().StartWith("RAG-");
    }

    [Fact]
    public async Task Handle_DuplicateName_AutoSuffixes()
    {
        // Arrange - first name exists, second doesn't
        _mockAgentRepo
            .SetupSequence(r => r.ExistsByNameForUserAsync(_userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)   // "MyAgent" exists
            .ReturnsAsync(false); // "MyAgent-1" doesn't exist

        var command = CreateCommand(agentName: "MyAgent");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.AgentName.Should().Be("MyAgent-1");
    }

    [Fact]
    public async Task Handle_TransactionRolledBack_OnError()
    {
        // Arrange - agent add fails
        _mockAgentRepo
            .Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var command = CreateCommand();

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

        _mockUnitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_AdminRole_BypassesSlotLimit()
    {
        // Arrange - 200 agents but admin role
        var agents = Enumerable.Range(0, 200)
            .Select(i => CreateAgent($"Agent{i}"))
            .ToList();

        _mockAgentRepo
            .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var command = CreateCommand(role: "Admin");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - should succeed (admin bypasses limits)
        result.SlotUsed.Should().Be(201);
    }

    [Fact]
    public async Task Handle_WithSharedGameId_SendsLinkCommand()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        _mockMediator
            .Setup(m => m.Send(It.IsAny<LinkAgentToSharedGameCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);

        var command = CreateCommand(sharedGameId: sharedGameId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockMediator.Verify(
            m => m.Send(
                It.Is<LinkAgentToSharedGameCommand>(cmd =>
                    cmd.GameId == sharedGameId && cmd.AgentId == result.AgentId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithDocumentIds_SendsUpdateDocumentsCommand()
    {
        // Arrange
        var documentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _mockMediator
            .Setup(m => m.Send(It.IsAny<UpdateAgentDocumentsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateAgentDocumentsResult(true, "ok", Guid.NewGuid(), 2));

        var command = CreateCommand(documentIds: documentIds);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockMediator.Verify(
            m => m.Send(
                It.Is<UpdateAgentDocumentsCommand>(cmd =>
                    cmd.AgentId == result.AgentId &&
                    cmd.DocumentIds.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutOptionalFields_DoesNotSendExtraCommands()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockMediator.Verify(
            m => m.Send(It.IsAny<LinkAgentToSharedGameCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockMediator.Verify(
            m => m.Send(It.IsAny<UpdateAgentDocumentsCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyDocumentIds_DoesNotSendUpdateCommand()
    {
        // Arrange
        var command = CreateCommand(documentIds: new List<Guid>());

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockMediator.Verify(
            m => m.Send(It.IsAny<UpdateAgentDocumentsCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithBothOptionalFields_SendsBothCommands()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var documentIds = new List<Guid> { Guid.NewGuid() };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<LinkAgentToSharedGameCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);
        _mockMediator
            .Setup(m => m.Send(It.IsAny<UpdateAgentDocumentsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateAgentDocumentsResult(true, "ok", Guid.NewGuid(), 1));

        var command = CreateCommand(sharedGameId: sharedGameId, documentIds: documentIds);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockMediator.Verify(
            m => m.Send(
                It.Is<LinkAgentToSharedGameCommand>(cmd => cmd.GameId == sharedGameId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mockMediator.Verify(
            m => m.Send(
                It.Is<UpdateAgentDocumentsCommand>(cmd => cmd.AgentId == result.AgentId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private CreateAgentWithSetupCommand CreateCommand(
        string tier = "free",
        string role = "user",
        bool addToCollection = false,
        string agentName = null!,
        string? strategyName = null,
        Guid? sharedGameId = null,
        List<Guid>? documentIds = null)
    {
        return new CreateAgentWithSetupCommand(
            UserId: _userId,
            UserTier: tier,
            UserRole: role,
            GameId: _gameId,
            AddToCollection: addToCollection,
            AgentType: "RAG",
            AgentName: agentName,
            StrategyName: strategyName,
            StrategyParameters: null)
        {
            SharedGameId = sharedGameId,
            DocumentIds = documentIds
        };
    }

    private static Agent CreateAgent(string name, bool isActive = true)
    {
        return new Agent(
            Guid.NewGuid(),
            name,
            AgentType.Parse("RAG"),
            AgentStrategy.SingleModel(),
            isActive);
    }
}
