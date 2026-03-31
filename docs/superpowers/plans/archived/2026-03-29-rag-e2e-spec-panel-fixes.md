# RAG E2E Spec Panel Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 3 issues identified by the spec panel review of the Library → PDF → Embedding → RAG Chat E2E flow: (1) document readiness validation in streaming chat, (2) embedding dimension startup validation, (3) private PDF auto-inclusion in agent scope with user document selection.

**Architecture:** Fix #1 adds a document-readiness guard to `SendAgentMessageCommandHandler` matching the existing pattern in `AskAgentQuestionCommandHandler`. Fix #2 adds a startup health check that validates embedding provider dimensions match the pgvector schema. Fix #3 is deferred to a separate plan (feature, not bug).

**Tech Stack:** .NET 9, MediatR, EF Core, pgvector, xUnit + Moq + FluentAssertions

---

## Scope Note

Fix #3 (auto-include private PDF + user document selection UI) is a **feature** spanning backend event handlers, new queries, and frontend UI. It will be planned separately. This plan covers only the two **bug/safety fixes** (#1 and #2).

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/SendAgentMessageCommandHandler.cs` | Modify (~260) | Add document readiness check after agent validation |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/SendAgentMessageCommandHandlerTests.cs` | Modify (end) | Add test for DOCUMENTS_NOT_READY error |
| `apps/api/src/Api/Infrastructure/Health/Checks/EmbeddingDimensionHealthCheck.cs` | Create | Startup health check: provider dims vs pgvector schema |
| `apps/api/src/Api/Infrastructure/Health/Extensions/HealthCheckServiceExtensions.cs` | Modify (~38) | Register new health check |
| `apps/api/tests/Api.Tests/Infrastructure/Health/EmbeddingDimensionHealthCheckTests.cs` | Create | Unit tests for dimension validation |

---

## Task 1: Document Readiness Validation in SendAgentMessageCommandHandler

**Context:** `AskAgentQuestionCommandHandler` (line 131-154) already checks `PdfDocument.ProcessingState != Ready` and throws `InvalidOperationException`. The streaming handler `SendAgentMessageCommandHandler` skips this check entirely, allowing chat to proceed with zero RAG context when documents are still being processed.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/SendAgentMessageCommandHandler.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/SendAgentMessageCommandHandlerTests.cs`

### Step 1.1: Write failing test

- [ ] Add test to `SendAgentMessageCommandHandlerTests.cs`

The handler needs `IPdfDocumentRepository` injected. Currently the handler does NOT have this dependency, so the test must first verify the new behavior. We need to:
1. Add `IPdfDocumentRepository` mock to the test class
2. Update the constructor call
3. Write the test

Add these at the end of the test file, before the `SeedAgentConfiguration` helper:

```csharp
[Fact]
public async Task Should_Return_Error_When_Documents_Not_Ready()
{
    // Arrange
    var agentId = Guid.NewGuid();
    var gameId = Guid.NewGuid();
    // Agent with gameId — triggers document readiness check
    var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent,
        AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)),
        true, gameId: gameId);
    SeedAgentConfiguration(agentId);
    var command = new SendAgentMessageCommand(agentId, "How do I play?", _userId);

    _mockAgentRepository
        .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(agent);

    // Simulate a document still being processed.
    // PdfDocument constructor sets ProcessingState = Pending (not Ready).
    // Note: uses agent.GameId (not request.GameId) because SendAgentMessageCommand
    // has no GameId property — this is an adaptation from AskAgentQuestionCommandHandler.
    var processingDoc = new PdfDocument(
        Guid.NewGuid(), gameId, new FileName("rules.pdf"), "/path/rules.pdf",
        new FileSize(5_000_000), _userId);

    _mockPdfDocumentRepository
        .Setup(r => r.FindByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<PdfDocument> { processingDoc });

    // Act
    var events = new List<RagStreamingEvent>();
    await foreach (var @event in _handler.Handle(command, CancellationToken.None))
    {
        events.Add(@event);
    }

    // Assert
    events.Should().ContainSingle();
    var error = events[0].Data.Should().BeOfType<StreamingError>().Which;
    error.errorCode.Should().Be("DOCUMENTS_NOT_READY");
    error.errorMessage.Should().Contain("processing");
}
```

**Note:** This test will not compile yet because:
1. The handler doesn't accept `IPdfDocumentRepository` in constructor
2. The test class doesn't have `_mockPdfDocumentRepository` field
3. The handler doesn't have the check logic

That's expected — we write the test first, then make it pass.

- [ ] Add mock field and update constructor in test class

Add to the test class fields (before line 42, alongside other mocks):

```csharp
private readonly Mock<IPdfDocumentRepository> _mockPdfDocumentRepository;
```

Add to the constructor (after line 53):

```csharp
_mockPdfDocumentRepository = new Mock<IPdfDocumentRepository>();
```

Update the handler construction (line 80-98) to include the new dependency. The new parameter goes after `CreatePermissiveRagAccessServiceMock()` and before `_mockLogger.Object`:

```csharp
_handler = new SendAgentMessageCommandHandler(
    _mockAgentRepository.Object,
    _mockChatThreadRepository.Object,
    _mockUnitOfWork.Object,
    _mockLlmService.Object,
    _mockEmbeddingService.Object,
    _dbContext,
    _mockBudgetService.Object,
    Mock.Of<ILlmModelOverrideService>(),
    Mock.Of<IModelConfigurationService>(),
    new ChatContextDomainService(),
    mockQueryRewriter.Object,
    Mock.Of<IConversationSummarizer>(),
    consentCheckMock.Object,
    Mock.Of<IGameSessionOrchestratorService>(),
    Mock.Of<IHybridCacheService>(),
    CreatePermissiveRagAccessServiceMock(),
    _mockPdfDocumentRepository.Object,
    _mockLogger.Object
);
```

Add the usings:

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
```

