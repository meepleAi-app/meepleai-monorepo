using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Integration;

/// <summary>
/// Integration tests for SSE Stream → ChatThread Persistence.
/// Issue #4467: Verifies that messages streamed via SSE are correctly
/// persisted to PostgreSQL with full metadata (Content, AgentType,
/// Confidence, TokenCount).
///
/// Uses SharedTestcontainers for real PostgreSQL.
/// Mocks only ILlmService to control streaming output.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Collection("Integration-GroupA")]
public sealed class SendAgentMessagePersistenceTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private string _connectionString = null!;
    private readonly string _databaseName = $"test_sse_persist_{Guid.NewGuid():N}";

    private IChatThreadRepository _chatThreadRepository = null!;
    private IAgentRepository _agentRepository = null!;
    private IUnitOfWork _unitOfWork = null!;
    private Mock<ILlmService> _mockLlmService = null!;
    private SendAgentMessageCommandHandler _handler = null!;

    private Guid _agentId;
    private Guid _vectorDocId;
    private readonly Guid _userId = Guid.NewGuid();

    public SendAgentMessagePersistenceTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connectionString);
        _dbContext = await Api.Tests.Infrastructure.TestHelpers.CreateDbContextAndMigrateAsync(_connectionString);

        var eventCollector = TestDbContextFactory.CreateMockEventCollector();

        _chatThreadRepository = new ChatThreadRepository(_dbContext, eventCollector.Object);
        _agentRepository = new AgentRepository(_dbContext, eventCollector.Object);
        _unitOfWork = new UnitOfWork(_dbContext);
        _mockLlmService = new Mock<ILlmService>();

        var mockQueryRewriter = new Mock<IConversationQueryRewriter>();
        mockQueryRewriter
            .Setup(r => r.RewriteQueryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((query, _, _) => Task.FromResult(query));

        // Issue #5513: Consent check — default true so existing tests pass
        var consentCheckMock = new Mock<IUserAiConsentCheckService>();
        consentCheckMock
            .Setup(s => s.IsAiProcessingAllowedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // EmbeddingService — return valid embedding so handler doesn't stop at EMBEDDING_FAILED
        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[384] }));

        // UserBudgetService — return true to avoid budget-exhausted fallback path
        var mockBudgetService = new Mock<IUserBudgetService>();
        mockBudgetService
            .Setup(b => b.HasBudgetForQueryAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _handler = new SendAgentMessageCommandHandler(
            _agentRepository,
            _chatThreadRepository,
            _unitOfWork,
            _mockLlmService.Object,
            mockEmbeddingService.Object,
            _dbContext,
            mockBudgetService.Object,
            Mock.Of<ILlmModelOverrideService>(),
            Mock.Of<IModelConfigurationService>(),
            new ChatContextDomainService(),
            mockQueryRewriter.Object,
            Mock.Of<IConversationSummarizer>(),
            consentCheckMock.Object,
            Mock.Of<IGameSessionOrchestratorService>(),
            Mock.Of<IHybridCacheService>(),
            CreatePermissiveRagAccessServiceMock(),
            Mock.Of<ILogger<SendAgentMessageCommandHandler>>());

        // Seed a test user (FK requirement)
        var userEntity = new UserEntity
        {
            Id = _userId,
            Email = "test@meepleai.test",
            DisplayName = "TestUser",
            Role = "User"
        };
        _dbContext.Set<UserEntity>().Add(userEntity);

        // Seed a test agent
        _agentId = Guid.NewGuid();
        var agentEntity = new AgentEntity
        {
            Id = _agentId,
            Name = "TestTutor",
            Type = "RAG",
            StrategyName = "default",
            StrategyParametersJson = "{}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Set<AgentEntity>().Add(agentEntity);

        // Seed a PdfDocument (FK for VectorDocument)
        var pdfDocId = Guid.NewGuid();
        var pdfDoc = new PdfDocumentEntity
        {
            Id = pdfDocId,
            FileName = "test.pdf",
            FilePath = "/test/test.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = _userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready"
        };
        _dbContext.Set<PdfDocumentEntity>().Add(pdfDoc);

        // Seed a VectorDocument (needed for agent KB doc IDs)
        _vectorDocId = Guid.NewGuid();
        var vectorDoc = new VectorDocumentEntity
        {
            Id = _vectorDocId,
            PdfDocumentId = pdfDocId,
            ChunkCount = 5,
            TotalCharacters = 500,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow
        };
        _dbContext.Set<VectorDocumentEntity>().Add(vectorDoc);

        // Seed AgentConfiguration with the vector document selected
        var agentConfig = new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = _agentId,
            LlmProvider = 0,
            LlmModel = "test-model",
            AgentMode = 0,
            SelectedDocumentIdsJson = JsonSerializer.Serialize(new List<Guid> { _vectorDocId }),
            Temperature = 0.7m,
            MaxTokens = 1024,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _userId
        };
        _dbContext.Set<AgentConfigurationEntity>().Add(agentConfig);

        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // Detach all to start clean
        foreach (var entry in _dbContext.ChangeTracker.Entries().ToList())
            entry.State = EntityState.Detached;
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null) await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    /// <summary>
    /// Sets up the LLM mock to return a non-streaming completion result.
    /// The handler now uses GenerateCompletionWithModelAsync (non-streaming).
    /// </summary>
    private void SetupLlmResponse(string fullResponse)
    {
        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(fullResponse));
    }

    private async Task<List<RagStreamingEvent>> ConsumeStream(SendAgentMessageCommand command, CancellationToken ct = default)
    {
        var events = new List<RagStreamingEvent>();
        await foreach (var e in _handler.Handle(command, ct))
        {
            events.Add(e);
        }
        return events;
    }

    /// <summary>
    /// Creates a fresh DbContext to read DB state without EF change tracker interference.
    /// </summary>
    private MeepleAiDbContext CreateReadContext()
    {
        return _fixture.CreateDbContext(_connectionString);
    }

    // ========================================================================
    // Tests
    // ========================================================================

    [Fact]
    public async Task Should_Persist_User_And_Assistant_Messages_To_Database()
    {
        // Arrange
        SetupLlmResponse("Catan is a board game.");
        var command = new SendAgentMessageCommand(_agentId, "What is Catan?", _userId);

        // Act
        var events = await ConsumeStream(command);

        // Assert - stream completed successfully
        events.Should().Contain(e => e.Type == StreamingEventType.Complete);

        var completeEvent = events.Last();
        var complete = completeEvent.Data.Should().BeOfType<StreamingComplete>().Which;
        complete.chatThreadId.Should().NotBeNull();

        // Verify persistence with fresh DbContext
        using var readCtx = CreateReadContext();
        var threadEntity = await readCtx.ChatThreads
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == complete.chatThreadId!.Value);

        threadEntity.Should().NotBeNull();
        threadEntity!.UserId.Should().Be(_userId);

        // Deserialize messages and verify both user + assistant persisted
        var messages = System.Text.Json.JsonSerializer.Deserialize<List<MessageDto>>(threadEntity.MessagesJson);
        messages.Should().NotBeNull();
        messages!.Count.Should().Be(2);

        var userMsg = messages.First(m => m.Role == "user");
        userMsg.Content.Should().Be("What is Catan?");

        var assistantMsg = messages.First(m => m.Role == "assistant");
        assistantMsg.Content.Should().Be("Catan is a board game.");
    }

    [Fact]
    public async Task Should_Persist_AgentMetadata_On_Assistant_Message()
    {
        // Arrange
        SetupLlmResponse("Setup rules: place settlements.");
        var command = new SendAgentMessageCommand(_agentId, "How to setup?", _userId);

        // Act
        await ConsumeStream(command);

        // Assert - read back from DB
        using var readCtx = CreateReadContext();
        var thread = await readCtx.ChatThreads.AsNoTracking().FirstAsync(t => t.UserId == _userId);
        var messages = System.Text.Json.JsonSerializer.Deserialize<List<MessageDto>>(thread.MessagesJson)!;

        var assistantMsg = messages.First(m => m.Role == "assistant");
        assistantMsg.AgentType.Should().Be("RAG");
        // Confidence is null when no RAG chunks are retrieved (vector search returns empty results in tests)
        // TokenCount comes from LlmCompletionResult.Usage.TotalTokens (0 from mock's default Usage)
        assistantMsg.TokenCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_Update_Thread_LastMessageAt_After_Streaming()
    {
        // Arrange
        var beforeStream = DateTime.UtcNow.AddSeconds(-1);
        SetupLlmResponse("Answer.");
        var command = new SendAgentMessageCommand(_agentId, "Question?", _userId);

        // Act
        await ConsumeStream(command);

        // Assert
        using var readCtx = CreateReadContext();
        var thread = await readCtx.ChatThreads.AsNoTracking().FirstAsync(t => t.UserId == _userId);
        thread.LastMessageAt.Should().BeAfter(beforeStream);
    }

    [Fact]
    public async Task Should_AutoCreate_Thread_And_Persist_When_No_ThreadId()
    {
        // Arrange
        SetupLlmResponse("New thread response.");
        var command = new SendAgentMessageCommand(_agentId, "Start new chat", _userId);

        // Act
        var events = await ConsumeStream(command);

        // Assert
        var complete = events.Last().Data.Should().BeOfType<StreamingComplete>().Which;
        complete.chatThreadId.Should().NotBeNull();

        using var readCtx = CreateReadContext();
        var thread = await readCtx.ChatThreads.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == complete.chatThreadId!.Value);

        thread.Should().NotBeNull();
        thread!.Title.Should().Be("Start new chat"); // Title from user question
        thread.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task Should_Reuse_Existing_Thread_When_ThreadId_Provided()
    {
        // Arrange - create thread first
        SetupLlmResponse("First response.");
        var firstCommand = new SendAgentMessageCommand(_agentId, "First question", _userId);
        var firstEvents = await ConsumeStream(firstCommand);
        var firstComplete = firstEvents.Last().Data.Should().BeOfType<StreamingComplete>().Which;
        var threadId = firstComplete.chatThreadId!.Value;

        // Detach all tracked entities for second pass
        foreach (var entry in _dbContext.ChangeTracker.Entries().ToList())
            entry.State = EntityState.Detached;

        // Arrange follow-up
        SetupLlmResponse("Follow-up response.");
        var followUpCommand = new SendAgentMessageCommand(_agentId, "Follow-up question", _userId, threadId);

        // Act
        await ConsumeStream(followUpCommand);

        // Assert - same thread, now 4 messages (2 user + 2 assistant)
        using var readCtx = CreateReadContext();
        var thread = await readCtx.ChatThreads.AsNoTracking().FirstAsync(t => t.Id == threadId);
        var messages = System.Text.Json.JsonSerializer.Deserialize<List<MessageDto>>(thread.MessagesJson)!;
        messages.Count.Should().Be(4);
        messages.Count(m => m.Role == "user").Should().Be(2);
        messages.Count(m => m.Role == "assistant").Should().Be(2);
    }

    [Fact]
    public async Task Should_Not_Persist_Assistant_Message_When_LLM_Returns_Empty()
    {
        // Arrange - LLM returns empty response string
        SetupLlmResponse("");

        var command = new SendAgentMessageCommand(_agentId, "Empty response test", _userId);

        // Act
        var events = await ConsumeStream(command);

        // Assert - Complete event emitted
        events.Should().Contain(e => e.Type == StreamingEventType.Complete);

        // Verify: only user message persisted (no assistant message since response was empty)
        using var readCtx = CreateReadContext();
        var thread = await readCtx.ChatThreads.AsNoTracking().FirstAsync(t => t.UserId == _userId);
        var messages = System.Text.Json.JsonSerializer.Deserialize<List<MessageDto>>(thread.MessagesJson)!;
        messages.Count.Should().Be(1);
        messages[0].Role.Should().Be("user");
    }

    [Fact]
    public async Task Should_Persist_User_Message_Even_When_LLM_Call_Cancelled()
    {
        // Arrange - LLM call hangs then gets cancelled
        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, string _, RequestSource _, CancellationToken ct) =>
            {
                await Task.Delay(5000, ct); // Long delay to allow cancellation
                return LlmCompletionResult.CreateSuccess("Never reached");
            });

        using var cts = new CancellationTokenSource();
        var command = new SendAgentMessageCommand(_agentId, "Cancel me", _userId);

        // Act - cancel after 500ms (after user message is persisted but before LLM completes)
        var events = new List<RagStreamingEvent>();
        cts.CancelAfter(500);
        try
        {
            await foreach (var e in _handler.Handle(command, cts.Token))
            {
                events.Add(e);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - user message was persisted before LLM call
        using var readCtx = CreateReadContext();
        var threads = await readCtx.ChatThreads.AsNoTracking()
            .Where(t => t.UserId == _userId)
            .ToListAsync();

        threads.Should().HaveCount(1);
        var thread = threads[0];
        var messages = System.Text.Json.JsonSerializer.Deserialize<List<MessageDto>>(thread.MessagesJson)!;

        // At minimum, user message should be persisted (it's saved before LLM call)
        messages.Should().Contain(m => m.Role == "user" && m.Content == "Cancel me");
    }

    [Fact]
    public async Task Should_Return_Error_When_Agent_Not_Found_And_Not_Create_Thread()
    {
        // Arrange
        var bogusAgentId = Guid.NewGuid();
        var command = new SendAgentMessageCommand(bogusAgentId, "Hello", _userId);

        // Act
        var events = await ConsumeStream(command);

        // Assert
        events.Should().HaveCount(1);
        var error = events[0].Data.Should().BeOfType<StreamingError>().Which;
        error.errorCode.Should().Be("AGENT_NOT_FOUND");

        // No thread created
        using var readCtx = CreateReadContext();
        var count = await readCtx.ChatThreads.CountAsync(t => t.UserId == _userId);
        count.Should().Be(0);
    }

    [Fact]
    public async Task Should_Persist_Correct_TokenCount_From_LlmUsage()
    {
        // Arrange - LLM returns a response with explicit token count in usage
        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "Hello world!",
                usage: new LlmUsage(PromptTokens: 10, CompletionTokens: 3, TotalTokens: 13)));

        var command = new SendAgentMessageCommand(_agentId, "Count tokens", _userId);

        // Act
        await ConsumeStream(command);

        // Assert
        using var readCtx = CreateReadContext();
        var thread = await readCtx.ChatThreads.AsNoTracking().FirstAsync(t => t.UserId == _userId);
        var messages = System.Text.Json.JsonSerializer.Deserialize<List<MessageDto>>(thread.MessagesJson)!;

        var assistantMsg = messages.First(m => m.Role == "assistant");
        assistantMsg.Content.Should().Be("Hello world!");
        assistantMsg.TokenCount.Should().Be(13); // From LlmUsage.TotalTokens
    }

    // ========================================================================
    // DTO for deserializing MessagesJson
    // ========================================================================

    private sealed record MessageDto(
        string Content,
        string Role,
        DateTime Timestamp,
        int SequenceNumber = 0,
        string? AgentType = null,
        float? Confidence = null,
        string? CitationsJson = null,
        int? TokenCount = null,
        Guid Id = default);
    private static IRagAccessService CreatePermissiveRagAccessServiceMock()
    {
        var mock = new Mock<IRagAccessService>();
        mock.Setup(s => s.CanAccessRagAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock.Object;
    }
}
