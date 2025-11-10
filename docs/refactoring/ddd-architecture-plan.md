# Domain-Driven Design Architecture Plan

## Executive Summary

Refactoring MeepleAI from layered architecture to **Domain-Driven Design (DDD)** with bounded contexts to:
- Reduce complexity (1000+ line files → 200-400 line focused modules)
- Improve testability (split 1400-line test files → feature-focused suites)
- Enable parallel development (teams can work on different bounded contexts)
- Reduce coupling (clear boundaries between domains)

**Timeline**: 6 phases over 12-16 weeks
**Risk Level**: Low (incremental, backward-compatible refactoring)
**Test Coverage Target**: Maintain 90%+ throughout refactoring

---

## Current State Analysis

### Layer Architecture Problems
```
Current Structure (Layered):
├── Services/          40+ services in flat directory ❌
│   ├── RagService.cs        (995 lines, multiple concerns) ❌
│   ├── ConfigurationService  (814 lines, 14 operations) ❌
│   ├── GameService
│   ├── AuthService
│   └── ... 36 more services
├── Infrastructure/    All entities mixed ❌
├── Models/            All DTOs mixed ❌
└── Tests/             200+ test files at root ❌
```

**Key Issues**:
1. **God Services**: Services with 700-1000 lines handling multiple concerns
2. **Low Cohesion**: Related services scattered across flat directory
3. **High Coupling**: Services directly depend on 10+ other services
4. **Navigation Difficulty**: Hard to find related code across 40+ files
5. **Test Chaos**: 1400-line test files with hundreds of test cases
6. **Parallel Dev Friction**: Multiple devs editing same large service files

---

## Target State: DDD Bounded Contexts

