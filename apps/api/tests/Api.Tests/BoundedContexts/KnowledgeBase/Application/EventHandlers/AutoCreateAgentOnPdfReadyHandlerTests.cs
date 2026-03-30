using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using AgentDef = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;
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
/// - No active system definition → early return
/// - User not found in DB → early return
/// - All conditions met → CreateGameAgentCommand dispatched
/// - Exception in CreateGameAgent → caught, not propagated
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AutoCreateAgentOnPdfReadyHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IAgentDefinitionRepository> _mockDefinitionRepo;
    private readonly Mock<IMediator> _mockMediator;
    private readonly AutoCreateAgentOnPdfReadyHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();
    private AgentDef? _activeDefinition;

    public AutoCreateAgentOnPdfReadyHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockDefinitionRepo = new Mock<IAgentDefinitionRepository>();
        _mockMediator = new Mock<IMediator>();

        _handler = new AutoCreateAgentOnPdfReadyHandler(
            _dbContext,
            _mockDefinitionRepo.Object,
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

    private AgentDef CreateActiveSystemDefinition()
    {
        var def = AgentDef.Create(
            "Default Agent",
            "A general-purpose agent",
            AgentType.RulesInterpreter,
            AgentDefinitionConfig.Default());
        def.Activate();
        // Mark as system-defined via reflection (readonly backing field)
        var field = typeof(AgentDef).GetField("_isSystemDefined",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(def, true);
        return def;
    }

    private void SetupActiveDefinition()
    {
        _activeDefinition = CreateActiveSystemDefinition();
        _mockDefinitionRepo
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([_activeDefinition]);
    }

    private void SetupNoActiveDefinition() =>
        _mockDefinitionRepo
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

    private CreateGameAgentResult CreateFakeAgentResult() =>
        new()
        {
            LibraryEntryId = Guid.NewGuid(),
            GameId = GameId,
            Status = "active",
            Definition = new AgentDefinitionInfo { Id = _activeDefinition?.Id ?? Guid.NewGuid(), Name = "Default Agent" },
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
        _mockDefinitionRepo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
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
        _mockDefinitionRepo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
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

        // Assert — definition lookup never reached
        _mockDefinitionRepo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockMediator.Verify(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Guard: no active system definition exists
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoActiveSystemDefinitionExists_SkipsAutoAgentCreation()
    {
        // Arrange
        var pdfId = await AddAdminPdfAsync();
        SetupNoActiveDefinition();
        var evt = CreateReadyEvent(pdfId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert — mediator never called even though PDF is admin priority
        _mockMediator.Verify(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNoSystemDefinitionIsActive_SkipsAutoAgentCreation()
    {
        // Arrange — definition exists but is not active and not system-defined
        var pdfId = await AddAdminPdfAsync();
        var inactiveDef = AgentDef.Create(
            "Inactive",
            "Not active",
            AgentType.RulesInterpreter,
            AgentDefinitionConfig.Default());
        // not Activate() → IsActive=false, IsSystemDefined=false
        _mockDefinitionRepo
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([inactiveDef]);

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
        SetupActiveDefinition();
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
        SetupActiveDefinition();

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
                    c.AgentDefinitionId == _activeDefinition!.Id &&
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
        SetupActiveDefinition();

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
        SetupActiveDefinition();

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
        SetupActiveDefinition();

        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OutOfMemoryException("Simulated OOM"));

        var evt = CreateReadyEvent(pdfId);

        // Act & Assert
        await _handler.Invoking(h => h.Handle(evt, CancellationToken.None))
            .Should().NotThrowAsync();
    }
}
