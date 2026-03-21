using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.Queries;
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.Handlers;

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

        result.Should().NotBeNull();
        result.Name.Should().Be("Catan Toolkit");
        result.GameId.Should().Be(command.GameId);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateToolkit_WithNullCommand_ThrowsArgumentNullException()
    {
        var handler = new CreateToolkitCommandHandler(_repoMock.Object, _uowMock.Object);

        var act = () =>
            handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
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

        result.Name.Should().Be("New Name");
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

        var act2 = () =>
            handler.Handle(command, TestContext.Current.CancellationToken);
        await act2.Should().ThrowAsync<NotFoundException>();
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

        result.IsPublished.Should().BeTrue();
        result.Version.Should().Be(2);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishToolkit_WhenNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new PublishToolkitCommandHandler(_repoMock.Object, _uowMock.Object);

        var act3 = () =>
            handler.Handle(new PublishToolkitCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);
        await act3.Should().ThrowAsync<NotFoundException>();
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

        result.DiceTools.Should().ContainSingle();
        result.DiceTools[0].Name.Should().Be("Attack");
        result.DiceTools[0].DiceType.Should().Be(DiceType.D20);
    }

    [Fact]
    public async Task AddDiceTool_WhenNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new AddDiceToolCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new AddDiceToolCommand(Guid.NewGuid(), "Dice", DiceType.D6, 1, null, true, null);

        var act4 = () =>
            handler.Handle(command, TestContext.Current.CancellationToken);
        await act4.Should().ThrowAsync<NotFoundException>();
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

        result.CounterTools.Should().ContainSingle();
        result.CounterTools[0].Name.Should().Be("Health");
        result.CounterTools[0].DefaultValue.Should().Be(50);
    }

    [Fact]
    public async Task AddCounterTool_WhenNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new AddCounterToolCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new AddCounterToolCommand(Guid.NewGuid(), "HP", 0, 100, 0, false, null, null);

        var act5 = () =>
            handler.Handle(command, TestContext.Current.CancellationToken);
        await act5.Should().ThrowAsync<NotFoundException>();
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

        result.DiceTools.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveDiceTool_WhenToolNotFound_ThrowsNotFoundException()
    {
        var toolkit = CreateTestToolkit();
        _repoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);

        var handler = new RemoveDiceToolCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new RemoveDiceToolCommand(toolkit.Id, "NonExistent");

        var act6 = () =>
            handler.Handle(command, TestContext.Current.CancellationToken);
        await act6.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RemoveDiceTool_WhenToolkitNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new RemoveDiceToolCommandHandler(_repoMock.Object, _uowMock.Object);

        var act7 = () =>
            handler.Handle(new RemoveDiceToolCommand(Guid.NewGuid(), "X"), TestContext.Current.CancellationToken);
        await act7.Should().ThrowAsync<NotFoundException>();
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

        result.CounterTools.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveCounterTool_WhenToolNotFound_ThrowsNotFoundException()
    {
        var toolkit = CreateTestToolkit();
        _repoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);

        var handler = new RemoveCounterToolCommandHandler(_repoMock.Object, _uowMock.Object);
        var command = new RemoveCounterToolCommand(toolkit.Id, "NonExistent");

        var act8 = () =>
            handler.Handle(command, TestContext.Current.CancellationToken);
        await act8.Should().ThrowAsync<NotFoundException>();
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

        result.ScoringTemplate.Should().NotBeNull();
        result.ScoringTemplate.Dimensions.Length.Should().Be(2);
        result.ScoringTemplate.ScoreType.Should().Be(ScoreType.Points);
    }

    [Fact]
    public async Task SetScoringTemplate_WhenNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new SetScoringTemplateCommandHandler(_repoMock.Object, _uowMock.Object);

        var act9 = () =>
            handler.Handle(new SetScoringTemplateCommand(Guid.NewGuid(), ["VP"], "VP", ScoreType.Points), TestContext.Current.CancellationToken);
        await act9.Should().ThrowAsync<NotFoundException>();
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

        result.TurnTemplate.Should().NotBeNull();
        result.TurnTemplate.TurnOrderType.Should().Be(TurnOrderType.RoundRobin);
        result.TurnTemplate.Phases.Length.Should().Be(3);
    }

    [Fact]
    public async Task SetTurnTemplate_WhenNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new SetTurnTemplateCommandHandler(_repoMock.Object, _uowMock.Object);

        var act10 = () =>
            handler.Handle(new SetTurnTemplateCommand(Guid.NewGuid(), TurnOrderType.Free, null), TestContext.Current.CancellationToken);
        await act10.Should().ThrowAsync<NotFoundException>();
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
