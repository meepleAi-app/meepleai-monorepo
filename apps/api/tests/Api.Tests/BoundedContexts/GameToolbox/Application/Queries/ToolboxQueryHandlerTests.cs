using Api.BoundedContexts.GameToolbox.Application.Queries;
using Api.BoundedContexts.GameToolbox.Domain.Entities;
using Api.BoundedContexts.GameToolbox.Domain.Repositories;
using Api.BoundedContexts.GameToolbox.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolbox.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolbox")]
public sealed class ToolboxQueryHandlerTests
{
    private readonly Mock<IToolboxRepository> _repo = new();
    private readonly Mock<IToolboxTemplateRepository> _templateRepo = new();

    // ── GetToolboxQueryHandler ────────────────────────────────────────────────

    [Fact]
    public void GetToolboxHandler_Constructor_ThrowsOnNullRepo()
    {
        var act = () => new GetToolboxQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public async Task GetToolboxHandler_Handle_ExistingId_ReturnsDto()
    {
        var toolbox = Toolbox.Create("Test", null);
        _repo.Setup(r => r.GetByIdAsync(toolbox.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(toolbox);
        var handler = new GetToolboxQueryHandler(_repo.Object);

        var result = await handler.Handle(new GetToolboxQuery(toolbox.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(toolbox.Id);
    }

    [Fact]
    public async Task GetToolboxHandler_Handle_NonExistingId_ReturnsNull()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Toolbox?)null);
        var handler = new GetToolboxQueryHandler(_repo.Object);

        var result = await handler.Handle(new GetToolboxQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    // ── GetToolboxByGameQueryHandler ──────────────────────────────────────────

    [Fact]
    public void GetToolboxByGameHandler_Constructor_ThrowsOnNullRepo()
    {
        var act = () => new GetToolboxByGameQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public async Task GetToolboxByGameHandler_Handle_ExistingGameId_ReturnsDto()
    {
        var gameId = Guid.NewGuid();
        var toolbox = Toolbox.Create("Game Toolbox", gameId);
        _repo.Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(toolbox);
        var handler = new GetToolboxByGameQueryHandler(_repo.Object);

        var result = await handler.Handle(new GetToolboxByGameQuery(gameId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.GameId.Should().Be(gameId);
    }

    [Fact]
    public async Task GetToolboxByGameHandler_Handle_NoToolboxForGame_ReturnsNull()
    {
        _repo.Setup(r => r.GetByGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Toolbox?)null);
        var handler = new GetToolboxByGameQueryHandler(_repo.Object);

        var result = await handler.Handle(new GetToolboxByGameQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    // ── GetToolboxTemplatesQueryHandler ───────────────────────────────────────

    [Fact]
    public void GetToolboxTemplatesHandler_Constructor_ThrowsOnNullRepo()
    {
        var act = () => new GetToolboxTemplatesQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public async Task GetToolboxTemplatesHandler_Handle_NoGameId_ReturnsAllTemplates()
    {
        _templateRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new List<ToolboxTemplate>());
        var handler = new GetToolboxTemplatesQueryHandler(_templateRepo.Object);

        var result = await handler.Handle(new GetToolboxTemplatesQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        _templateRepo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