### Step 1.2: Run test to verify it fails

- [ ] Run: `cd apps/api && dotnet build`

Expected: Compilation error — `SendAgentMessageCommandHandler` constructor doesn't accept `IPdfDocumentRepository` parameter.

### Step 1.3: Add IPdfDocumentRepository to handler

- [ ] Modify `SendAgentMessageCommandHandler.cs`

Add using at the top (after line 6):

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
```

Add field (before `_logger` at line 55, shifting it down):

```csharp
private readonly IPdfDocumentRepository _pdfDocumentRepository;
```

Add constructor parameter (after `IRagAccessService ragAccessService,` — line 76):

```csharp
IPdfDocumentRepository pdfDocumentRepository,
```

Add assignment in constructor body (after line 94, before `_logger = logger`):

```csharp
_pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
```

### Step 1.4: Add the document readiness check

- [ ] Add validation in `HandleCore` method

Insert the following block **after** the closing `}` of the RAG access enforcement block (after line 213, before the comment `// Load agent configuration and validate KB readiness`).

**Note:** `SendAgentMessageCommand` has no `GameId` property (unlike `AskAgentQuestionCommand`), so we use `agent.GameId` instead of `request.GameId`. This is equivalent — the agent's game association determines which documents to check:

```csharp
// Document readiness check — aligned with AskAgentQuestionCommandHandler
if (agent.GameId.HasValue && sessionContext == null)
{
    var documents = await _pdfDocumentRepository
        .FindByGameIdAsync(agent.GameId.Value, cancellationToken)
        .ConfigureAwait(false);

    if (documents.Count > 0)
    {
        var notCompleted = documents.Count(d =>
            d.ProcessingState != PdfProcessingState.Ready);
        if (notCompleted > 0)
        {
            var stateBreakdown = documents
                .GroupBy(d => d.ProcessingState)
                .Select(g => $"{g.Key}={g.Count()}")
                .ToList();

            _logger.LogInformation(
                "Documents not ready for game {GameId}: {NotReady}/{Total}. States: {States}",
                agent.GameId, notCompleted, documents.Count, string.Join(", ", stateBreakdown));

            yield return CreateEvent(
                StreamingEventType.Error,
                new StreamingError(
                    $"{notCompleted} di {documents.Count} documenti sono ancora in fase di processing. Attendi il completamento prima di chattare.",
                    "DOCUMENTS_NOT_READY"));
            yield break;
        }
    }
}
```

