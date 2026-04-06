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
public sealed class PhaseCommandHandlerTests
{
    private readonly Mock<IToolboxRepository> _repo = new();

    private static Toolbox CreatePhasedToolbox() =>
        Toolbox.Create("Phased Toolbox", null, ToolboxMode.Phased);

    // ── AddPhaseCommandHandler ────────────────────────────────────────────────

    [Fact]
    public void AddPhaseHandler_Constructor_ThrowsOnNullRepo()
    {
        var act = () => new AddPhaseCommandHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public async Task AddPhaseHandler_Handle_ToolboxNotFound_ThrowsNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Toolbox?)null);
        var handler = new AddPhaseCommandHandler(_repo.Object);

        var act = async () => await handler.Handle(
            new AddPhaseCommand(Guid.NewGuid(), "Setup"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AddPhaseHandler_Handle_ValidCommand_AddsPhaseAndReturnsDto()
    {
        var toolbox = CreatePhasedToolbox();
        _repo.Setup(r => r.GetByIdAsync(toolbox.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(toolbox);
        var handler = new AddPhaseCommandHandler(_repo.Object);

        var result = await handler.Handle(
            new AddPhaseCommand(toolbox.Id, "Setup Phase"), CancellationToken.None);

        result.Should().NotBeNull();
        result.Name.Should().Be("Setup Phase");
        _repo.Verify(r => r.UpdateAsync(toolbox, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── RemovePhaseCommandHandler ─────────────────────────────────────────────

    [Fact]
    public void RemovePhaseHandler_Constructor_ThrowsOnNullRepo()
    {
        var act = () => new RemovePhaseCommandHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public async Task RemovePhaseHandler_Handle_ToolboxNotFound_ThrowsNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Toolbox?)null);
        var handler = new RemovePhaseCommandHandler(_repo.Object);

        var act = async () => await handler.Handle(
            new RemovePhaseCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RemovePhaseHandler_Handle_ValidCommand_RemovesPhaseAndSaves()
    {
        var toolbox = CreatePhasedToolbox();
        var phase = toolbox.AddPhase("Phase 1");
        _repo.Setup(r => r.GetByIdAsync(toolbox.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(toolbox);
        var handler = new RemovePhaseCommandHandler(_repo.Object);

        await handler.Handle(new RemovePhaseCommand(toolbox.Id, phase.Id), CancellationToken.None);

        _repo.Verify(r => r.UpdateAsync(toolbox, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── AdvancePhaseCommandHandler ────────────────────────────────────────────

    [Fact]
    public void AdvancePhaseHandler_Constructor_ThrowsOnNullRepo()
    {
        var act = () => new AdvancePhaseCommandHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public async Task AdvancePhaseHandler_Handle_ToolboxNotFound_ThrowsNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Toolbox?)null);
        var handler = new AdvancePhaseCommandHandler(_repo.Object);

        var act = async () => await handler.Handle(
            new AdvancePhaseCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AdvancePhaseHandler_Handle_ToolboxWithPhase_AdvancesAndReturnsDto()
    {
        var toolbox = CreatePhasedToolbox();
        toolbox.AddPhase("Phase 1");
        _repo.Setup(r => r.GetByIdAsync(toolbox.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(toolbox);
        var handler = new AdvancePhaseCommandHandler(_repo.Object);

        var result = await handler.Handle(
            new AdvancePhaseCommand(toolbox.Id), CancellationToken.None);

        result.Should().NotBeNull();
        _repo.Verify(r => r.UpdateAsync(toolbox, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdvancePhaseHandler_Handle_FreeformToolbox_ThrowsInvalidOperationException()
    {
        var toolbox = Toolbox.Create("Freeform Toolbox", null, ToolboxMode.Freeform);
        _repo.Setup(r => r.GetByIdAsync(toolbox.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(toolbox);
        var handler = new AdvancePhaseCommandHandler(_repo.Object);

        var act = async () => await handler.Handle(
            new AdvancePhaseCommand(toolbox.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Toolbox>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
