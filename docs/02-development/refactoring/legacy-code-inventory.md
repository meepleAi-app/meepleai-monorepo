# Legacy Code Inventory & Removal Plan

**Document Status**: Active
**Last Updated**: 2025-11-13
**DDD Refactoring Progress**: 99% → Target 100%
**Estimated Legacy Code**: ~24,000-28,000 lines

---

## Executive Summary

Despite achieving 99% DDD refactoring completion (as documented in CLAUDE.md), the API codebase still contains significant legacy code that bypasses the CQRS/MediatR pattern. This document provides a comprehensive inventory of remaining legacy services and endpoints, prioritized removal plan, and migration guidelines.

### Key Findings

- **105 legacy service files** remain in `/apps/api/src/Api/Services/`
- **7 out of 10 endpoint files** still use legacy service injection instead of MediatR
- **Only 2 endpoint files** are fully DDD-compliant (KnowledgeBaseEndpoints, UserProfileEndpoints)
- **Estimated 24,000-28,000 lines** of legacy code to remove
- **2,070 lines already removed** (GameService, AuthService, PDF services, UserManagementService)

### Services to Retain

Per CLAUDE.md requirements, the following orchestration/infrastructure services must be **KEPT**:
- ✅ `ConfigurationService` - Runtime configuration management
- ✅ `AdminStatsService` - Admin statistics aggregation
- ✅ `AlertingService` - Email/Slack/PagerDuty alerting infrastructure
- ✅ `RagService` - RAG orchestration layer

---

## 1. Endpoint Files Analysis

### 1.1 Fully DDD-Compliant ✅

#### KnowledgeBaseEndpoints.cs (165 lines) - 100% DDD ✅
**Status**: Perfect DDD implementation
**Uses**: Only `IMediator` with CQRS handlers
- `SearchQuery` - Hybrid vector/keyword search
- `AskQuestionQuery` - RAG Q&A

**Bounded Context**: `KnowledgeBase`

#### UserProfileEndpoints.cs (208 lines) - 100% DDD ✅
**Status**: Perfect DDD implementation
**Uses**: Only `IMediator` with CQRS handlers
- `GetUserProfileQuery`
- `UpdateUserProfileCommand`
- `ChangePasswordCommand`

**Bounded Context**: `Authentication`

---

### 1.2 Partially DDD-Compliant ⚠️

#### GameEndpoints.cs (243 lines) - 95% DDD, 5% Legacy ⚠️
**Status**: Almost complete, 1 legacy call

**DDD (11 endpoints)** ✅:
- Game CRUD: `GetAllGamesQuery`, `GetGameByIdQuery`, `CreateGameCommand`, `UpdateGameCommand`
- Sessions: `StartGameSessionCommand`, `CompleteGameSessionCommand`, `AbandonGameSessionCommand`, `GetGameSessionByIdQuery`, `GetActiveSessionsByGameQuery`

**Legacy (1 endpoint)** ❌:
- Line 106: `ChatService.GetAgentsForGameAsync()` - Should be `GetAgentsByGameQuery`

**Bounded Context**: `GameManagement`

**Migration Effort**: 1 hour (create 1 query handler)

---

#### AuthEndpoints.cs (926 lines) - 40% DDD, 60% Legacy ⚠️

**DDD (4 endpoints)** ✅:
- `RegisterCommand` (line 34-76)
- `LoginCommand` (line 79-175)
- `LogoutCommand` (line 177-200)
- `CreateSessionCommand` (line 366, 560, 894) - Used in 2FA verify, OAuth callback, password reset

**Legacy Services (40+ usages)** ❌:
- `ITotpService` - 2FA setup, enable, verify, disable, status (7 endpoints)
  - Lines: 250, 287, 328, 353, 356, 400, 415, 441, 456
- `IOAuthService` - Google/Discord/GitHub OAuth (5 endpoints)
  - Lines: 481, 504, 529, 554, 610, 618, 639, 644
- `ITempSessionService` - Temporary sessions for 2FA (1 usage)
  - Line: 343
- `IRateLimitService` - Rate limiting (4 usages)
  - Lines: 331, 484, 538
- `ISessionCacheService` - Session caching (2 usages)
  - Lines: 717, 757
- `IPasswordResetService` - Password reset flow (3 endpoints)
  - Lines: 789, 800, 828, 838, 863, 881
- `ISessionManagementService` - Session listing (1 endpoint)
  - Line: 774, 779

**Bounded Context**: `Authentication`

**Migration Effort**: 2-3 weeks

**Priority**: HIGH - Authentication is core functionality