### Proposed Architecture
```
Target Structure (DDD Bounded Contexts):
src/Api/
├── BoundedContexts/
│   ├── Authentication/        Auth domain (OAuth, 2FA, Sessions, API Keys)
│   │   ├── Domain/            Entities, value objects, domain services
│   │   │   ├── Entities/      User, Session, ApiKey, OAuthAccount
│   │   │   ├── ValueObjects/  Email, PasswordHash, TotpSecret
│   │   │   └── Services/      AuthDomainService, TotpDomainService
│   │   ├── Application/       Use cases, DTOs, interfaces
│   │   │   ├── Commands/      LoginCommand, LogoutCommand, Enable2FACommand
│   │   │   ├── Queries/       GetUserQuery, ValidateSessionQuery
│   │   │   ├── DTOs/          UserDto, SessionDto, ApiKeyDto
│   │   │   └── Services/      AuthApplicationService, SessionApplicationService
│   │   ├── Infrastructure/    Persistence, external integrations
│   │   │   ├── Persistence/   UserRepository, SessionRepository
│   │   │   └── External/      OAuthProviders, EmailSender
│   │   └── Tests/             Authentication.Tests/
│   │       ├── Domain/        Unit tests for domain logic
│   │       ├── Application/   Unit tests for use cases
│   │       └── Integration/   Integration tests with DB
│   │
│   ├── DocumentProcessing/    PDF domain (Upload, Extraction, Validation, Storage)
│   │   ├── Domain/
│   │   │   ├── Entities/      PdfDocument, Page, TextChunk, Table
│   │   │   ├── ValueObjects/  FileSize, MimeType, PageNumber
│   │   │   └── Services/      TextExtractionDomainService, TableExtractionDomainService
│   │   ├── Application/
│   │   │   ├── Commands/      UploadPdfCommand, ExtractTextCommand
│   │   │   ├── Queries/       GetPdfQuery, ListPdfsQuery
│   │   │   ├── DTOs/          PdfDto, PageDto
│   │   │   └── Services/      PdfApplicationService, ValidationApplicationService
│   │   ├── Infrastructure/
│   │   │   ├── Persistence/   PdfDocumentRepository, FileStorage
│   │   │   └── External/      DocnetAdapter, iText7Adapter, TesseractAdapter
│   │   └── Tests/             DocumentProcessing.Tests/
│   │
│   ├── KnowledgeBase/         RAG domain (Embeddings, Vector Search, LLM)
│   │   ├── Domain/
│   │   │   ├── Entities/      VectorDocument, Embedding, SearchResult
│   │   │   ├── ValueObjects/  Vector, Confidence, Citation
│   │   │   └── Services/      EmbeddingDomainService, SearchDomainService
│   │   ├── Application/
│   │   │   ├── Commands/      IndexDocumentCommand, GenerateEmbeddingCommand
│   │   │   ├── Queries/       SearchQuery, AskQuestionQuery
│   │   │   ├── DTOs/          SearchResultDto, EmbeddingDto
│   │   │   └── Services/      RagApplicationService, LlmApplicationService
│   │   ├── Infrastructure/
│   │   │   ├── Persistence/   VectorRepository (Qdrant adapter)
│   │   │   └── External/      OpenRouterClient, EmbeddingServiceClient
│   │   └── Tests/             KnowledgeBase.Tests/
│   │
│   ├── GameManagement/        Game domain (Games, RuleSpecs, Versions, Comments)
│   │   ├── Domain/
│   │   │   ├── Entities/      Game, RuleSpec, RuleSpecVersion, Comment
│   │   │   ├── ValueObjects/  GameId, Version, Diff
│   │   │   └── Services/      DiffCalculationDomainService, VersioningDomainService
│   │   ├── Application/
│   │   │   ├── Commands/      CreateGameCommand, UpdateRuleSpecCommand, AddCommentCommand
│   │   │   ├── Queries/       GetGameQuery, GetRuleSpecQuery, ListVersionsQuery
│   │   │   ├── DTOs/          GameDto, RuleSpecDto, CommentDto
│   │   │   └── Services/      GameApplicationService, RuleSpecApplicationService
│   │   ├── Infrastructure/
│   │   │   └── Persistence/   GameRepository, RuleSpecRepository, CommentRepository
│   │   └── Tests/             GameManagement.Tests/
│   │
│   ├── SystemConfiguration/   Configuration domain (Settings, Feature Flags, Prompts)
│   │   ├── Domain/
│   │   │   ├── Entities/      Configuration, FeatureFlag, PromptTemplate, PromptVersion
│   │   │   ├── ValueObjects/  ConfigKey, ConfigValue, ConfigEnvironment
│   │   │   └── Services/      ConfigurationDomainService, FeatureFlagDomainService
│   │   ├── Application/
│   │   │   ├── Commands/      UpdateConfigCommand, ToggleFlagCommand, ActivatePromptCommand
│   │   │   ├── Queries/       GetConfigQuery, GetPromptQuery
│   │   │   ├── DTOs/          ConfigDto, FeatureFlagDto, PromptDto
│   │   │   └── Services/      ConfigurationApplicationService, PromptManagementApplicationService
│   │   ├── Infrastructure/
│   │   │   ├── Persistence/   ConfigurationRepository, PromptTemplateRepository
│   │   │   └── Caching/       RedisCacheAdapter
│   │   └── Tests/             SystemConfiguration.Tests/
│   │
│   ├── Administration/        Admin domain (User Management, Analytics, Alerting)
│   │   ├── Domain/
│   │   │   ├── Entities/      AdminUser, Alert, AuditLog, Statistic
│   │   │   ├── ValueObjects/  Role, Permission, AlertSeverity
│   │   │   └── Services/      UserManagementDomainService, AlertingDomainService
│   │   ├── Application/
│   │   │   ├── Commands/      CreateUserCommand, UpdateRoleCommand, SendAlertCommand
│   │   │   ├── Queries/       GetUsersQuery, GetStatsQuery, GetAuditLogsQuery
│   │   │   ├── DTOs/          AdminUserDto, AlertDto, StatisticsDto
│   │   │   └── Services/      UserManagementApplicationService, StatsApplicationService
│   │   ├── Infrastructure/
│   │   │   ├── Persistence/   AdminRepository, AuditLogRepository
│   │   │   └── External/      EmailAlertSender, SlackAlertSender, PagerDutyAdapter
│   │   └── Tests/             Administration.Tests/
│   │
│   └── WorkflowIntegration/   Workflow domain (n8n Templates, Webhooks, Error Handling)
│       ├── Domain/
│       │   ├── Entities/      WorkflowTemplate, WorkflowExecution, WorkflowError
│       │   ├── ValueObjects/  TemplateId, ExecutionStatus, ErrorSeverity
│       │   └── Services/      WorkflowDomainService
│       ├── Application/
│       │   ├── Commands/      ExecuteWorkflowCommand, HandleWebhookCommand, LogErrorCommand
│       │   ├── Queries/       GetTemplateQuery, GetErrorsQuery
│       │   ├── DTOs/          TemplateDto, ExecutionDto, ErrorDto
│       │   └── Services/      WorkflowApplicationService, N8nTemplateApplicationService
│       ├── Infrastructure/
│       │   ├── Persistence/   WorkflowRepository
│       │   └── External/      N8nApiAdapter
│       └── Tests/             WorkflowIntegration.Tests/
│
├── SharedKernel/              Shared across all bounded contexts
│   ├── Domain/                Common domain primitives
│   │   ├── ValueObjects/      Email, Url, DateTimeRange
│   │   ├── Interfaces/        IEntity, IAggregateRoot, IDomainEvent
│   │   └── Exceptions/        DomainException, ValidationException
│   ├── Application/           Common application concerns
│   │   ├── Interfaces/        IQuery<T>, ICommand<T>, IQueryHandler<T>, ICommandHandler<T>
│   │   └── Behaviors/         ValidationBehavior, LoggingBehavior, CachingBehavior
│   └── Infrastructure/        Cross-cutting infrastructure
│       ├── Persistence/       IRepository<T>, UnitOfWork
│       ├── Caching/           ICacheService, HybridCacheAdapter
│       └── Messaging/         IDomainEventPublisher, DomainEventDispatcher
│
└── Api/                       API entry point (Program.cs, Middleware, Routing)
    ├── Program.cs             Startup + DI registration (keep < 500 lines)
    ├── Endpoints/             Minimal API endpoints grouped by bounded context
    │   ├── AuthenticationEndpoints.cs
    │   ├── DocumentProcessingEndpoints.cs
    │   ├── KnowledgeBaseEndpoints.cs
    │   └── ...
    ├── Middleware/            Cross-cutting middleware
    └── Configuration/         Startup configuration helpers
```

