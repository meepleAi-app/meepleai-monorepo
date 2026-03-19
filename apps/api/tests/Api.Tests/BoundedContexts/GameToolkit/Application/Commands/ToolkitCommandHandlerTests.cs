using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.Handlers;
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class ToolkitCommandHandlerTests
{
    private readonly Mock<IGameToolkitRepository> _repoMock;
    private readonly Mock<IUnitOfWork> _uowMock;

    public ToolkitCommandHandlerTests()
    {
        _repoMock = new Mock<IGameToolkitRepository>();
        _uowMock = new Mock<IUnitOfWork>();
    }

    // ========================================================================
    // CreateToolkitCommandHandler
    // ========================================================================

    [Fact]
    public async Task CreateToolkit_WithValidCommand_CreatesAndReturnsDto()
    {
        var handler = new CreateToolkitCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new CreateToolkitCommand(Guid.NewGuid(), "Catan Toolkit", Guid.NewGuid());

        Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit? captured = null;
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit>(), It.IsAny<CancellationToken>()))
            .Callback<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Catan Toolkit", result.Name);
        Assert.Equal(command.GameId, result.GameId);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateToolkit_WithNullCommand_ThrowsArgumentNullException()
    {
        var handler = new CreateToolkitCommandHandler(_repoMock.Object, _uowMock.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // UpdateToolkitCommandHandler
    // ========================================================================

    [Fact]
    public async Task UpdateToolkit_WhenExists_UpdatesAndReturnsDto()
    {
        var toolkit = CreateTestToolkit();
        _repoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);

        var handler = new UpdateToolkitCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new UpdateToolkitCommand(toolkit.Id, "New Name");

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal("New Name", result.Name);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateToolkit_WhenNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new UpdateToolkitCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new UpdateToolkitCommand(Guid.NewGuid(), "Name");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // PublishToolkitCommandHandler
    // ========================================================================

    [Fact]
    public async Task PublishToolkit_WhenExists_PublishesAndReturnsDto()
    {
        var toolkit = CreateTestToolkit();
        _repoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);

        var handler = new PublishToolkitCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new PublishToolkitCommand(toolkit.Id);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsPublished);
        Assert.Equal(2, result.Version);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishToolkit_WhenNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new PublishToolkitCommandHandler(_repoMock.Object, _uowMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new PublishToolkitCommand(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // AddDiceToolCommandHandler
    // ========================================================================

    [Fact]
    public async Task AddDiceTool_WhenExists_AddsDiceAndReturnsDto()
    {
        var toolkit = CreateTestToolkit();
        _repoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);

        var handler = new AddDiceToolCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new AddDiceToolCommand(toolkit.Id, "Attack", DiceType.D20, 1, null, true, "#FF0000");

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Single(result.DiceTools);
        Assert.Equal("Attack", result.DiceTools[0].Name);
        Assert.Equal(DiceType.D20, result.DiceTools[0].DiceType);
    }

    [Fact]
    public async Task AddDiceTool_WhenNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new AddDiceToolCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new AddDiceToolCommand(Guid.NewGuid(), "Dice", DiceType.D6, 1, null, true, null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // AddCounterToolCommandHandler
    // ========================================================================

    [Fact]
    public async Task AddCounterTool_WhenExists_AddsCounterAndReturnsDto()
    {
        var toolkit = CreateTestToolkit();
        _repoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);

        var handler = new AddCounterToolCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new AddCounterToolCommand(toolkit.Id, "Health", 0, 100, 50, true, "heart", "#00FF00");

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Single(result.CounterTools);
        Assert.Equal("Health", result.CounterTools[0].Name);
        Assert.Equal(50, result.CounterTools[0].DefaultValue);
    }

    [Fact]
    public async Task AddCounterTool_WhenNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new AddCounterToolCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new AddCounterToolCommand(Guid.NewGuid(), "HP", 0, 100, 0, false, null, null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // RemoveDiceToolCommandHandler
    // ========================================================================

    [Fact]
    public async Task RemoveDiceTool_WhenToolExists_RemovesAndReturnsDto()
    {
        var toolkit = CreateTestToolkit();
        toolkit.AddDiceTool(new DiceToolConfig("Attack", DiceType.D6, 1, null, true, null));
        _repoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);

        var handler = new RemoveDiceToolCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new RemoveDiceToolCommand(toolkit.Id, "Attack");

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Empty(result.DiceTools);
    }

    [Fact]
    public async Task RemoveDiceTool_WhenToolNotFound_ThrowsNotFoundException()
    {
        var toolkit = CreateTestToolkit();
        _repoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);

        var handler = new RemoveDiceToolCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new RemoveDiceToolCommand(toolkit.Id, "NonExistent");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveDiceTool_WhenToolkitNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new RemoveDiceToolCommandHandler(_repoMock.Object, _uowMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new RemoveDiceToolCommand(Guid.NewGuid(), "X"), TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // RemoveCounterToolCommandHandler
    // ========================================================================

    [Fact]
    public async Task RemoveCounterTool_WhenToolExists_RemovesAndReturnsDto()
    {
        var toolkit = CreateTestToolkit();
        toolkit.AddCounterTool(new CounterToolConfig("Health", 0, 100, 50, true, null, null));
        _repoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);

        var handler = new RemoveCounterToolCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new RemoveCounterToolCommand(toolkit.Id, "Health");

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Empty(result.CounterTools);
    }

    [Fact]
    public async Task RemoveCounterTool_WhenToolNotFound_ThrowsNotFoundException()
    {
        var toolkit = CreateTestToolkit();
        _repoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);

        var handler = new RemoveCounterToolCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new RemoveCounterToolCommand(toolkit.Id, "NonExistent");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // SetScoringTemplateCommandHandler
    // ========================================================================

    [Fact]
    public async Task SetScoringTemplate_WhenExists_SetsTemplateAndReturnsDto()
    {
        var toolkit = CreateTestToolkit();
        _repoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);

        var handler = new SetScoringTemplateCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new SetScoringTemplateCommand(toolkit.Id, ["VP", "Gold"], "VP", ScoreType.Points);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(result.ScoringTemplate);
        Assert.Equal(2, result.ScoringTemplate.Dimensions.Length);
        Assert.Equal(ScoreType.Points, result.ScoringTemplate.ScoreType);
    }

    [Fact]
    public async Task SetScoringTemplate_WhenNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new SetScoringTemplateCommandHandler(_repoMock.Object, _uowMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new SetScoringTemplateCommand(Guid.NewGuid(), ["VP"], "VP", ScoreType.Points), TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // SetTurnTemplateCommandHandler
    // ========================================================================

    [Fact]
    public async Task SetTurnTemplate_WhenExists_SetsTemplateAndReturnsDto()
    {
        var toolkit = CreateTestToolkit();
        _repoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);

        var handler = new SetTurnTemplateCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new SetTurnTemplateCommand(toolkit.Id, TurnOrderType.RoundRobin, ["Draw", "Play", "End"]);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(result.TurnTemplate);
        Assert.Equal(TurnOrderType.RoundRobin, result.TurnTemplate.TurnOrderType);
        Assert.Equal(3, result.TurnTemplate.Phases.Length);
    }

    [Fact]
    public async Task SetTurnTemplate_WhenNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new SetTurnTemplateCommandHandler(_repoMock.Object, _uowMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new SetTurnTemplateCommand(Guid.NewGuid(), TurnOrderType.Free, null), TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // Helper
    // ========================================================================

    private static Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit CreateTestToolkit()
    {
        return new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(
            Guid.NewGuid(), Guid.NewGuid(), "Test Toolkit", Guid.NewGuid());
    }
}