### Step 1.5: Run test to verify it passes

- [ ] Run: `cd apps/api && dotnet test --filter "Should_Return_Error_When_Documents_Not_Ready" --no-restore`

Expected: PASS

### Step 1.6: Verify existing tests still pass

- [ ] Run: `cd apps/api && dotnet test --filter "SendAgentMessageCommandHandlerTests" --no-restore`

Expected: All tests PASS. Existing tests don't set up `_mockPdfDocumentRepository` with game-linked agents, so `FindByGameIdAsync` returns an empty list by default (Moq default), and the check is skipped.

### Step 1.7: Commit

- [ ] Commit

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/SendAgentMessageCommandHandler.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/SendAgentMessageCommandHandlerTests.cs
git commit -m "fix(kb): add document readiness check to SendAgentMessageCommandHandler

Aligned streaming chat handler with AskAgentQuestionCommandHandler.
Previously, chat could proceed with zero RAG context when PDFs were
still being processed (Extracting/Chunking/Embedding states).
Now returns DOCUMENTS_NOT_READY SSE error if any documents are not Ready."
```

---

## Task 2: Embedding Dimension Startup Health Check

**Context:** The pgvector_embeddings table has `vector(768)` hardcoded in schema. The default provider (OllamaNomic) outputs 768-dim vectors. If someone changes the provider to OpenRouterSmall (1536) or External (1024), inserts fail silently or produce garbage cosine similarity. A health check at startup prevents this.

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Health/Checks/EmbeddingDimensionHealthCheck.cs`
- Modify: `apps/api/src/Api/Infrastructure/Health/Extensions/HealthCheckServiceExtensions.cs`
- Create: `apps/api/tests/Api.Tests/Infrastructure/Health/EmbeddingDimensionHealthCheckTests.cs`

### Step 2.1: Write failing test

- [ ] Create test file `apps/api/tests/Api.Tests/Infrastructure/Health/EmbeddingDimensionHealthCheckTests.cs`

```csharp
using Api.Infrastructure.Health.Checks;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Health;

[Trait("Category", "Unit")]
public sealed class EmbeddingDimensionHealthCheckTests
{
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<ILogger<EmbeddingDimensionHealthCheck>> _mockLogger;

    public EmbeddingDimensionHealthCheckTests()
    {
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockLogger = new Mock<ILogger<EmbeddingDimensionHealthCheck>>();
    }

    [Fact]
    public async Task Should_Return_Healthy_When_Dimensions_Match_Schema()
    {
        // Arrange — default schema is 768
        _mockEmbeddingService
            .Setup(s => s.GetEmbeddingDimensions())
            .Returns(768);

        var healthCheck = new EmbeddingDimensionHealthCheck(
            _mockEmbeddingService.Object, _mockLogger.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Theory]
    [InlineData(1536, "OpenRouterSmall")]
    [InlineData(1024, "External/HuggingFace")]
    [InlineData(3072, "OpenRouterLarge")]
    public async Task Should_Return_Unhealthy_When_Dimensions_Mismatch(
        int providerDimensions, string scenario)
    {
        // Arrange
        _mockEmbeddingService
            .Setup(s => s.GetEmbeddingDimensions())
            .Returns(providerDimensions);

        var healthCheck = new EmbeddingDimensionHealthCheck(
            _mockEmbeddingService.Object, _mockLogger.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy,
            because: $"provider returns {providerDimensions} dims but schema expects 768 ({scenario})");
        result.Description.Should().Contain("768");
        result.Description.Should().Contain(providerDimensions.ToString());
    }
}
```

### Step 2.2: Run test to verify it fails

- [ ] Run: `cd apps/api && dotnet build`

Expected: Compilation error — `EmbeddingDimensionHealthCheck` class does not exist.

### Step 2.3: Implement health check

- [ ] Create `apps/api/src/Api/Infrastructure/Health/Checks/EmbeddingDimensionHealthCheck.cs`

