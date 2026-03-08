using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class AcceptMechanicDraftCommandHandlerTests
{
    private readonly Mock<IMechanicDraftRepository> _draftRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AcceptMechanicDraftCommandHandler _handler;

    public AcceptMechanicDraftCommandHandlerTests()
    {
        _draftRepositoryMock = new Mock<IMechanicDraftRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<AcceptMechanicDraftCommandHandler>>();

        _handler = new AcceptMechanicDraftCommandHandler(
            _draftRepositoryMock.Object,
            _unitOfWorkMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidDraft_AcceptsDraftAndReturnsDto()
    {
        // Arrange
        var draftId = Guid.NewGuid();
        var draft = MechanicDraft.Create(Guid.NewGuid(), Guid.NewGuid(), "Catan", Guid.NewGuid());
        var command = new AcceptMechanicDraftCommand(draft.Id, "summary", "AI-generated summary text");

        _draftRepositoryMock.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SummaryDraft.Should().Be("AI-generated summary text");
        _draftRepositoryMock.Verify(r => r.Update(draft), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDraftNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var draftId = Guid.NewGuid();
        var command = new AcceptMechanicDraftCommand(draftId, "summary", "content");

        _draftRepositoryMock.Setup(r => r.GetByIdAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MechanicDraft?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AcceptsMechanicsDraft()
    {
        // Arrange
        var draft = MechanicDraft.Create(Guid.NewGuid(), Guid.NewGuid(), "Wingspan", Guid.NewGuid());
        var jsonDraft = "[\"Worker Placement\",\"Engine Building\"]";
        var command = new AcceptMechanicDraftCommand(draft.Id, "mechanics", jsonDraft);

        _draftRepositoryMock.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.MechanicsDraft.Should().Be(jsonDraft);
    }
}
