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
public sealed class FinalizeMechanicAnalysisCommandHandlerTests
{
    private readonly Mock<IMechanicDraftRepository> _draftRepositoryMock;
    private readonly Mock<IRulebookAnalysisRepository> _analysisRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly FinalizeMechanicAnalysisCommandHandler _handler;

    public FinalizeMechanicAnalysisCommandHandlerTests()
    {
        _draftRepositoryMock = new Mock<IMechanicDraftRepository>();
        _analysisRepositoryMock = new Mock<IRulebookAnalysisRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<FinalizeMechanicAnalysisCommandHandler>>();

        _handler = new FinalizeMechanicAnalysisCommandHandler(
            _draftRepositoryMock.Object,
            _analysisRepositoryMock.Object,
            _unitOfWorkMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenDraftNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var draftId = Guid.NewGuid();
        var command = new FinalizeMechanicAnalysisCommand(draftId, Guid.NewGuid());

        _draftRepositoryMock.Setup(r => r.GetByIdAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MechanicDraft?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenAlreadyActivated_ThrowsConflictException()
    {
        // Arrange
        var draft = CreateDraftWithAcceptedSections();
        draft.MarkActivated();
        var command = new FinalizeMechanicAnalysisCommand(draft.Id, Guid.NewGuid());

        _draftRepositoryMock.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already been finalized*");
    }

    [Fact]
    public async Task Handle_WhenSummaryDraftEmpty_ThrowsConflictException()
    {
        // Arrange
        var draft = MechanicDraft.Create(Guid.NewGuid(), Guid.NewGuid(), "Catan", Guid.NewGuid());
        draft.AcceptDraft("mechanics", "[\"Worker Placement\"]");
        var command = new FinalizeMechanicAnalysisCommand(draft.Id, Guid.NewGuid());

        _draftRepositoryMock.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Summary draft must be accepted*");
    }

    [Fact]
    public async Task Handle_WhenMechanicsDraftEmpty_ThrowsConflictException()
    {
        // Arrange
        var draft = MechanicDraft.Create(Guid.NewGuid(), Guid.NewGuid(), "Catan", Guid.NewGuid());
        draft.AcceptDraft("summary", "A game about trading and building");
        var command = new FinalizeMechanicAnalysisCommand(draft.Id, Guid.NewGuid());

        _draftRepositoryMock.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Mechanics draft must be accepted*");
    }

    [Fact]
    public async Task Handle_WithValidDraft_CreatesRulebookAnalysis()
    {
        // Arrange
        var draft = CreateDraftWithAcceptedSections();
        var userId = Guid.NewGuid();
        var command = new FinalizeMechanicAnalysisCommand(draft.Id, userId);

        _draftRepositoryMock.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.GameTitle.Should().Be("Catan");
        result.Summary.Should().Be("A game about trading and building settlements.");
        result.IsActive.Should().BeTrue();
        result.Source.Should().Be(GenerationSource.Manual);
        result.CreatedBy.Should().Be(userId);

        _analysisRepositoryMock.Verify(r => r.DeactivateAllAsync(
            draft.SharedGameId, draft.PdfDocumentId, It.IsAny<CancellationToken>()), Times.Once);
        _analysisRepositoryMock.Verify(r => r.AddAsync(
            It.IsAny<RulebookAnalysis>(), It.IsAny<CancellationToken>()), Times.Once);
        _draftRepositoryMock.Verify(r => r.Update(draft), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MarksDraftAsActivated()
    {
        // Arrange
        var draft = CreateDraftWithAcceptedSections();
        var command = new FinalizeMechanicAnalysisCommand(draft.Id, Guid.NewGuid());

        _draftRepositoryMock.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        draft.Status.Should().Be(MechanicDraftStatus.Activated);
    }

    [Fact]
    public async Task Handle_ParsesKeyMechanicsFromJson()
    {
        // Arrange
        var draft = CreateDraftWithAcceptedSections();
        var command = new FinalizeMechanicAnalysisCommand(draft.Id, Guid.NewGuid());

        _draftRepositoryMock.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.KeyMechanics.Should().Contain("Worker Placement");
        result.KeyMechanics.Should().Contain("Resource Trading");
    }

    private static MechanicDraft CreateDraftWithAcceptedSections()
    {
        var draft = MechanicDraft.Create(Guid.NewGuid(), Guid.NewGuid(), "Catan", Guid.NewGuid());
        draft.AcceptDraft("summary", "A game about trading and building settlements.");
        draft.AcceptDraft("mechanics", "[\"Worker Placement\",\"Resource Trading\"]");
        return draft;
    }
}