---

#### PdfEndpoints.cs (488 lines) - 30% DDD, 70% Legacy ⚠️

**DDD (1 endpoint)** ✅:
- `IndexPdfCommand` (line 447, 461) - PDF indexing for vector search

**Legacy Services (20+ usages)** ❌:
- `PdfStorageService` - Upload, download, delete PDFs (5 endpoints)
  - Lines: 24, 78, 203, 208, 241, 287
- `IPdfValidator` - PDF validation (1 usage)
  - Line: 24, 67
- `AuditService` - Manual audit logging (2 usages)
  - Lines: 241, 269, 298
- `RuleSpecService` - Generate RuleSpec from PDF (1 endpoint)
  - Line: 422, 436
- `IBggApiService` - BoardGameGeek API integration (2 endpoints)
  - Lines: 99, 119, 148, 167
- `IFeatureFlagService` - Feature flag checks (1 usage)
  - Line: 24, 27
- `IBackgroundTaskService` - Task cancellation (1 endpoint)
  - Line: 373, 407

**Bounded Contexts**: `DocumentProcessing` (primary), `GameManagement` (RuleSpec)

**Migration Effort**: 2 weeks

**Priority**: MEDIUM - PDF processing is complete in DDD layer, just need endpoint migration

---

#### AdminEndpoints.cs (2423 lines) - 30% DDD, 70% Legacy ⚠️

**DDD (some endpoints)** ✅:
- N8n configuration via `IMediator` (partially)
- Some admin commands use MediatR

**Legacy Services (50+ usages)** ❌:
- `IQualityReportService` - Quality reports (1+ usages)
  - Line: 233
- `IFeatureFlagService` - Feature flags (5+ usages)
  - Lines: 251, 288
- `IEncryptionService` - Encryption (infrastructure, keep but refactor)
  - Lines: 283, 318
- `ISessionManagementService` - Session management (3+ usages)
  - Lines: 483, 492, 509
- `IWorkflowErrorLoggingService` - Workflow error logging (3+ usages)
  - Lines: 590, 606, 631
- `IAlertingService` - **KEEP per CLAUDE.md** (infrastructure)
  - Lines: 659, 719, 745
- `IPromptTemplateService` - Prompt templates (5+ usages)
  - Line: 1053
- `IPromptEvaluationService` - Prompt evaluation (4+ usages)
  - Lines: 1100, 1157, 1205, 1225
- `IAiResponseCacheService` - AI response caching (2+ usages)
  - Lines: 1842, 1871

**Bounded Contexts**: `Administration`, `WorkflowIntegration`, `SystemConfiguration`

**Migration Effort**: 3-4 weeks

**Priority**: MEDIUM - Admin features are not user-facing

---

### 1.3 Fully Legacy ❌

#### RuleSpecEndpoints.cs (573 lines) - 0% DDD, 100% Legacy ❌

**ZERO MediatR usage** - All endpoints use legacy services

**Legacy Services (50+ usages)** ❌:
- `RuleSpecService` - CRUD, versioning, history, diffs, exports (15+ endpoints)
  - Lines: 18, 24, 35, 48, 73, 79, 91, 108, 116, 122, 134, 146, 147, 528, 541
- `AuditService` - Manual audit logging (1 usage)
  - Line: 35, 52
- `RuleSpecDiffService` - Version diffs (1 endpoint)
  - Line: 134, 154
- `RuleSpecCommentService` - Legacy comment system (4 endpoints)
  - Lines: 159, 172, 183, 189, 193, 206, 222, 232
- `IRuleCommentService` - New comment system (6 endpoints)
  - Lines: 268, 273, 319, 324, 376, 381, 405, 410, 437, 444, 489, 496

**Bounded Context**: `GameManagement`

**Migration Effort**: 2 weeks

**Priority**: HIGH - Core feature for editors

**Notes**:
- RuleSpec domain logic exists in `GameManagement.Domain`
- Need to create Commands/Queries for all 15+ endpoints
- Comment system has both legacy and new implementations (needs consolidation)

---

#### ChatEndpoints.cs (370 lines) - 0% DDD, 95% Legacy ❌

**ZERO MediatR usage** - All endpoints use legacy services

**Legacy Services (10+ usages)** ❌:
- `ChatService` - Chat CRUD, message management (10+ endpoints)
  - Lines: 18, 24, 25, 40, 45, 73, 90, 93, 106, 119, 126, 143, 149, 169, 170, 209, 213, 234, 237
- `IChatExportService` - Export chats to MD/TXT/PDF (1 endpoint)
  - Lines: 276, 279, 303