```csharp
using Api.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check that validates the configured embedding provider dimensions
/// match the pgvector_embeddings table schema (vector(768)).
/// Prevents silent RAG failures from dimension mismatches.
/// </summary>
public class EmbeddingDimensionHealthCheck : IHealthCheck
{
    /// <summary>
    /// The vector dimension defined in the pgvector_embeddings table schema.
    /// Must match PgVectorEmbeddingEntityConfiguration: HasColumnType("vector(768)").
    /// </summary>
    internal const int ExpectedSchemaDimensions = 768;

    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<EmbeddingDimensionHealthCheck> _logger;

    public EmbeddingDimensionHealthCheck(
        IEmbeddingService embeddingService,
        ILogger<EmbeddingDimensionHealthCheck> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var providerDimensions = _embeddingService.GetEmbeddingDimensions();

            if (providerDimensions == ExpectedSchemaDimensions)
            {
                return Task.FromResult(HealthCheckResult.Healthy(
                    $"Embedding dimensions match: provider={providerDimensions}, schema={ExpectedSchemaDimensions}"));
            }

            _logger.LogError(
                "CRITICAL: Embedding dimension mismatch! Provider outputs {ProviderDims}-dim vectors " +
                "but pgvector_embeddings schema expects {SchemaDims}-dim. " +
                "RAG indexing and search will fail. Change Embedding:Provider config or run a schema migration.",
                providerDimensions, ExpectedSchemaDimensions);

            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Embedding dimension mismatch: provider={providerDimensions}, schema expects {ExpectedSchemaDimensions}. " +
                $"Change Embedding:Provider in config or run a schema migration to vector({providerDimensions})."));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not verify embedding dimensions — service may be unavailable");
            return Task.FromResult(HealthCheckResult.Degraded(
                "Cannot verify embedding dimensions — embedding service unavailable", ex));
        }
    }
}
```

### Step 2.4: Run test to verify it passes

- [ ] Run: `cd apps/api && dotnet test --filter "EmbeddingDimensionHealthCheckTests" --no-restore`

Expected: All 4 tests PASS.

### Step 2.5: Register health check

- [ ] Modify `apps/api/src/Api/Infrastructure/Health/Extensions/HealthCheckServiceExtensions.cs`

Add after the `EmbeddingServiceHealthCheck` registration block (after line 38):

```csharp
// Embedding dimension validation — catches provider/schema mismatch at startup.
// Uses Degraded + NonCritical to match other AI health checks and avoid
// blocking dev/CI startup when embedding service is not running.
builder.AddCheck<EmbeddingDimensionHealthCheck>(
    "embedding-dimensions",
    HealthStatus.Degraded,
    tags: new[] { HealthCheckTags.Ai, HealthCheckTags.NonCritical },
    timeout: TimeSpan.FromSeconds(1));
```

### Step 2.6: Verify build and existing health check tests

- [ ] Run: `cd apps/api && dotnet build --no-restore`

Expected: Build succeeds.

### Step 2.7: Commit

- [ ] Commit

```bash
git add apps/api/src/Api/Infrastructure/Health/Checks/EmbeddingDimensionHealthCheck.cs
git add apps/api/src/Api/Infrastructure/Health/Extensions/HealthCheckServiceExtensions.cs
git add apps/api/tests/Api.Tests/Infrastructure/Health/EmbeddingDimensionHealthCheckTests.cs
git commit -m "fix(health): add embedding dimension validation health check

Prevents silent RAG failures when the embedding provider dimensions
(e.g., 1536 for OpenRouterSmall) don't match the pgvector_embeddings
table schema (vector(768)). Flags as Unhealthy at startup with
actionable error message."
```

---

## Summary

| Task | Type | Risk Fixed | Effort |
|------|------|-----------|--------|
| 1. Document readiness check in streaming chat | Bug fix | Chat proceeds with zero RAG context when PDFs processing | ~30 min |
| 2. Embedding dimension health check | Safety | Silent RAG corruption on provider change | ~20 min |

### Not in Scope (Separate Plan)

**Fix #3: Private PDF auto-inclusion + user document selection UI** — This is a feature spanning:
- New `PrivatePdfAssociatedEventHandler` in KnowledgeBase BC
- New `GetAvailableDocumentsForGameQuery` combining shared + private PDFs
- Frontend PDF selection UI in ConfigAgentStep
- Agent creation flow updates

This will be planned separately as `2026-03-29-private-pdf-agent-scope.md`.
