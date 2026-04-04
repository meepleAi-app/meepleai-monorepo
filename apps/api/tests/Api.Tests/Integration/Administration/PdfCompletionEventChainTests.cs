using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using AgentDef = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests verifying the event chain:
/// PdfStateChanged(Ready) + Admin priority → AutoCreateAgentOnPdfReadyHandler → CreateGameAgentCommand dispatched.
/// Issue #4673: Validate handler reads real DB, checks conditions, dispatches command.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4673")]
public sealed class PdfCompletionEventChainTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public PdfCompletionEventChainTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_pdf_event_chain_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        // MeepleAiDbContext requires IMediator + IDomainEventCollector; provide no-op mocks
        services.AddSingleton<IMediator>(new Mock<IMediator>().Object);
        services.AddSingleton<Api.SharedKernel.Application.Services.IDomainEventCollector>(
            new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>().Object);
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        var serviceProvider = services.BuildServiceProvider();
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null) await _dbContext.DisposeAsync();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { }
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helper: seed DB with PDF, User
    // ────────────────────────────────────────────────────────────────────────

    private async Task<Guid> SeedAdminPdfAsync(string priority = "Admin")
    {
        var pdfId = Guid.NewGuid();
        _dbContext!.Users.Add(new UserEntity
        {
            Id = UserId,
            Email = "admin@meepleai.dev",
            Tier = "premium",
            Role = "admin",
        });
        _dbContext.Games.Add(new GameEntity { Id = GameId, Name = "Gloomhaven" });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = GameId,
            FileName = "gloomhaven.pdf",
            FilePath = "/uploads/gloomhaven.pdf",
            UploadedByUserId = UserId,
            ProcessingPriority = priority,
        });
        await _dbContext.SaveChangesAsync();
        return pdfId;
    }

    private AutoCreateAgentOnPdfReadyHandler CreateHandler(
        Mock<IAgentDefinitionRepository> definitionRepo,
        Mock<IMediator> mediator)
    {
        return new AutoCreateAgentOnPdfReadyHandler(
            _dbContext!,
            definitionRepo.Object,
            mediator.Object,
            new Mock<ILogger<AutoCreateAgentOnPdfReadyHandler>>().Object);
    }

    private AgentDef CreateActiveSystemDefinition()
    {
        var def = AgentDef.Create(
            "Default",
            "Desc",
            AgentType.RulesInterpreter,
            AgentDefinitionConfig.Default());
        def.Activate();
        var field = typeof(AgentDef).GetField("_isSystemDefined",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(def, true);
        return def;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Happy path: event chain fires CreateGameAgentCommand
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task Handle_AdminPdfReady_DispatchesCreateGameAgentCommandWithCorrectParams()
    {
        // Arrange
        var pdfId = await SeedAdminPdfAsync("Admin");
        var activeDefinition = CreateActiveSystemDefinition();

        var mockDefinitionRepo = new Mock<IAgentDefinitionRepository>();
        mockDefinitionRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([activeDefinition]);

        CreateGameAgentCommand? capturedCommand = null;
        var mockMediator = new Mock<IMediator>();
        mockMediator
            .Setup(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CreateGameAgentResult>, CancellationToken>((cmd, _) =>
                capturedCommand = (CreateGameAgentCommand)cmd)
            .ReturnsAsync(new CreateGameAgentResult
            {
                LibraryEntryId = Guid.NewGuid(),
                GameId = GameId,
                Status = "active",
                Definition = new AgentDefinitionInfo { Id = activeDefinition.Id, Name = "Default" },
                Strategy = new AgentStrategyInfo { Name = "Balanced", Parameters = null },
            });

        var handler = CreateHandler(mockDefinitionRepo, mockMediator);
        var evt = new PdfStateChangedEvent(pdfId, PdfProcessingState.Indexing, PdfProcessingState.Ready, UserId);

        // Act
        await handler.Handle(evt, TestCancellationToken);

        // Assert: command dispatched with correct data from real DB
        capturedCommand.Should().NotBeNull("CreateGameAgentCommand should be dispatched");
        capturedCommand!.UserId.Should().Be(UserId);
        capturedCommand.AgentDefinitionId.Should().Be(activeDefinition.Id);
        capturedCommand.StrategyName.Should().Be("Balanced");
        capturedCommand.UserTier.Should().Be("premium");
        capturedCommand.UserRole.Should().Be("admin");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Guard: Non-admin priority PDF → command NOT dispatched
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task Handle_NormalPriorityPdfReady_DoesNotDispatchCreateGameAgentCommand()
    {
        // Arrange
        var pdfId = await SeedAdminPdfAsync("Normal");

        var mockDefinitionRepo = new Mock<IAgentDefinitionRepository>();
        var mockMediator = new Mock<IMediator>();

        var handler = CreateHandler(mockDefinitionRepo, mockMediator);
        var evt = new PdfStateChangedEvent(pdfId, PdfProcessingState.Indexing, PdfProcessingState.Ready, UserId);

        // Act
        await handler.Handle(evt, TestCancellationToken);

        // Assert: command NOT dispatched for normal priority
        mockMediator.Verify(
            m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Guard: Non-Ready state → command NOT dispatched
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task Handle_NonReadyState_DoesNotDispatchCreateGameAgentCommand()
    {
        // Arrange — PDF exists but state is not Ready
        await SeedAdminPdfAsync("Admin");
        var mockDefinitionRepo = new Mock<IAgentDefinitionRepository>();
        var mockMediator = new Mock<IMediator>();

        var handler = CreateHandler(mockDefinitionRepo, mockMediator);
        var evt = new PdfStateChangedEvent(Guid.NewGuid(), PdfProcessingState.Pending, PdfProcessingState.Extracting, UserId);

        // Act
        await handler.Handle(evt, TestCancellationToken);

        // Assert
        mockMediator.Verify(
            m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Guard: Exception in command handler → not propagated (event chain not broken)
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task Handle_AgentCreationFails_ExceptionDoesNotPropagateFromEventHandler()
    {
        // Arrange
        var pdfId = await SeedAdminPdfAsync("Admin");
        var activeDefinition = CreateActiveSystemDefinition();

        var mockDefinitionRepo = new Mock<IAgentDefinitionRepository>();
        mockDefinitionRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([activeDefinition]);

        var mockMediator = new Mock<IMediator>();
        mockMediator
            .Setup(m => m.Send(It.IsAny<CreateGameAgentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent creation failed"));

        var handler = CreateHandler(mockDefinitionRepo, mockMediator);
        var evt = new PdfStateChangedEvent(pdfId, PdfProcessingState.Indexing, PdfProcessingState.Ready, UserId);

        // Act & Assert — pipeline must not break
        await handler.Invoking(h => h.Handle(evt, TestCancellationToken))
            .Should().NotThrowAsync();
    }
}