- `IFeatureFlagService` - Feature flag checks (2 usages)
  - Lines: 143, 150, 160, 209, 224, 289

**Bounded Context**: `KnowledgeBase`

**Migration Effort**: 1.5 weeks

**Priority**: HIGH - User-facing chat functionality

**Notes**:
- Chat domain entities exist in Infrastructure
- Need to move to KnowledgeBase bounded context
- Message edit/delete features (CHAT-06) depend on this

---

#### AiEndpoints.cs (2400+ lines estimated) - 0% DDD, 100% Legacy ❌

**ZERO MediatR usage** - All endpoints use legacy services

**Legacy Services (40+ usages)** ❌:
- `IRagService` - RAG orchestration (**KEEP per CLAUDE.md**, but refactor endpoints)
  - Multiple usages throughout
- `ChatService` - Chat persistence
  - Lines: 22, 26, 63-72, 162-184
- `AiRequestLogService` - AI request logging
  - Line: 22, 27, 187
- `IResponseQualityService` - Quality scoring (AI-11)
  - Lines: 22, 28, 131
- `IFollowUpQuestionService` - Follow-up question generation (CHAT-02)
  - Lines: 22, 29, 98
- `IStreamingRagService` - Streaming RAG responses
  - Streaming endpoints
- `IChessAgentService` - Chess agent (specialized)
  - Chess endpoints
- `IChessKnowledgeService` - Chess knowledge base
  - Chess endpoints
- `IBggApiService` - BoardGameGeek API
  - BGG search endpoints

**Bounded Context**: `KnowledgeBase`

**Migration Effort**: 4-5 weeks (LARGE file)

**Priority**: CRITICAL - Main AI/RAG functionality

**Notes**:
- This is the largest and most complex endpoint file
- Contains multiple sub-features: QA, streaming, explain, chess, BGG
- RagService orchestrator should be kept but wrapped in CQRS handlers
- Quality scoring and follow-up generation need domain service patterns

---

## 2. Legacy Services Inventory

### 2.1 By Bounded Context

#### Authentication (~20 services) ❌

**Core Authentication**:
- `ApiKeyAuthenticationService.cs` - API key validation
- `ApiKeyManagementService.cs` - API key CRUD
- `PasswordHashingService.cs` - Password hashing (PBKDF2)
- `PasswordResetService.cs` - Password reset flow
- `EncryptionService.cs` - Token encryption (infrastructure)

**OAuth**:
- `OAuthService.cs` - Google/Discord/GitHub OAuth

**2FA**:
- `TotpService.cs` - TOTP generation/verification
- `TempSessionService.cs` - Temporary sessions for 2FA

**Sessions**:
- `SessionManagementService.cs` - Session listing/revocation
- `SessionCacheService.cs` - Session caching (Redis)
- `SessionAutoRevocationService.cs` - Auto-revoke inactive sessions

**Migration Target**: Create Commands/Queries in `BoundedContexts/Authentication/Application/`

---

#### KnowledgeBase (~40 services) ❌

**Chat**:
- `ChatService.cs` - Chat CRUD, message management
- `ChatExportService.cs` - Export to MD/TXT/PDF
- `AgentFeedbackService.cs` - Agent feedback

**RAG/Search**:
- `RagService.cs` - **KEEP** (orchestration layer per CLAUDE.md)
- `StreamingQaService.cs` - Streaming Q&A
- `StreamingRagService.cs` - Streaming RAG
- `HybridSearchService.cs` - Hybrid vector+keyword search
- `KeywordSearchService.cs` - PostgreSQL FTS
- `Rag/CitationExtractorService.cs` - Citation extraction
- `Rag/QueryExpansionService.cs` - Query expansion
- `RagEvaluationService.cs` - RAG quality metrics

**LLM**:
- `LlmService.cs` - OpenRouter LLM client
- `OllamaLlmService.cs` - Ollama local LLM
- `HybridLlmService.cs` - Multi-model consensus (GPT-4 + Claude)
- `ProviderHealthCheckService.cs` - LLM provider health

**Embeddings & Vectors**:
- `EmbeddingService.cs` - Text embeddings (OpenAI)
- `QdrantService.cs` - Qdrant vector DB client
- `QdrantClientAdapter.cs` - Qdrant adapter

**Text Processing**:
- `TextChunkingService.cs` - Sentence chunking
- `LanguageDetectionService.cs` - Language detection