---

## Bounded Context Definitions

### 1. Authentication Context
**Ubiquitous Language**: User, Session, ApiKey, OAuth Provider, 2FA, TOTP, Backup Code
**Core Domain**: Yes (business-critical security)
**Complexity**: High (OAuth, 2FA, session management, security requirements)

**Services to Migrate**:
- AuthService (Cookie auth)
- ApiKeyAuthenticationService (API key validation)
- SessionManagementService (Session CRUD)
- SessionAutoRevocationService (Background revocation)
- OAuthService (OAuth 2.0 flows)
- TotpService (TOTP generation/validation)
- TempSessionService (Temporary 2FA sessions)
- EncryptionService (Token encryption)
- PasswordResetService

**Aggregates**:
- User (aggregate root): Email, PasswordHash, Roles, TotpSecret, BackupCodes
- Session: Token, ExpiresAt, IpAddress, UserAgent
- ApiKey: KeyHash, Name, Scopes, RateLimits
- OAuthAccount: Provider, ProviderUserId, Tokens

**Domain Events**:
- UserLoggedIn, UserLoggedOut, SessionExpired
- ApiKeyCreated, ApiKeyRevoked
- TwoFactorEnabled, TwoFactorDisabled, BackupCodeUsed
- OAuthAccountLinked, OAuthAccountUnlinked

---

### 2. DocumentProcessing Context
**Ubiquitous Language**: PDF, Page, TextChunk, Table, OCR, Extraction, Validation
**Core Domain**: No (supporting, but complex)
**Complexity**: High (PDF parsing, OCR fallback, table extraction)

**Services to Migrate**:
- PdfStorageService (Upload, storage, retrieval)
- PdfTextExtractionService (Docnet.Core integration)
- PdfTableExtractionService (iText7 integration)
- PdfValidationService (Magic bytes, size, page count)
- TesseractOcrService (OCR fallback)
- PdfIndexingService (Trigger embedding pipeline)

**Aggregates**:
- PdfDocument (aggregate root): FileId, Metadata, Pages, UploadStatus
- Page: PageNumber, TextContent, Tables, Images
- Table: Rows, Columns, Cells
- TextChunk: Content, PageNumber, BoundingBox

**Domain Events**:
- PdfUploaded, PdfValidated, PdfExtractionCompleted
- OcrFallbackTriggered, TableExtracted
- PdfIndexingStarted, PdfIndexingCompleted

---

