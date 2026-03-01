using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Unit tests for AutoCreateAgentOnPdfReadyHandler.
/// Issue #4673: Verifies auto-agent creation trigger conditions and guard clauses.
///
/// Tests:
/// - Non-Ready state → early return, no mediator call
/// - PDF not found in DB → early return
/// - PDF not admin priority → early return
/// - No approved typology → early return
/// - User not found in DB → early return
/// - All conditions met → CreateGameAgentCommand dispatched
/// - Exception in CreateGameAgent → caught, not propagated
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AutoCreateAgentOnPdfReadyHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IAgentTypologyRepository> _mockTypologyRepo;
    private readonly Mock<IMediator> _mockMediator;
    private readonly AutoCreateAgentOnPdfReadyHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();
    private static readonly Guid TypologyId = Guid.NewGuid();

    public AutoCreateAgentOnPdfReadyHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockTypologyRepo = new Mock<IAgentTypologyRepository>();
        _mockMediator = new Mock<IMediator>();

        _handler = new AutoCreateAgentOnPdfReadyHandler(
            _dbContext,
            _mockTypologyRepo.Object,
            _mockMediator.Object,
            NullLogger<AutoCreateAgentOnPdfReadyHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private static PdfStateChangedEvent CreateReadyEvent(Guid pdfId) =>
        new(pdfId, PdfProcessingState.Indexing, PdfProcessingState.Ready, UserId);

    private static PdfStateChangedEvent CreateNonReadyEvent(Guid pdfId) =>
        new(pdfId, PdfProcessingState.Pending, PdfProcessingState.Extracting, UserId);

    private AgentTypology CreateApprovedTypology() =>
        new(TypologyId, "Default Agent", "A general-purpose agent", "System prompt", AgentStrategy.SingleModel(), Guid.NewGuid(), TypologyStatus.Approved);

    private void SetupApprovedTypology() =>
        _mockTypologyRepo
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateApprovedTypology()]);

    private void SetupNoApprovedTypology() =>
        _mockTypologyRepo
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

    private async Task<Guid> AddAdminPdfAsync()
    {
        var pdfId = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = GameId,
            FileName = "rulebook.pdf",
            FilePath = "/uploads/rulebook.pdf",
            UploadedByUserId = UserId,
            ProcessingPriority = "Admin",
        });
        await _dbContext.SaveChangesAsync();
        return pdfId;
    }

    private async Task<Guid> AddNormalPdfAsync()
    {
        var pdfId = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = GameId,
            FileName = "rulebook.pdf",
            FilePath = "/uploads/rulebook.pdf",
            UploadedByUserId = UserId,
            ProcessingPriority = "Normal",
        });
        await _dbContext.SaveChangesAsync();
        return pdfId;
    }

    private async Task AddUserAsync()
    {
        _dbContext.Users.Add(new UserEntity
        {
            Id = UserId,
            Email = "admin@example.com",
            Tier = "premium",
            Role = "admin",
        });
        await _dbContext.SaveChangesAsync();
    }

    private static CreateGameAgentResult CreateFakeAgentResult() =>
        new()
        {
            LibraryEntryId = Guid.NewGuid(),
            GameId = GameId,
            Status = "active",
            Typology = new AgentTypologyInfo { Id = TypologyId, Name = "Default Agent" },
            Strategy = new AgentStrategyInfo { Name = "Balanced", Parameters = null },
        };

    // ────────────────────────────────────────────────────────────────────────
    // Guard: non-Ready state
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenStateIsNotReady_ReturnsWithoutAnyWork()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var evt = CreateNonReadyEvent(pdfId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert — no DB access, no mediator call
        _mockTypologyRepo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockMediator.Verify(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(PdfProcessingState.Pending)]
    [InlineData(PdfProcessingState.Uploading)]
    [InlineData(PdfProcessingState.Extracting)]
    [InlineData(PdfProcessingState.Indexing)]
    [InlineData(PdfProcessingState.Failed)]
    public async Task Handle_WhenStateIsNotReady_AllNonReadyStatesSkipped(PdfProcessingState nonReadyState)
    {
        // Arrange
        var evt = new PdfStateChangedEvent(Guid.NewGuid(), PdfProcessingState.Pending, nonReadyState, UserId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _mockMediator.Verify(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Guard: PDF not in database
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenPdfNotFound_ReturnsWithoutCreatingAgent()
    {
        // Arrange — DB is empty, no PDF
        var evt = CreateReadyEvent(Guid.NewGuid());

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _mockTypologyRepo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockMediator.Verify(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Guard: non-admin processing priority
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenPdfIsNormalPriority_SkipsAutoAgentCreation()
    {
        // Arrange
        var pdfId = await AddNormalPdfAsync();
        var evt = CreateReadyEvent(pdfId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert — typology lookup never reached
        _mockTypologyRepo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockMediator.Verify(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Guard: no approved typology exists
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoApprovedTypologyExists_SkipsAutoAgentCreation()
    {
        // Arrange
        var pdfId = await AddAdminPdfAsync();
        SetupNoApprovedTypology();
        var evt = CreateReadyEvent(pdfId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert — mediator never called even though PDF is admin priority
        _mockMediator.Verify(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOnlyDraftTypologiesExist_SkipsAutoAgentCreation()
    {
        // Arrange
        var pdfId = await AddAdminPdfAsync();
        var draftTypology = new AgentTypology(Guid.NewGuid(), "Draft", "Desc", "Prompt", AgentStrategy.SingleModel(), Guid.NewGuid(), TypologyStatus.Draft);
        _mockTypologyRepo
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([draftTypology]);

        var evt = CreateReadyEvent(pdfId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _mockMediator.Verify(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Guard: user not found for tier/role lookup
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserNotFoundInDatabase_SkipsAutoAgentCreation()
    {
        // Arrange
        var pdfId = await AddAdminPdfAsync();
        SetupApprovedTypology();
        // User NOT added to DB
        var evt = CreateReadyEvent(pdfId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert — mediator never called (user lookup failed)
        _mockMediator.Verify(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Happy path: all conditions met → CreateGameAgentCommand dispatched
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAllConditionsMet_SendsCreateGameAgentCommand()
    {
        // Arrange
        var pdfId = await AddAdminPdfAsync();
        await AddUserAsync();
        SetupApprovedTypology();

        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFakeAgentResult());

        var evt = CreateReadyEvent(pdfId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _mockMediator.Verify(
            m => m.Send(
                It.Is<CreateGameAgentCommand>(c =>
                    c.GameId == GameId &&
                    c.TypologyId == TypologyId &&
                    c.StrategyName == "Balanced" &&
                    c.UserId == UserId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAllConditionsMet_PassesUserTierAndRole()
    {
        // Arrange
        var pdfId = await AddAdminPdfAsync();
        await AddUserAsync(); // Tier="premium", Role="admin"
        SetupApprovedTypology();

        CreateGameAgentCommand? capturedCommand = null;
        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CreateGameAgentResult>, CancellationToken>((cmd, _) =>
                capturedCommand = (CreateGameAgentCommand)cmd)
            .ReturnsAsync(CreateFakeAgentResult());

        var evt = CreateReadyEvent(pdfId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        capturedCommand.Should().NotBeNull();
        capturedCommand!.UserTier.Should().Be("premium");
        capturedCommand.UserRole.Should().Be("admin");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Exception resilience: error in CreateGameAgentCommand must not propagate
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCreateAgentThrows_ExceptionIsCaughtAndDoesNotPropagate()
    {
        // Arrange
        var pdfId = await AddAdminPdfAsync();
        await AddUserAsync();
        SetupApprovedTypology();

        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Quota exceeded"));

        var evt = CreateReadyEvent(pdfId);

        // Act & Assert — exception must be swallowed so the PDF pipeline is not broken
        await _handler.Invoking(h => h.Handle(evt, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WhenCreateAgentThrowsUnexpectedException_DoesNotPropagate()
    {
        // Arrange
        var pdfId = await AddAdminPdfAsync();
        await AddUserAsync();
        SetupApprovedTypology();

        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OutOfMemoryException("Simulated OOM"));

        var evt = CreateReadyEvent(pdfId);

        // Act & Assert
        await _handler.Invoking(h => h.Handle(evt, CancellationToken.None))
            .Should().NotThrowAsync();
    }
}