**Quality & Prompts**:
- `QualityReportService.cs` - Quality reports
- `ResponseQualityService.cs` - Response quality scoring
- `PromptTemplateService.cs` - Prompt templates
- `PromptEvaluationService.cs` - Prompt A/B testing
- `PromptManagementService.cs` - Prompt versioning

**Chess (Specialized)**:
- `ChessAgentService.cs` - Chess agent
- `ChessKnowledgeService.cs` - Chess knowledge base

**AI Logging**:
- `AiRequestLogService.cs` - AI request logging
- `AiResponseCacheService.cs` - AI response caching

**Setup**:
- `SetupGuideService.cs` - Setup guide generation

**Migration Target**: Create Commands/Queries in `BoundedContexts/KnowledgeBase/Application/`

---

#### GameManagement (~10 services) ❌

**RuleSpec**:
- `RuleSpecService.cs` - RuleSpec CRUD, versioning, history
- `RuleCommentService.cs` - Comment system (new)
- `RuleSpecCommentService.cs` - Comment system (legacy)
- `RuleSpecDiffService.cs` - Version diff computation

**BGG Integration**:
- `BggApiService.cs` - BoardGameGeek API client

**Migration Target**: Create Commands/Queries in `BoundedContexts/GameManagement/Application/`

**Note**: Some domain logic already exists in `GameManagement.Domain`, just needs CQRS wrappers

---

#### DocumentProcessing (~5 services) ❌

**PDF Processing**:
- `PdfStorageService.cs` - PDF upload, storage, retrieval, deletion

**Note**:
- Core PDF extraction is already DDD-compliant (3-stage fallback pipeline)
- `IndexPdfCommand` already exists
- Just need CQRS wrappers for CRUD operations

**Migration Target**: Create Commands/Queries in `BoundedContexts/DocumentProcessing/Application/`

---

#### WorkflowIntegration (~5 services) ❌

**n8n Integration**:
- `N8nConfigService.cs` - n8n configuration
- `N8nTemplateService.cs` - n8n workflow templates
- `WorkflowErrorLoggingService.cs` - Workflow error logging

**Migration Target**: Create Commands/Queries in `BoundedContexts/WorkflowIntegration/Application/`

**Note**: Some Commands already exist, just need to complete migration

---

#### SystemConfiguration (~5 services) ❌

**Configuration**:
- `ConfigurationService.cs` - **KEEP** (per CLAUDE.md)
- `FeatureFlagService.cs` - Feature flags
- `DynamicTtlStrategy.cs` - Cache TTL strategy

**Migration Target**: Create Commands/Queries in `BoundedContexts/SystemConfiguration/Application/`

**Note**: ConfigurationService is orchestration layer, keep but refactor endpoints to use CQRS

---

#### Administration (~10 services) ❌

**Audit**:
- `AuditService.cs` - Audit logging

**Stats**:
- `AdminStatsService.cs` - **KEEP** (per CLAUDE.md)

**Alerting**:
- `AlertingService.cs` - **KEEP** (per CLAUDE.md, infrastructure)
- `EmailAlertChannel.cs` - Email alerts
- `SlackAlertChannel.cs` - Slack alerts
- `PagerDutyAlertChannel.cs` - PagerDuty alerts

**Background Tasks**:
- `BackgroundTaskService.cs` - Background task management

**Caching**:
- `CacheMetricsRecorder.cs` - Cache metrics
- `CacheWarmingService.cs` - Cache warming

**Rate Limiting**:
- `RateLimitService.cs` - Token bucket rate limiting
- `RedisFrequencyTracker.cs` - Redis-based frequency tracking

**Migration Target**: Create Commands/Queries in `BoundedContexts/Administration/Application/`

---

#### Infrastructure (Cross-cutting) ❌

**Email**:
- `EmailService.cs` - Email sending

**Caching**:
- `HybridCacheService.cs` - L1+L2 caching

**Migration**: Move to `Infrastructure/` and inject as interfaces

---

## 3. Migration Priority Matrix

### Priority 1: CRITICAL (Complete First) 🔴

| Endpoint File | Lines | Legacy % | Effort | Bounded Context | Users Impacted |
|--------------|-------|----------|--------|-----------------|----------------|
| **AiEndpoints.cs** | ~2,400 | 100% | 4-5 weeks | KnowledgeBase | HIGH - All users |
| **RuleSpecEndpoints.cs** | 573 | 100% | 2 weeks | GameManagement | MEDIUM - Editors |
| **ChatEndpoints.cs** | 370 | 95% | 1.5 weeks | KnowledgeBase | HIGH - All users |

**Total**: ~3,350 lines, 7.5-8.5 weeks