### 3. KnowledgeBase Context
**Ubiquitous Language**: Embedding, Vector, Search, Query, RAG, LLM, Citation, Confidence
**Core Domain**: Yes (core AI/RAG functionality)
**Complexity**: Very High (vector search, LLM orchestration, query expansion, RRF fusion)

**Services to Migrate** (CRITICAL - Split RagService):
- **RagService (995 lines)** → Split into:
  - EmbeddingApplicationService (Generate embeddings)
  - VectorSearchApplicationService (Qdrant search)
  - QueryExpansionApplicationService (Query variants)
  - RrfFusionApplicationService (Reciprocal Rank Fusion)
  - QualityTrackingApplicationService (Confidence metrics)
- LlmService (OpenRouter integration, streaming)
- StreamingQaService (SSE streaming)
- EmbeddingService (Embedding generation)
- QdrantService (Vector database operations)
- TextChunkingService (Sentence-aware chunking)
- KeywordSearchService (PostgreSQL FTS)
- HybridSearchService (Vector + keyword fusion)
- RagEvaluationService (Precision@K, MRR, quality gates)
- SetupGuideService (RAG-powered setup guides)
- FollowUpQuestionService

**Aggregates**:
- VectorDocument (aggregate root): DocumentId, ChunkIds, Metadata
- Embedding: Vector, Model, Dimensions
- SearchResult: DocumentId, Score, Snippet, Citation
- LlmResponse: Content, Tokens, Confidence, Model

**Domain Events**:
- EmbeddingGenerated, VectorIndexed, SearchPerformed
- LlmResponseGenerated, QualityAssessed
- LowQualityDetected, HighConfidenceAchieved

---

### 4. GameManagement Context
**Ubiquitous Language**: Game, RuleSpec, Version, Diff, Comment, Timeline
**Core Domain**: Yes (business domain model)
**Complexity**: Medium (versioning, diffs, comments)

**Services to Migrate**:
- GameService (Game CRUD)
- RuleSpecService (RuleSpec CRUD, versioning, export)
- RuleSpecDiffService (Calculate diffs between versions)
- RuleCommentService (Comments on rule specs)
- BggApiService (BoardGameGeek integration)

**Aggregates**:
- Game (aggregate root): Name, Publisher, Year, Complexity
- RuleSpec (aggregate root): Version, Content, Status, Timeline
- RuleSpecVersion: VersionNumber, Content, CreatedAt, CreatedBy
- Comment: Content, LineNumber, Author, Timestamp

**Domain Events**:
- GameCreated, GameUpdated, GameDeleted
- RuleSpecCreated, RuleSpecUpdated, RuleSpecPublished
- VersionCreated, DiffCalculated
- CommentAdded, CommentEdited, CommentDeleted

---

### 5. SystemConfiguration Context
**Ubiquitous Language**: Configuration, FeatureFlag, PromptTemplate, PromptVersion, Environment
**Core Domain**: No (generic subdomain)
**Complexity**: Medium (3-tier fallback, version control, caching)

**Services to Migrate** (CRITICAL - Split ConfigurationService):
- **ConfigurationService (814 lines, 14 operations)** → Split into:
  - ConfigurationApplicationService (CRUD, validation)
  - ConfigurationVersioningApplicationService (History, rollback)
  - ConfigurationBulkApplicationService (Bulk updates, import/export)
  - ConfigurationCacheApplicationService (Cache invalidation)
- FeatureFlagService (Runtime feature toggles)
- PromptTemplateService (Prompt management, Redis cache)
- PromptManagementService (Prompt versions, activation)
- PromptEvaluationService (Prompt A/B testing, 5-metric engine)

**Aggregates**:
- Configuration (aggregate root): Key, Value, Environment, Version, History
- FeatureFlag (aggregate root): Name, Enabled, RolloutPercentage
- PromptTemplate (aggregate root): Name, Versions, ActiveVersion
- PromptVersion: Content, Status, EvaluationResults

**Domain Events**:
- ConfigurationCreated, ConfigurationUpdated, ConfigurationDeleted
- ConfigurationActivated, ConfigurationRolledBack
- FeatureFlagToggled, FeatureFlagRolledOut
- PromptVersionCreated, PromptVersionActivated
- PromptEvaluated, PromptCompared

---

### 6. Administration Context
**Ubiquitous Language**: AdminUser, Role, Permission, Alert, Statistic, AuditLog
**Core Domain**: No (supporting)
**Complexity**: Medium (user management, alerting, analytics)

