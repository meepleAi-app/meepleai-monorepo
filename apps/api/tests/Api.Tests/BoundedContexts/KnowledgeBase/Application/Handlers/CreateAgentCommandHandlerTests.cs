using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for CreateAgentCommandHandler.
/// Tests the complete agent creation flow including validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateAgentCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _mockAgentRepo;
    private readonly MeepleAiDbContext _db;
    private readonly Mock<ILogger<CreateAgentCommandHandler>> _mockLogger;
    private readonly CreateAgentCommandHandler _handler;

    public CreateAgentCommandHandlerTests()
    {
        _mockAgentRepo = new Mock<IAgentRepository>();
        _mockLogger = new Mock<ILogger<CreateAgentCommandHandler>>();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);

        _handler = new CreateAgentCommandHandler(
            _mockAgentRepo.Object,
            _db,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidConfiguration_CreatesAgentSuccessfully()
    {
        // Arrange
        var command = new CreateAgentCommand(
            Name: "Test Agent",
            AgentType: "RAG",
            StrategyName: "CustomStrategy",
            StrategyParameters: new Dictionary<string, object> { ["TopK"] = 10 },
            IsActive: true
        );

        _mockAgentRepo
            .Setup(r => r.ExistsAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockAgentRepo
            .Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Agent", result.Name);
        Assert.Equal("RAG", result.Type);
        Assert.Equal("CustomStrategy", result.StrategyName);
        Assert.True(result.IsActive);
        Assert.NotEqual(Guid.Empty, result.Id);

        _mockAgentRepo.Verify(r => r.ExistsAsync(command.Name, It.IsAny<CancellationToken>()), Times.Once);
        _mockAgentRepo.Verify(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new CreateAgentCommand(
            Name: "Existing Agent",
            AgentType: "RAG",
            StrategyName: "Strategy",
            StrategyParameters: new Dictionary<string, object>()
        );

        _mockAgentRepo
            .Setup(r => r.ExistsAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
        _mockAgentRepo.Verify(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidAgentType_ThrowsArgumentException()
    {
        // Arrange
        var command = new CreateAgentCommand(
            Name: "Test Agent",
            AgentType: "InvalidType",
            StrategyName: "Strategy",
            StrategyParameters: new Dictionary<string, object>()
        );

        _mockAgentRepo
            .Setup(r => r.ExistsAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains("Unknown AgentType", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("RAG")]
    [InlineData("Citation")]
    [InlineData("Confidence")]
    [InlineData("RulesInterpreter")]
    [InlineData("Conversation")]
    public async Task Handle_WithValidAgentTypes_CreatesAgentSuccessfully(string agentType)
    {
        // Arrange
        var command = new CreateAgentCommand(
            Name: $"Agent {agentType}",
            AgentType: agentType,
            StrategyName: "TestStrategy",
            StrategyParameters: new Dictionary<string, object>()
        );

        _mockAgentRepo
            .Setup(r => r.ExistsAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockAgentRepo
            .Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(agentType.ToUpperInvariant(), result.Type.ToUpperInvariant());
    }

    [Fact]
    public async Task Handle_WithInactiveFlag_CreatesInactiveAgent()
    {
        // Arrange
        var command = new CreateAgentCommand(
            Name: "Inactive Agent",
            AgentType: "RAG",
            StrategyName: "Strategy",
            StrategyParameters: new Dictionary<string, object>(),
            IsActive: false
        );

        _mockAgentRepo
            .Setup(r => r.ExistsAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockAgentRepo
            .Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task Handle_WithStrategyParameters_CreatesAgentWithCorrectParameters()
    {
        // Arrange
        var strategyParams = new Dictionary<string, object>
        {
            ["TopK"] = 10,
            ["MinScore"] = 0.75,
            ["EnableCache"] = true
        };

        var command = new CreateAgentCommand(
            Name: "Parameterized Agent",
            AgentType: "RAG",
            StrategyName: "CustomRAG",
            StrategyParameters: strategyParams
        );

        _mockAgentRepo
            .Setup(r => r.ExistsAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Agent? capturedAgent = null;
        _mockAgentRepo
            .Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Callback<Agent, CancellationToken>((agent, _) => capturedAgent = agent)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("CustomRAG", result.StrategyName);
        Assert.NotNull(capturedAgent);
        Assert.Equal(3, capturedAgent.Strategy.Parameters.Count);
    }

    [Fact]
    public async Task Handle_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_SetsCreatedAtTimestamp()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow.AddSeconds(-1);

        var command = new CreateAgentCommand(
            Name: "Timestamped Agent",
            AgentType: "RAG",
            StrategyName: "Strategy",
            StrategyParameters: new Dictionary<string, object>()
        );

        _mockAgentRepo
            .Setup(r => r.ExistsAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockAgentRepo
            .Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CreatedAt >= beforeCreation);
        Assert.True(result.CreatedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task Handle_InitializesInvocationCountToZero()
    {
        // Arrange
        var command = new CreateAgentCommand(
            Name: "New Agent",
            AgentType: "RAG",
            StrategyName: "Strategy",
            StrategyParameters: new Dictionary<string, object>()
        );

        _mockAgentRepo
            .Setup(r => r.ExistsAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockAgentRepo
            .Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.InvocationCount);
        Assert.Null(result.LastInvokedAt);
    }

    [Fact]
    public async Task Handle_WithEmptyStrategyParameters_CreatesAgentSuccessfully()
    {
        // Arrange
        var command = new CreateAgentCommand(
            Name: "Minimal Agent",
            AgentType: "Citation",
            StrategyName: "BasicStrategy",
            StrategyParameters: new Dictionary<string, object>()
        );

        _mockAgentRepo
            .Setup(r => r.ExistsAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockAgentRepo
            .Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("BasicStrategy", result.StrategyName);
        Assert.Empty(result.StrategyParameters);
    }

    [Fact]
    public async Task Handle_LogsAgentCreation()
    {
        // Arrange
        var command = new CreateAgentCommand(
            Name: "Logged Agent",
            AgentType: "RAG",
            StrategyName: "Strategy",
            StrategyParameters: new Dictionary<string, object>()
        );

        _mockAgentRepo
            .Setup(r => r.ExistsAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockAgentRepo
            .Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created agent")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Constructor_WithNullAgentRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new CreateAgentCommandHandler(null!, _db, _mockLogger.Object));

        Assert.Equal("agentRepository", ex.ParamName);
    }

    [Fact]
    public async Task Constructor_WithNullDb_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new CreateAgentCommandHandler(_mockAgentRepo.Object, null!, _mockLogger.Object));

        Assert.Equal("db", ex.ParamName);
    }

    [Fact]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new CreateAgentCommandHandler(_mockAgentRepo.Object, _db, null!));

        Assert.Equal("logger", ex.ParamName);
    }

    [Theory]
    [InlineData("rag")]
    [InlineData("RAG")]
    [InlineData("Rag")]
    public async Task Handle_WithCaseInsensitiveAgentType_CreatesAgentSuccessfully(string agentType)
    {
        // Arrange
        var command = new CreateAgentCommand(
            Name: $"Case Test {agentType}",
            AgentType: agentType,
            StrategyName: "Strategy",
            StrategyParameters: new Dictionary<string, object>()
        );

        _mockAgentRepo
            .Setup(r => r.ExistsAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockAgentRepo
            .Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("RAG", result.Type);
    }

    [Fact]
    public async Task Handle_DefaultIsActiveIsTrue()
    {
        // Arrange
        var command = new CreateAgentCommand(
            Name: "Default Active Agent",
            AgentType: "RAG",
            StrategyName: "Strategy",
            StrategyParameters: new Dictionary<string, object>()
        // IsActive defaults to true in record definition
        );

        _mockAgentRepo
            .Setup(r => r.ExistsAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockAgentRepo
            .Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsActive);
    }
}