**Impact**: Core user-facing AI/chat functionality

---

### Priority 2: HIGH (Complete Second) 🟠

| Endpoint File | Lines | Legacy % | Effort | Bounded Context | Users Impacted |
|--------------|-------|----------|--------|-----------------|----------------|
| **AuthEndpoints.cs** | 926 | 60% | 2-3 weeks | Authentication | HIGH - Security |
| **PdfEndpoints.cs** | 488 | 70% | 2 weeks | DocumentProcessing | MEDIUM - Editors |

**Total**: ~1,414 lines, 4-5 weeks

**Impact**: Security and document processing

---

### Priority 3: MEDIUM (Complete Third) 🟡

| Endpoint File | Lines | Legacy % | Effort | Bounded Context | Users Impacted |
|--------------|-------|----------|--------|-----------------|----------------|
| **AdminEndpoints.cs** | 2,423 | 70% | 3-4 weeks | Administration | LOW - Admins only |
| **GameEndpoints.cs** | 243 | 5% | 1 hour | GameManagement | LOW - Minor fix |

**Total**: ~2,666 lines, 3-4 weeks

**Impact**: Admin features, low user impact

---

### Summary

| Priority | Files | Lines | Effort | Order |
|----------|-------|-------|--------|-------|
| **CRITICAL** | 3 | ~3,350 | 7.5-8.5 weeks | 1st |
| **HIGH** | 2 | ~1,414 | 4-5 weeks | 2nd |
| **MEDIUM** | 2 | ~2,666 | 3-4 weeks | 3rd |
| **TOTAL** | 7 | ~7,430 | 15-17.5 weeks | - |

**Note**: These are endpoint files only. Service removal happens after endpoint migration.

---

## 4. Migration Guidelines

### 4.1 Standard Migration Pattern

Follow this proven pattern used for GameManagement and DocumentProcessing:

#### Step 1: Create Domain Logic (if needed)
```csharp
// Location: BoundedContexts/{Context}/Domain/
// Example: BoundedContexts/KnowledgeBase/Domain/Services/ChatDomainService.cs

public class ChatDomainService
{
    public ChatMessage CreateMessage(ChatId chatId, UserId userId, string content)
    {
        // Pure domain logic, no dependencies
        if (string.IsNullOrWhiteSpace(content))
            throw new ValidationException("Message content cannot be empty");

        return new ChatMessage(chatId, userId, content, DateTime.UtcNow);
    }
}
```

#### Step 2: Create CQRS Commands/Queries
```csharp
// Location: BoundedContexts/{Context}/Application/Commands/
// Example: BoundedContexts/KnowledgeBase/Application/Commands/CreateChatCommand.cs

public record CreateChatCommand(
    Guid UserId,
    Guid GameId,
    Guid AgentId
) : IRequest<ChatDto>;
```

#### Step 3: Create Handler
```csharp
// Location: BoundedContexts/{Context}/Application/Handlers/
// Example: BoundedContexts/KnowledgeBase/Application/Handlers/CreateChatCommandHandler.cs

public class CreateChatCommandHandler : IRequestHandler<CreateChatCommand, ChatDto>
{
    private readonly IChatRepository _chatRepository;
    private readonly ILogger<CreateChatCommandHandler> _logger;

    public async Task<ChatDto> Handle(CreateChatCommand request, CancellationToken ct)
    {
        // 1. Load aggregates/entities
        // 2. Execute domain logic
        // 3. Persist changes
        // 4. Return DTO
    }
}
```

#### Step 4: Register in DI
```csharp
// Location: BoundedContexts/{Context}/Infrastructure/DependencyInjection/
// Example: BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs

public static IServiceCollection AddKnowledgeBaseBoundedContext(this IServiceCollection services)
{
    // Register handlers (MediatR discovers them automatically)
    services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

    // Register repositories
    services.AddScoped<IChatRepository, ChatRepository>();

    return services;
}
```

#### Step 5: Migrate Endpoint to MediatR
```csharp
// BEFORE (Legacy)
group.MapPost("/chats", async (
    CreateChatRequest request,
    HttpContext context,
    ChatService chatService,  // ❌ Legacy injection
    CancellationToken ct) =>
{
    var chat = await chatService.CreateChatAsync(request.UserId, request.GameId, request.AgentId, ct);
    return Results.Created($"/chats/{chat.Id}", chat);
});

// AFTER (DDD)
group.MapPost("/chats", async (
    CreateChatRequest request,
    HttpContext context,
    IMediator mediator,  // ✅ Only MediatR
    CancellationToken ct) =>
{
    var command = new CreateChatCommand(request.UserId, request.GameId, request.AgentId);
    var result = await mediator.Send(command, ct);
    return Results.Created($"/chats/{result.Id}", result);
});
```