**Services to Migrate**:
- UserManagementService (User CRUD, role assignment)
- AdminStatsService (8 metrics, 5 charts, CSV/JSON export)
- AlertingService (Email, Slack, PagerDuty, throttling)
- AuditService (Audit logging)
- AiRequestLogService (AI usage tracking)
- CacheWarmingService (Cache preloading)
- BackgroundTaskService (Background job orchestration)

**Aggregates**:
- AdminUser (aggregate root): Email, Roles, Permissions, CreatedAt
- Alert (aggregate root): Severity, Message, Channel, ThrottleState
- AuditLog: Action, Entity, OldValue, NewValue, Timestamp, UserId
- Statistic: MetricName, Value, Timestamp, Dimensions

**Domain Events**:
- UserCreated, UserUpdated, UserDeleted, RoleAssigned
- AlertSent, AlertThrottled, AlertFailed
- AuditLogCreated, StatisticRecorded

---

### 7. WorkflowIntegration Context
**Ubiquitous Language**: WorkflowTemplate, WorkflowExecution, WorkflowError, Webhook, n8n
**Core Domain**: No (integration)
**Complexity**: Low (external system integration)

**Services to Migrate**:
- N8nConfigService (n8n configuration)
- N8nTemplateService (12+ templates, parameter substitution)
- WorkflowErrorLoggingService (Error handling, sensitive data redaction)

**Aggregates**:
- WorkflowTemplate (aggregate root): Name, Definition, Parameters
- WorkflowExecution: ExecutionId, Status, StartTime, EndTime
- WorkflowError: ErrorType, Message, StackTrace, RedactedData

**Domain Events**:
- WorkflowExecuted, WorkflowFailed, WorkflowRetried
- WebhookReceived, ErrorLogged, TemplateImported

---

## Migration Strategy

### Phase 1: Foundation & Shared Kernel (2 weeks)
**Goal**: Establish DDD infrastructure without breaking existing code

**Tasks**:
1. Create `BoundedContexts/` and `SharedKernel/` directories
2. Implement SharedKernel base classes:
   - `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`
   - `IRepository<TEntity>`, `IUnitOfWork`
   - `IDomainEvent`, `DomainEventDispatcher`
   - `ICommand<TResponse>`, `IQuery<TResponse>`, MediatR handlers
3. Create bounded context folder structure (all 7 contexts)
4. Add new DI registration infrastructure in `Program.cs`
5. Update build pipeline to include new directories

**Deliverables**:
- `SharedKernel/` with base classes and interfaces
- Empty bounded context directories (ready for migration)
- Updated `Program.cs` with DI registration helpers
- Documentation: "DDD Migration Phase 1 Complete"

**Risk Mitigation**:
- No existing code modified (zero risk)
- All tests still passing (100% green)
- New infrastructure ready but not yet used

---

### Phase 2: Authentication Context Migration (3 weeks)
**Goal**: Migrate entire Authentication bounded context as proof-of-concept

**Tasks**:
1. **Week 1: Domain Layer**
   - Define entities: `User`, `Session`, `ApiKey`, `OAuthAccount`
   - Define value objects: `Email`, `PasswordHash`, `TotpSecret`
   - Migrate domain logic from services to entities (e.g., `User.ValidatePassword()`)
   - Write domain unit tests

2. **Week 2: Application Layer**
   - Create commands: `LoginCommand`, `LogoutCommand`, `Enable2FACommand`
   - Create queries: `GetUserQuery`, `ValidateSessionQuery`
   - Create DTOs: `UserDto`, `SessionDto`, `ApiKeyDto`
   - Implement command/query handlers with MediatR
   - Migrate service logic to application services
   - Write application unit tests

3. **Week 3: Infrastructure & Integration**
   - Create repositories: `UserRepository`, `SessionRepository`, `ApiKeyRepository`
   - Implement external adapters: `OAuthProviderAdapter`, `EmailSenderAdapter`
   - Wire up new authentication context in `Program.cs`
   - **Dual-run mode**: Old services + new context (both active)
   - Write integration tests with Testcontainers
   - Performance comparison (old vs new)

**Deliverables**:
- Complete Authentication bounded context
- 90%+ test coverage maintained
- Dual-run mode active (fallback to old if issues)
- Performance benchmarks (prove no regression)
- Migration guide document

