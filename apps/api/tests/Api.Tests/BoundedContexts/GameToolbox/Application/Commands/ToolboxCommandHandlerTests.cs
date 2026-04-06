using Api.BoundedContexts.GameToolbox.Application.Commands;
using Api.BoundedContexts.GameToolbox.Domain.Entities;
using Api.BoundedContexts.GameToolbox.Domain.Repositories;
using Api.BoundedContexts.GameToolbox.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolbox.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolbox")]
public sealed class ToolboxCommandHandlerTests
{
    private readonly Mock<IToolboxRepository> _repo = new();

    // ── CreateToolboxCommandHandler ───────────────────────────────────────────

    [Fact]
    public void CreateToolboxHandler_Constructor_ThrowsOnNullRepo()
    {
        var act = () => new CreateToolboxCommandHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public async Task CreateToolboxHandler_Handle_NullCommand_ThrowsArgumentNullException()
    {
        var handler = new CreateToolboxCommandHandler(_repo.Object);
        var act = async () => await handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateToolboxHandler_Handle_ValidCommand_AddsAndReturnsDto()
    {
        // Arrange
        var handler = new CreateToolboxCommandHandler(_repo.Object);
        var cmd = new CreateToolboxCommand("Catan Toolbox", Guid.NewGuid(), "Freeform");

        // Act
        var result = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Catan Toolbox");
        _repo.Verify(r => r.AddAsync(It.IsAny<Toolbox>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateToolboxHandler_Handle_PhasedMode_CreatesWithPhasedMode()
    {
        var handler = new CreateToolboxCommandHandler(_repo.Object);
        var cmd = new CreateToolboxCommand("Phased Toolbox", null, "Phased");

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.Mode.Should().Be("Phased");
    }

    // ── UpdateToolboxModeCommandHandler ──────────────────────────────────────

    [Fact]
    public void UpdateToolboxModeHandler_Constructor_ThrowsOnNullRepo()
    {
        var act = () => new UpdateToolboxModeCommandHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public async Task UpdateToolboxModeHandler_Handle_NullCommand_ThrowsArgumentNullException()
    {
        var handler = new UpdateToolboxModeCommandHandler(_repo.Object);
        var act = async () => await handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateToolboxModeHandler_Handle_ToolboxNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Toolbox?)null);
        var handler = new UpdateToolboxModeCommandHandler(_repo.Object);

        // Act
        var act = async () => await handler.Handle(
            new UpdateToolboxModeCommand(Guid.NewGuid(), "Phased"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateToolboxModeHandler_Handle_ExistingToolbox_UpdatesModeAndReturnsDto()
    {
        // Arrange
        var toolbox = Toolbox.Create("Old Name", null, ToolboxMode.Freeform);
        _repo.Setup(r => r.GetByIdAsync(toolbox.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(toolbox);
        var handler = new UpdateToolboxModeCommandHandler(_repo.Object);

        // Act
        var result = await handler.Handle(
            new UpdateToolboxModeCommand(toolbox.Id, "Phased"), CancellationToken.None);

        // Assert
        result.Mode.Should().Be("Phased");
        _repo.Verify(r => r.UpdateAsync(toolbox, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── AddToolToToolboxCommandHandler ────────────────────────────────────────

    [Fact]
    public void AddToolHandler_Constructor_ThrowsOnNullRepo()
    {
        var act = () => new AddToolToToolboxCommandHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public async Task AddToolHandler_Handle_ToolboxNotFound_ThrowsNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Toolbox?)null);
        var handler = new AddToolToToolboxCommandHandler(_repo.Object);

        var act = async () => await handler.Handle(
            new AddToolToToolboxCommand(Guid.NewGuid(), "DiceRoller"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AddToolHandler_Handle_ValidCommand_AddsTool()
    {
        // Arrange
        var toolbox = Toolbox.Create("Test Toolbox", null);
        _repo.Setup(r => r.GetByIdAsync(toolbox.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(toolbox);
        var handler = new AddToolToToolboxCommandHandler(_repo.Object);

        // Act
        var result = await handler.Handle(
            new AddToolToToolboxCommand(toolbox.Id, "DiceRoller", "{}"), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be("DiceRoller");
        _repo.Verify(r => r.UpdateAsync(toolbox, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── RemoveToolFromToolboxCommandHandler ───────────────────────────────────

    [Fact]
    public void RemoveToolHandler_Constructor_ThrowsOnNullRepo()
    {
        var act = () => new RemoveToolFromToolboxCommandHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public async Task RemoveToolHandler_Handle_ToolboxNotFound_ThrowsNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Toolbox?)null);
        var handler = new RemoveToolFromToolboxCommandHandler(_repo.Object);

        var act = async () => await handler.Handle(
            new RemoveToolFromToolboxCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RemoveToolHandler_Handle_ValidCommand_RemovesToolAndSaves()
    {
        // Arrange
        var toolbox = Toolbox.Create("Test Toolbox", null);
        var tool = toolbox.AddTool("DiceRoller", "{}");
        _repo.Setup(r => r.GetByIdAsync(toolbox.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(toolbox);
        var handler = new RemoveToolFromToolboxCommandHandler(_repo.Object);

        // Act
        await handler.Handle(new RemoveToolFromToolboxCommand(toolbox.Id, tool.Id), CancellationToken.None);

        // Assert
        _repo.Verify(r => r.UpdateAsync(toolbox, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