#### Step 6: Write Tests
```csharp
// Location: tests/Api.Tests/BoundedContexts/{Context}/Application/Handlers/
// Example: tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/CreateChatCommandHandlerTests.cs

public class CreateChatCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_CreatesChat()
    {
        // Arrange
        var repository = new Mock<IChatRepository>();
        var handler = new CreateChatCommandHandler(repository.Object, logger);
        var command = new CreateChatCommand(userId, gameId, agentId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        repository.Verify(r => r.AddAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

#### Step 7: Run Tests
```bash
# Backend tests
dotnet test

# Verify coverage
dotnet test --collect:"XPlat Code Coverage"
```

#### Step 8: Remove Legacy Service
```bash
# Only after ALL endpoints are migrated
rm apps/api/src/Api/Services/ChatService.cs
rm apps/api/src/Api/Services/IChatService.cs
```

#### Step 9: Commit
```bash
git add .
git commit -m "feat: Migrate ChatEndpoints to DDD/CQRS (KnowledgeBase)"
```

---

### 4.2 Handling Orchestration Services

Some services like `RagService`, `ConfigurationService`, `AlertingService` are **orchestration layers** that coordinate multiple bounded contexts. Per CLAUDE.md, these should be **KEPT** but endpoints should still use CQRS:

```csharp
// Handler wraps orchestration service
public class AskQuestionCommandHandler : IRequestHandler<AskQuestionCommand, QaResponse>
{
    private readonly IRagService _ragService;  // ✅ Orchestrator kept

    public async Task<QaResponse> Handle(AskQuestionCommand request, CancellationToken ct)
    {
        // Wrap orchestration call in CQRS handler
        return await _ragService.AskWithHybridSearchAsync(
            request.GameId,
            request.Query,
            request.SearchMode,
            request.Language,
            request.BypassCache,
            ct);
    }
}
```

**Services to Wrap (Not Remove)**:
- `RagService` - RAG orchestration
- `ConfigurationService` - Runtime config
- `AdminStatsService` - Stats aggregation
- `AlertingService` - Multi-channel alerting

---

### 4.3 Handling Infrastructure Services

Infrastructure services (email, caching, encryption) should be moved to `Infrastructure/` and injected via interfaces:

```csharp
// Move from Services/ to Infrastructure/Email/
// Keep interface in SharedKernel or Infrastructure
public interface IEmailService
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}

// Implementation in Infrastructure/Email/EmailService.cs
public class EmailService : IEmailService
{
    // SMTP implementation
}

// Register in Infrastructure DI
services.AddTransient<IEmailService, EmailService>();

// Use in handlers
public class SendPasswordResetEmailCommandHandler : IRequestHandler<SendPasswordResetEmailCommand>
{
    private readonly IEmailService _emailService;  // ✅ Infrastructure service
}
```

---

### 4.4 Test Coverage Requirements

Maintain **90%+ test coverage** throughout migration:

1. **Unit Tests** (handlers):
   - Test each command/query handler independently
   - Mock repositories and infrastructure
   - Cover happy path + error cases

2. **Integration Tests** (endpoints):
   - Use Testcontainers for Postgres, Qdrant, Redis
   - Test full request → MediatR → handler → repository → response flow
   - Verify HTTP status codes and response formats

3. **Domain Tests**:
   - Test domain services and value objects
   - Verify business rule validation

**Example Coverage Report**:
```bash
# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate report
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report

# View report
open coverage-report/index.html