**Risk Mitigation**:
- Dual-run mode allows rollback if issues detected
- Extensive integration tests prevent regressions
- Feature flag to switch between old/new implementations

---

### Phase 3: KnowledgeBase Context Migration (4 weeks)
**Goal**: Migrate complex RAG domain and split 995-line RagService

**Tasks**:
1. **Week 1: Domain Layer & Splitting RagService**
   - Define entities: `VectorDocument`, `Embedding`, `SearchResult`
   - Define value objects: `Vector`, `Confidence`, `Citation`
   - **Split RagService (995 lines)** into 5 domain services:
     - `EmbeddingDomainService` (Embedding generation logic)
     - `VectorSearchDomainService` (Qdrant search logic)
     - `QueryExpansionDomainService` (Query variants)
     - `RrfFusionDomainService` (Reciprocal Rank Fusion)
     - `QualityTrackingDomainService` (Confidence metrics)
   - Write domain unit tests (target: 200-300 lines each)

2. **Week 2: Application Layer**
   - Create commands: `IndexDocumentCommand`, `GenerateEmbeddingCommand`
   - Create queries: `SearchQuery`, `AskQuestionQuery`
   - Create application services:
     - `RagApplicationService` (Orchestrates 5 domain services)
     - `LlmApplicationService` (LLM integration)
     - `StreamingQaApplicationService` (SSE streaming)
   - Write application unit tests

3. **Week 3: Infrastructure & Integration**
   - Create repositories: `VectorDocumentRepository`, `EmbeddingRepository`
   - Implement Qdrant adapter: `QdrantVectorStoreAdapter`
   - Implement OpenRouter adapter: `OpenRouterLlmAdapter`
   - Wire up KnowledgeBase context in `Program.cs`
   - Write integration tests (RAG end-to-end flows)

4. **Week 4: Performance Validation & Rollout**
   - Performance benchmarks (old RagService vs new context)
   - Load testing (ensure no regression)
   - Gradual rollout with feature flag
   - Monitor quality metrics (AI-11.2 Quality Tracking)
   - Documentation and knowledge transfer

**Deliverables**:
- Complete KnowledgeBase bounded context
- RagService split from 995 lines → 5 services (200-300 lines each)
- 90%+ test coverage maintained
- Performance benchmarks (target: <5% regression)
- Migration guide document

**Risk Mitigation**:
- Gradual rollout with feature flag
- Extensive integration tests for RAG pipeline
- Performance monitoring with Prometheus alerts
- Rollback plan if quality metrics degrade

---

### Phase 4: DocumentProcessing & GameManagement (3 weeks)
**Goal**: Migrate two medium-complexity contexts in parallel

**Tasks**:
1. **Week 1-2: DocumentProcessing Context**
   - Domain: `PdfDocument`, `Page`, `TextChunk`, `Table`
   - Application: `PdfApplicationService`, `ValidationApplicationService`
   - Infrastructure: `PdfDocumentRepository`, Docnet/iText7 adapters
   - Tests: PDF upload/extraction end-to-end flows

2. **Week 2-3: GameManagement Context**
   - Domain: `Game`, `RuleSpec`, `RuleSpecVersion`, `Comment`
   - Application: `GameApplicationService`, `RuleSpecApplicationService`
   - Infrastructure: `GameRepository`, `RuleSpecRepository`, BGG adapter
   - Tests: Game/RuleSpec CRUD, versioning, diffs

**Deliverables**:
- 2 complete bounded contexts
- 90%+ test coverage maintained
- Migration guide documents

---

### Phase 5: SystemConfiguration & Administration (2 weeks)
**Goal**: Migrate configuration and admin contexts

**Tasks**:
1. **Week 1: SystemConfiguration Context**
   - **Split ConfigurationService (814 lines, 14 operations)** into 4 application services:
     - `ConfigurationApplicationService` (CRUD, validation)
     - `ConfigurationVersioningApplicationService` (History, rollback)
     - `ConfigurationBulkApplicationService` (Bulk, import/export)
     - `ConfigurationCacheApplicationService` (Cache invalidation)
   - Migrate FeatureFlagService, PromptManagementService
   - Tests: Configuration CRUD, versioning, caching

2. **Week 2: Administration Context**
   - Domain: `AdminUser`, `Alert`, `AuditLog`, `Statistic`
   - Application: `UserManagementApplicationService`, `AlertingApplicationService`
   - Infrastructure: Alert adapters (Email, Slack, PagerDuty)
   - Tests: User management, alerting, analytics

