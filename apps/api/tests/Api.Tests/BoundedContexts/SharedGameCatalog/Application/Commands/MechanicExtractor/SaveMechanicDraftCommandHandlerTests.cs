using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SaveMechanicDraftCommandHandlerTests
{
    private readonly Mock<IMechanicDraftRepository> _draftRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly SaveMechanicDraftCommandHandler _handler;

    public SaveMechanicDraftCommandHandlerTests()
    {
        _draftRepositoryMock = new Mock<IMechanicDraftRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<SaveMechanicDraftCommandHandler>>();

        _handler = new SaveMechanicDraftCommandHandler(
            _draftRepositoryMock.Object,
            _unitOfWorkMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNoDraftExists_CreatesNewDraft()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new SaveMechanicDraftCommand(
            gameId, pdfId, "Catan", userId,
            "Summary notes", "Mechanics notes", "Victory notes",
            "Resources notes", "Phases notes", "Questions notes");

        _draftRepositoryMock.Setup(r => r.GetDraftForGameAsync(gameId, pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MechanicDraft?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SharedGameId.Should().Be(gameId);
        result.PdfDocumentId.Should().Be(pdfId);
        result.GameTitle.Should().Be("Catan");
        result.CreatedBy.Should().Be(userId);
        result.SummaryNotes.Should().Be("Summary notes");
        result.MechanicsNotes.Should().Be("Mechanics notes");
        result.Status.Should().Be(MechanicDraftStatus.Draft.ToString());

        _draftRepositoryMock.Verify(r => r.AddAsync(It.IsAny<MechanicDraft>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDraftExists_UpdatesExistingDraft()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingDraft = MechanicDraft.Create(gameId, pdfId, "Catan", userId);

        var command = new SaveMechanicDraftCommand(
            gameId, pdfId, "Catan", userId,
            "Updated summary", "Updated mechanics", "", "", "", "");

        _draftRepositoryMock.Setup(r => r.GetDraftForGameAsync(gameId, pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDraft);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SummaryNotes.Should().Be("Updated summary");
        result.MechanicsNotes.Should().Be("Updated mechanics");

        _draftRepositoryMock.Verify(r => r.Update(It.IsAny<MechanicDraft>()), Times.Once);
        _draftRepositoryMock.Verify(r => r.AddAsync(It.IsAny<MechanicDraft>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsMappedDto()
    {
        // Arrange
        var command = new SaveMechanicDraftCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Wingspan", Guid.NewGuid(),
            "s", "m", "v", "r", "p", "q");

        _draftRepositoryMock.Setup(r => r.GetDraftForGameAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MechanicDraft?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Id.Should().NotBe(Guid.Empty);
        result.GameTitle.Should().Be("Wingspan");
        result.VictoryNotes.Should().Be("v");
        result.ResourcesNotes.Should().Be("r");
        result.PhasesNotes.Should().Be("p");
        result.QuestionsNotes.Should().Be("q");
    }
}