# Verify ≥90% coverage
```

---

## 5. Service Removal Checklist

Before removing any legacy service file:

### Pre-Removal Checklist

- [ ] **All endpoints migrated** - Grep for service name in `Routing/` directory
  ```bash
  grep -r "ChatService" apps/api/src/Api/Routing/
  # Should return ZERO results
  ```

- [ ] **All tests passing** - Run full test suite
  ```bash
  dotnet test
  # Should be 100% pass
  ```

- [ ] **Coverage maintained** - Verify ≥90%
  ```bash
  dotnet test --collect:"XPlat Code Coverage"
  ```

- [ ] **No DI registrations** - Check `Program.cs` and service extensions
  ```bash
  grep -r "AddChatService" apps/api/src/Api/
  # Should return ZERO results
  ```

- [ ] **Documentation updated** - Update CLAUDE.md if service was listed
  ```bash
  grep "ChatService" CLAUDE.md
  # Remove reference if present
  ```

### Removal Steps

1. **Remove service files**:
   ```bash
   rm apps/api/src/Api/Services/ChatService.cs
   rm apps/api/src/Api/Services/IChatService.cs
   ```

2. **Remove DI registration**:
   ```csharp
   // Remove from Program.cs or service extensions
   // services.AddScoped<IChatService, ChatService>();
   ```

3. **Verify build**:
   ```bash
   dotnet build
   # Should succeed with ZERO errors
   ```

4. **Run tests**:
   ```bash
   dotnet test
   # Should pass 100%
   ```

5. **Commit removal**:
   ```bash
   git add .
   git commit -m "refactor: Remove ChatService (migrated to KnowledgeBase CQRS)"
   ```

---

## 6. Tracking Progress

### 6.1 Metrics Dashboard

Track migration progress with these metrics:

| Metric | Current | Target | Progress |
|--------|---------|--------|----------|
| **DDD Refactoring** | 99% | 100% | 🟡 |
| **Endpoint Files Migrated** | 2/10 | 10/10 | 🔴 20% |
| **Legacy Service Files** | 105 | 4* | 🔴 4% |
| **Legacy Code Lines** | ~24,000 | ~800* | 🔴 3% |
| **Test Coverage** | 90%+ | 90%+ | 🟢 100% |

\* Excluding ConfigurationService, AdminStatsService, AlertingService, RagService (orchestration layers to keep)

---

### 6.2 Weekly Progress Report Template

```markdown
## DDD Migration Progress - Week of YYYY-MM-DD

### Completed This Week
- [ ] Migrated `{EndpointFile}` (X lines, Y endpoints)
- [ ] Created Z CQRS handlers in `{BoundedContext}`
- [ ] Removed A legacy services
- [ ] All tests passing (90%+ coverage)

### In Progress
- [ ] Working on `{EndpointFile}`
- [ ] Created X/Y handlers

### Blocked
- [ ] None / {Blocker description}

### Next Week Plan
- [ ] Complete `{EndpointFile}`
- [ ] Start `{NextEndpointFile}`

### Metrics
- Endpoint Files Migrated: X/10
- Legacy Services Remaining: Y/105
- Test Coverage: Z%
```

---

### 6.3 GitHub Project Board

Create issues for each endpoint file:

**Example Issue Template**:
```markdown
## Migrate {EndpointFile} to DDD/CQRS

**Bounded Context**: {Context}
**Lines**: {N}
**Effort**: {Estimate}
**Priority**: {Critical/High/Medium}

### Tasks
- [ ] Create Commands/Queries
- [ ] Create Handlers
- [ ] Write tests (90%+ coverage)
- [ ] Update endpoints to use IMediator
- [ ] Remove legacy service injection
- [ ] Verify all tests pass
- [ ] Update documentation

### Legacy Services to Replace
- {ServiceName1}
- {ServiceName2}