**Deliverables**:
- 2 complete bounded contexts
- ConfigurationService split from 814 lines → 4 services (150-250 lines each)
- 90%+ test coverage maintained

---

### Phase 6: WorkflowIntegration & Test Reorganization (2 weeks)
**Goal**: Complete final context migration and reorganize all tests

**Tasks**:
1. **Week 1: WorkflowIntegration Context**
   - Domain: `WorkflowTemplate`, `WorkflowExecution`, `WorkflowError`
   - Application: `WorkflowApplicationService`, `N8nTemplateApplicationService`
   - Infrastructure: n8n API adapter
   - Tests: Workflow execution, error handling, webhooks

2. **Week 2: Test Reorganization**
   - **Split large test files** (1000+ lines → 200-400 lines):
     - `PasswordResetServiceTests.cs` (1454 lines) → Split by feature
     - `RagServiceTests.cs` (1364 lines) → Split by bounded context
     - `LlmServiceTests.cs` (1180 lines) → Split by scenario
   - **Organize tests by bounded context**:
     ```
     tests/
     ├── Authentication.Tests/
     │   ├── Domain/
     │   ├── Application/
     │   └── Integration/
     ├── KnowledgeBase.Tests/
     │   ├── Domain/
     │   ├── Application/
     │   └── Integration/
     └── ...
     ```
   - Extract shared test fixtures → `tests/Shared/Fixtures/`
   - Extract test helpers → `tests/Shared/Helpers/`
   - Update CI pipeline to run tests by bounded context

**Deliverables**:
- Complete WorkflowIntegration bounded context
- All tests reorganized by bounded context
- Test files reduced from 1000+ lines → 200-400 lines
- Shared test infrastructure extracted
- Updated CI pipeline

---

## Frontend Refactoring (Parallel Track)

### Current Problems
- Flat component structure (all in `src/components/`)
- ChatProvider (638 lines) manages too many concerns
- Test files exceed 1000 lines

### Target Structure
```
apps/web/src/
├── features/                  Feature-based organization
│   ├── authentication/        Auth feature
│   │   ├── components/        AuthForm, OAuthButtons, 2FASetup
│   │   ├── hooks/            useAuth, useSession, use2FA
│   │   ├── types/            AuthDto, SessionDto
│   │   ├── api/              auth.api.ts (API client methods)
│   │   └── __tests__/        Feature-focused tests
│   ├── chat/                  Chat feature
│   │   ├── components/        ChatProvider, MessageList, MessageInput
│   │   ├── hooks/            useChat, useChatStreaming
│   │   ├── types/            MessageDto, ChatDto
│   │   ├── api/              chat.api.ts
│   │   └── __tests__/
│   ├── games/                 Game management feature
│   ├── pdf-upload/            PDF upload feature
│   ├── rule-editor/           Rule editor feature
│   └── admin/                 Admin feature
│
├── shared/                    Shared across features
│   ├── components/            Button, Modal, Form, Loading
│   ├── hooks/                useDebounce, useToast, useUploadQueue
│   ├── utils/                api.ts, errors.ts, logger.ts
│   ├── types/                Common types
│   └── __tests__/
│
├── pages/                     Next.js pages (thin, route to features)
│   ├── index.tsx              → features/home
│   ├── chat.tsx               → features/chat
│   ├── upload.tsx             → features/pdf-upload
│   └── admin.tsx              → features/admin
│
└── styles/                    Global styles
```

### Frontend Refactoring Phases
1. **Phase 1**: Extract shared components to `shared/components/`
2. **Phase 2**: Create feature folders for chat, auth, games
3. **Phase 3**: Split ChatProvider (638 lines) → ChatProvider + ChatStateManager + ChatApiManager
4. **Phase 4**: Reorganize tests by feature
5. **Phase 5**: Split large test files (1000+ lines → 200-400 lines)

---

## Benefits & Impact

### Complexity Reduction
- **Services**: 40+ flat services → 7 organized bounded contexts
- **File Sizes**: 700-1000 line services → 200-400 line focused modules
- **Test Files**: 1000-1400 line tests → 200-400 line feature suites
- **Navigation**: Find related code by bounded context instead of searching 40+ files

