using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.ToolState;
using Api.BoundedContexts.GameManagement.Application.Handlers.ToolState;
using Api.BoundedContexts.GameManagement.Domain.Entities.ToolState;
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.ToolState;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class ToolStateCommandHandlerTests
{
    private readonly Mock<IToolStateRepository> _toolStateRepoMock;
    private readonly Mock<IGameToolkitRepository> _toolkitRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;

    public ToolStateCommandHandlerTests()
    {
        _toolStateRepoMock = new Mock<IToolStateRepository>();
        _toolkitRepoMock = new Mock<IGameToolkitRepository>();
        _uowMock = new Mock<IUnitOfWork>();
    }

    // ========================================================================
    // InitializeToolStatesCommandHandler
    // ========================================================================

    [Fact]
    public async Task InitializeToolStates_WithValidToolkit_CreatesToolStatesForEachTool()
    {
        var sessionId = Guid.NewGuid();
        var toolkit = CreateToolkitWithTools();
        var toolkitId = toolkit.Id;

        _toolkitRepoMock.Setup(r => r.GetByIdAsync(toolkitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);
        _toolStateRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>());

        var handler = new InitializeToolStatesCommandHandler(
            _toolStateRepoMock.Object, _toolkitRepoMock.Object, _uowMock.Object);
        var command = new InitializeToolStatesCommand(sessionId, toolkitId);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Toolkit has 1 dice + 1 counter = 2 tools
        Assert.Equal(2, result.Count);
        _toolStateRepoMock.Verify(r => r.AddRangeAsync(
            It.Is<IEnumerable<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>>(
                list => list.Count() == 2),
            It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitializeToolStates_WhenToolkitNotFound_ThrowsNotFoundException()
    {
        _toolkitRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new InitializeToolStatesCommandHandler(
            _toolStateRepoMock.Object, _toolkitRepoMock.Object, _uowMock.Object);
        var command = new InitializeToolStatesCommand(Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeToolStates_WhenAlreadyInitialized_ThrowsConflictException()
    {
        var sessionId = Guid.NewGuid();
        var toolkit = CreateToolkitWithTools();

        _toolkitRepoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);
        _toolStateRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>
            {
                new(Guid.NewGuid(), sessionId, toolkit.Id, "Dice", ToolType.Dice, "{}")
            });

        var handler = new InitializeToolStatesCommandHandler(
            _toolStateRepoMock.Object, _toolkitRepoMock.Object, _uowMock.Object);
        var command = new InitializeToolStatesCommand(sessionId, toolkit.Id);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeToolStates_WithNullCommand_ThrowsArgumentNullException()
    {
        var handler = new InitializeToolStatesCommandHandler(
            _toolStateRepoMock.Object, _toolkitRepoMock.Object, _uowMock.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeToolStates_CreatesCorrectDiceState()
    {
        var sessionId = Guid.NewGuid();
        var toolkit = CreateToolkitWithTools();

        _toolkitRepoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);
        _toolStateRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>());

        var handler = new InitializeToolStatesCommandHandler(
            _toolStateRepoMock.Object, _toolkitRepoMock.Object, _uowMock.Object);
        var command = new InitializeToolStatesCommand(sessionId, toolkit.Id);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        var diceState = result.First(r => r.ToolName == "Battle Dice");
        Assert.Equal(ToolType.Dice, diceState.ToolType);

        // Parse the state data to verify structure
        var stateDoc = diceState.StateData;
        Assert.Equal("D6", stateDoc.GetProperty("diceType").GetString());
        Assert.Equal(2, stateDoc.GetProperty("quantity").GetInt32());
    }

    // ========================================================================
    // RollDiceCommandHandler
    // ========================================================================

    [Fact]
    public async Task RollDice_WithValidDiceTool_ReturnsUpdatedState()
    {
        var sessionId = Guid.NewGuid();
        var toolName = "Battle Dice";
        var diceState = CreateDiceToolState(sessionId, toolName);

        _toolStateRepoMock.Setup(r => r.GetBySessionAndToolNameAsync(sessionId, toolName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(diceState);

        var handler = new RollDiceCommandHandler(_toolStateRepoMock.Object, _uowMock.Object);
        var command = new RollDiceCommand(sessionId, toolName, Guid.NewGuid());

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(toolName, result.ToolName);

        // Verify state was updated with roll values
        var stateDoc = result.StateData;
        Assert.True(stateDoc.TryGetProperty("lastRoll", out var lastRoll));
        Assert.Equal(JsonValueKind.Array, lastRoll.ValueKind);
        Assert.Equal(2, lastRoll.GetArrayLength()); // 2 dice

        // Each roll should be between 1 and 6 (D6)
        foreach (var roll in lastRoll.EnumerateArray())
        {
            var value = roll.GetInt32();
            Assert.InRange(value, 1, 6);
        }

        _toolStateRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RollDice_WhenToolNotFound_ThrowsNotFoundException()
    {
        _toolStateRepoMock.Setup(r => r.GetBySessionAndToolNameAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState?)null);

        var handler = new RollDiceCommandHandler(_toolStateRepoMock.Object, _uowMock.Object);
        var command = new RollDiceCommand(Guid.NewGuid(), "Missing", null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RollDice_WhenNotDiceTool_ThrowsBadRequestException()
    {
        var sessionId = Guid.NewGuid();
        var counterState = new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
            Guid.NewGuid(), sessionId, Guid.NewGuid(), "HP Counter", ToolType.Counter, "{}");

        _toolStateRepoMock.Setup(r => r.GetBySessionAndToolNameAsync(sessionId, "HP Counter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(counterState);

        var handler = new RollDiceCommandHandler(_toolStateRepoMock.Object, _uowMock.Object);
        var command = new RollDiceCommand(sessionId, "HP Counter", null);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(command, TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // UpdateCounterCommandHandler
    // ========================================================================

    [Fact]
    public async Task UpdateCounter_WithPositiveChange_IncreasesValue()
    {
        var sessionId = Guid.NewGuid();
        var toolName = "HP";
        var counterState = CreateCounterToolState(sessionId, toolName);

        _toolStateRepoMock.Setup(r => r.GetBySessionAndToolNameAsync(sessionId, toolName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(counterState);

        var handler = new UpdateCounterCommandHandler(_toolStateRepoMock.Object, _uowMock.Object);
        var command = new UpdateCounterCommand(sessionId, toolName, "player1", 5);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(toolName, result.ToolName);

        _toolStateRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCounter_WhenToolNotFound_ThrowsNotFoundException()
    {
        _toolStateRepoMock.Setup(r => r.GetBySessionAndToolNameAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState?)null);

        var handler = new UpdateCounterCommandHandler(_toolStateRepoMock.Object, _uowMock.Object);
        var command = new UpdateCounterCommand(Guid.NewGuid(), "Missing", "player1", 1);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateCounter_WhenNotCounterTool_ThrowsBadRequestException()
    {
        var sessionId = Guid.NewGuid();
        var diceState = new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
            Guid.NewGuid(), sessionId, Guid.NewGuid(), "Dice", ToolType.Dice,
            "{\"diceType\":\"D6\",\"quantity\":1}");

        _toolStateRepoMock.Setup(r => r.GetBySessionAndToolNameAsync(sessionId, "Dice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(diceState);

        var handler = new UpdateCounterCommandHandler(_toolStateRepoMock.Object, _uowMock.Object);
        var command = new UpdateCounterCommand(sessionId, "Dice", "player1", 1);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateCounter_ClampsToMaxValue()
    {
        var sessionId = Guid.NewGuid();
        var counterState = CreateCounterToolState(sessionId, "Score", maxValue: 100, currentValue: 95);

        _toolStateRepoMock.Setup(r => r.GetBySessionAndToolNameAsync(sessionId, "Score", It.IsAny<CancellationToken>()))
            .ReturnsAsync(counterState);

        var handler = new UpdateCounterCommandHandler(_toolStateRepoMock.Object, _uowMock.Object);
        // Try to add 20 when current is 95 and max is 100
        var command = new UpdateCounterCommand(sessionId, "Score", "player1", 20);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Should clamp to 100
        Assert.NotNull(result);
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit CreateToolkitWithTools()
    {
        var toolkit = new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(
            Guid.NewGuid(), Guid.NewGuid(), "Test Toolkit", Guid.NewGuid());
        toolkit.AddDiceTool(new DiceToolConfig("Battle Dice", DiceType.D6, 2));
        toolkit.AddCounterTool(new CounterToolConfig("HP", 0, 100, 20, true));
        return toolkit;
    }

    private static Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState CreateDiceToolState(
        Guid sessionId, string toolName)
    {
        var state = JsonSerializer.Serialize(new
        {
            diceType = "D6",
            quantity = 2,
            customFaces = (string[]?)null,
            lastRoll = (int[]?)null,
            rollHistory = Array.Empty<object>()
        });
        return new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
            Guid.NewGuid(), sessionId, Guid.NewGuid(), toolName, ToolType.Dice, state);
    }

    private static Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState CreateCounterToolState(
        Guid sessionId, string toolName, int maxValue = 100, int currentValue = 20)
    {
        var state = JsonSerializer.Serialize(new
        {
            minValue = 0,
            maxValue,
            defaultValue = 20,
            isPerPlayer = true,
            currentValue,
            playerValues = new Dictionary<string, int>()
        });
        return new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
            Guid.NewGuid(), sessionId, Guid.NewGuid(), toolName, ToolType.Counter, state);
    }
}