### Definition of Done
- [ ] Zero legacy service injections in endpoint file
- [ ] All tests passing
- [ ] Coverage ≥90%
- [ ] Documentation updated
```

---

## 7. Estimated Timeline

### Phase 1: Critical (Weeks 1-9) 🔴

| Week | Task | Bounded Context | Lines | Status |
|------|------|-----------------|-------|--------|
| 1-5 | Migrate AiEndpoints.cs | KnowledgeBase | ~2,400 | ⬜ Not Started |
| 6-7 | Migrate RuleSpecEndpoints.cs | GameManagement | 573 | ⬜ Not Started |
| 8-9 | Migrate ChatEndpoints.cs | KnowledgeBase | 370 | ⬜ Not Started |

**Deliverable**: Core AI/chat/RuleSpec functionality 100% DDD

---

### Phase 2: High (Weeks 10-14) 🟠

| Week | Task | Bounded Context | Lines | Status |
|------|------|-----------------|-------|--------|
| 10-12 | Migrate AuthEndpoints.cs | Authentication | 926 | ⬜ Not Started |
| 13-14 | Migrate PdfEndpoints.cs | DocumentProcessing | 488 | ⬜ Not Started |

**Deliverable**: Security and document processing 100% DDD

---

### Phase 3: Medium (Weeks 15-18) 🟡

| Week | Task | Bounded Context | Lines | Status |
|------|------|-----------------|-------|--------|
| 15-18 | Migrate AdminEndpoints.cs | Administration | 2,423 | ⬜ Not Started |
| 18 | Fix GameEndpoints.cs | GameManagement | 1 call | ⬜ Not Started |

**Deliverable**: Admin features 100% DDD

---

### Phase 4: Service Cleanup (Weeks 19-20) 🧹

| Week | Task | Services Removed | Status |
|------|------|------------------|--------|
| 19-20 | Remove ~100 legacy service files | 100+ files | ⬜ Not Started |

**Deliverable**: `/Services/` directory cleaned up (only 4 orchestration services remain)

---

### Total Duration: ~20 weeks (5 months)

---

## 8. Risk Mitigation

### 8.1 Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| **Breaking changes** in production | Medium | High | • Feature flags for new endpoints<br>• Gradual rollout<br>• Comprehensive integration tests |
| **Test coverage drops** below 90% | Medium | Medium | • Pre-commit coverage checks<br>• PR reviews require coverage report<br>• Block merge if <90% |
| **Performance regression** | Low | Medium | • Benchmark existing endpoints<br>• Compare MediatR vs direct service calls<br>• Load testing before production |
| **Timeline overrun** | Medium | Low | • Prioritize critical endpoints first<br>• Ship incrementally<br>• Defer admin features if needed |
| **Domain logic loss** | Low | High | • Thorough code review<br>• Port ALL business rules to domain layer<br>• Validate with E2E tests |

---

### 8.2 Rollback Plan

If critical issues arise after migration:

1. **Feature Flag Disable**:
   ```csharp
   // Temporarily disable new DDD endpoint
   if (!await featureFlags.IsEnabledAsync("Features.DddChatEndpoints"))
   {
       // Fall back to legacy endpoint (keep temporarily)
   }
   ```

2. **Git Revert**:
   ```bash
   # Revert specific commit
   git revert <commit-hash>
   git push
   ```

3. **Hotfix Branch**:
   ```bash
   # Create hotfix from production
   git checkout -b hotfix/revert-chat-migration production
   git revert <commit-hash>
   git push origin hotfix/revert-chat-migration
   ```

---

## 9. Success Criteria

Migration is **complete** when:

### Code Metrics ✅
- [x] DDD refactoring: 100% (currently 99%)
- [x] Endpoint files migrated: 10/10 (currently 2/10)
- [x] Legacy services removed: 100/105 (keep 4 orchestration services + 1 infrastructure)
- [x] Test coverage: ≥90% (maintained throughout)

### Code Quality ✅
- [x] All endpoints use `IMediator.Send()` instead of direct service injection
- [x] All Commands/Queries in `BoundedContexts/{Context}/Application/`
- [x] All Handlers in `BoundedContexts/{Context}/Application/Handlers/`
- [x] All domain logic in `BoundedContexts/{Context}/Domain/`
- [x] Zero legacy services in `/Services/` except:
  - ConfigurationService (orchestration)
  - AdminStatsService (orchestration)
  - AlertingService (infrastructure)
  - RagService (orchestration)

### Documentation ✅
- [x] CLAUDE.md updated with 100% DDD status
- [x] This document archived as "completed"
- [x] Architecture docs updated (ADRs, diagrams)

### Production ✅
- [x] All tests passing (xUnit + Testcontainers)
- [x] Zero build errors
- [x] Zero build warnings (legacy service references)
- [x] Performance benchmarks meet/exceed legacy baseline
- [x] E2E tests passing

---

## 10. References

### Internal Documentation
- [CLAUDE.md](../../CLAUDE.md) - Project overview (DDD status)
- [ddd-quick-reference.md](../ddd-quick-reference.md) - DDD patterns
- [board-game-ai-architecture-overview.md](../architecture/board-game-ai-architecture-overview.md)
- [ADR-004: AI Agents Bounded Context](../architecture/adr-004-ai-agents-bounded-context.md)

### Code Examples
- `BoundedContexts/GameManagement/` - Fully migrated bounded context
- `BoundedContexts/DocumentProcessing/` - Fully migrated bounded context
- `BoundedContexts/Authentication/` - Partially migrated (40% complete)

### External References
- [MediatR Documentation](https://github.com/jbogard/MediatR/wiki)
- [Domain-Driven Design by Eric Evans](https://www.domainlanguage.com/ddd/)
- [Implementing Domain-Driven Design by Vaughn Vernon](https://vaughnvernon.com/?page_id=168)

---

## Changelog

| Date | Author | Change |
|------|--------|--------|
| 2025-11-13 | Claude Code | Initial document creation |

---

**Document Status**: Active
**Next Review**: Weekly during migration
**Owner**: Engineering Lead
**Contact**: [Update with team contact]