### Testability Improvements
- **Isolated Tests**: Test domain logic independently from infrastructure
- **Faster Tests**: Unit tests run in-memory, integration tests use Testcontainers
- **Maintainable Tests**: 200-400 line test files easier to read and update
- **Clear Coverage**: Coverage per bounded context visible

### Parallel Development
- **Team Scaling**: Multiple teams can work on different bounded contexts
- **Reduced Conflicts**: Fewer merge conflicts (separate directories)
- **Clear Ownership**: Each team owns specific bounded contexts
- **Independent Deployment**: Future microservices extraction easier

### Maintainability
- **Bounded Contexts**: Clear boundaries reduce coupling
- **Domain Language**: Ubiquitous language in each context
- **Single Responsibility**: Each service has one clear purpose
- **Explicit Dependencies**: Dependencies visible via constructor injection

---

## Risks & Mitigations

### Risk 1: Breaking Changes During Migration
**Mitigation**:
- Dual-run mode (old + new implementations active)
- Feature flags to switch between old/new
- Extensive integration tests
- Gradual rollout per bounded context

### Risk 2: Performance Regression
**Mitigation**:
- Performance benchmarks before/after each phase
- Prometheus metrics for real-time monitoring
- Load testing with realistic workloads
- Rollback plan if >5% regression detected

### Risk 3: Test Coverage Drops
**Mitigation**:
- Enforce 90% coverage in CI pipeline
- Write tests before migration (TDD approach)
- Test reorganization in final phase
- Coverage reports per bounded context

### Risk 4: Team Confusion During Transition
**Mitigation**:
- Comprehensive migration guide documents
- Phase-by-phase documentation
- Knowledge transfer sessions after each phase
- Clear communication about old vs new code locations

### Risk 5: Longer Development Time (12-16 weeks)
**Mitigation**:
- Incremental delivery (value after each phase)
- Parallel migration where possible
- Prioritize high-impact contexts first (Auth, KnowledgeBase)
- Pause regular feature development during refactoring

---

## Success Metrics

### Quantitative Metrics
- **File Size**: Average service file size reduced from 700 lines → 300 lines
- **Test File Size**: Average test file size reduced from 800 lines → 300 lines
- **Test Coverage**: Maintain 90%+ throughout refactoring
- **Performance**: <5% regression in P95 latency
- **Build Time**: CI pipeline completes in <20 minutes
- **Code Duplication**: Reduced by 30% via SharedKernel

### Qualitative Metrics
- **Developer Satisfaction**: Survey team after migration (target: 8/10)
- **Onboarding Time**: New developers find code faster (target: 50% reduction)
- **Bug Rate**: Fewer production bugs due to clearer boundaries
- **Code Review Speed**: Faster reviews with smaller, focused files

---

## Timeline Summary

| Phase | Duration | Focus | Risk Level |
|-------|----------|-------|------------|
| 1. Foundation | 2 weeks | SharedKernel, infrastructure | Low |
| 2. Authentication | 3 weeks | Proof-of-concept migration | Medium |
| 3. KnowledgeBase | 4 weeks | Complex RAG domain, split RagService | High |
| 4. Documents + Games | 3 weeks | Two contexts in parallel | Medium |
| 5. Config + Admin | 2 weeks | Split ConfigurationService | Low |
| 6. Workflows + Tests | 2 weeks | Final context + test reorganization | Low |
| **Total** | **16 weeks** | **7 bounded contexts** | **Medium** |

**Note**: Timeline assumes 1 developer full-time. With 2 developers, estimate 10-12 weeks.

---

## Next Steps

1. **Review & Approve**: Stakeholder review of this plan
2. **Create GitHub Project**: Track all 6 phases with issues
3. **Setup Monitoring**: Prometheus dashboards for performance tracking
4. **Phase 1 Kickoff**: Start SharedKernel implementation
5. **Weekly Sync**: Review progress, adjust timeline as needed

---

## References

- [Domain-Driven Design (Eric Evans)](https://www.domainlanguage.com/ddd/)
- [Implementing Domain-Driven Design (Vaughn Vernon)](https://vaughnvernon.co/)
- [Clean Architecture (Robert C. Martin)](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Vertical Slice Architecture (Jimmy Bogard)](https://www.jimmybogard.com/vertical-slice-architecture/)
- [CQRS Pattern (Martin Fowler)](https://martinfowler.com/bliki/CQRS.html)
