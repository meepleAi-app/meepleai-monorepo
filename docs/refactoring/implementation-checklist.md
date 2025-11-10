# DDD Refactoring Implementation Checklist

## Phase 1: Foundation & Shared Kernel (2 weeks)

### Week 1: SharedKernel Setup
- [ ] Create directory structure:
  - [ ] `src/Api/SharedKernel/Domain/`
  - [ ] `src/Api/SharedKernel/Application/`
  - [ ] `src/Api/SharedKernel/Infrastructure/`
- [ ] Implement base domain classes:
  - [ ] `Entity<TId>` with equality comparison
  - [ ] `AggregateRoot<TId>` with domain events collection
  - [ ] `ValueObject` with value equality
  - [ ] `IDomainEvent` interface
  - [ ] `DomainException` and `ValidationException`
- [ ] Write unit tests for SharedKernel domain classes
- [ ] Add MediatR dependency for CQRS

### Week 2: Infrastructure & Context Structure
- [ ] Implement SharedKernel infrastructure:
  - [ ] `IRepository<TEntity>` interface
  - [ ] `IUnitOfWork` interface
  - [ ] `DomainEventDispatcher`
  - [ ] `ICacheService` abstraction
  - [ ] `ICommand<TResponse>` and `IQuery<TResponse>` interfaces
- [ ] Create bounded context directories:
  - [ ] `BoundedContexts/Authentication/`
  - [ ] `BoundedContexts/DocumentProcessing/`
  - [ ] `BoundedContexts/KnowledgeBase/`
  - [ ] `BoundedContexts/GameManagement/`
  - [ ] `BoundedContexts/SystemConfiguration/`
  - [ ] `BoundedContexts/Administration/`
  - [ ] `BoundedContexts/WorkflowIntegration/`
- [ ] Each context subdirectories:
  - [ ] `Domain/Entities/`, `Domain/ValueObjects/`, `Domain/Services/`
  - [ ] `Application/Commands/`, `Application/Queries/`, `Application/DTOs/`, `Application/Services/`
  - [ ] `Infrastructure/Persistence/`, `Infrastructure/External/`
  - [ ] `Tests/Domain/`, `Tests/Application/`, `Tests/Integration/`
- [ ] Update `Program.cs`:
  - [ ] Add DI registration helpers (e.g., `AddAuthentication()`, `AddKnowledgeBase()`)
  - [ ] Register MediatR
  - [ ] Register domain event dispatcher
- [ ] Update `.csproj` to include new directories
- [ ] Verify build succeeds with empty bounded contexts

---

## Phase 2: Authentication Context (3 weeks)

### Week 1: Domain Layer
- [ ] Define value objects:
  - [ ] `Email` (validation, normalization)
  - [ ] `PasswordHash` (hashing, verification)
  - [ ] `TotpSecret` (encryption, generation)
  - [ ] `ApiKeyHash` (hashing, verification)
- [ ] Define entities:
  - [ ] `User` (aggregate root): Properties, methods (ValidatePassword, Enable2FA)
  - [ ] `Session`: Token, expiration, validation
  - [ ] `ApiKey`: Scopes, rate limits, validation
  - [ ] `OAuthAccount`: Provider, tokens, linking
  - [ ] `BackupCode`: Hashing, single-use enforcement
  - [ ] `TempSession`: 5-min TTL, single-use
- [ ] Define domain services:
  - [ ] `AuthDomainService` (authentication logic)
  - [ ] `TotpDomainService` (TOTP generation/validation)
  - [ ] `SessionDomainService` (session lifecycle)
- [ ] Define domain events:
  - [ ] `UserLoggedIn`, `UserLoggedOut`, `SessionExpired`
  - [ ] `TwoFactorEnabled`, `TwoFactorDisabled`, `BackupCodeUsed`
  - [ ] `OAuthAccountLinked`, `OAuthAccountUnlinked`
  - [ ] `ApiKeyCreated`, `ApiKeyRevoked`
- [ ] Write domain unit tests:
  - [ ] `UserTests.cs` (password validation, 2FA logic)
  - [ ] `SessionTests.cs` (expiration, validation)
  - [ ] `ApiKeyTests.cs` (scope validation, rate limits)
  - [ ] `TotpDomainServiceTests.cs` (TOTP generation/validation)

### Week 2: Application Layer
- [ ] Define commands:
  - [ ] `LoginCommand`, `LogoutCommand`, `RefreshSessionCommand`
  - [ ] `Enable2FACommand`, `Disable2FACommand`, `Verify2FACommand`
  - [ ] `LinkOAuthAccountCommand`, `UnlinkOAuthAccountCommand`
  - [ ] `CreateApiKeyCommand`, `RevokeApiKeyCommand`
- [ ] Define queries:
  - [ ] `GetUserQuery`, `ValidateSessionQuery`, `ValidateApiKeyQuery`
  - [ ] `GetOAuthAccountsQuery`, `Get2FAStatusQuery`
- [ ] Define DTOs:
  - [ ] `UserDto`, `SessionDto`, `ApiKeyDto`, `OAuthAccountDto`
  - [ ] `LoginRequest`, `LoginResponse`, `2FASetupResponse`
- [ ] Implement command handlers:
  - [ ] `LoginCommandHandler` (coordinate domain services, create session)
  - [ ] `Enable2FACommandHandler` (generate TOTP, backup codes)
  - [ ] `LinkOAuthAccountCommandHandler` (OAuth flow, link account)
- [ ] Implement query handlers:
  - [ ] `GetUserQueryHandler` (fetch user, map to DTO)
  - [ ] `ValidateSessionQueryHandler` (validate token, check expiration)
- [ ] Define application services:
  - [ ] `AuthApplicationService` (orchestrate use cases)
  - [ ] `SessionApplicationService` (session management)
  - [ ] `ApiKeyApplicationService` (API key management)
  - [ ] `OAuthApplicationService` (OAuth integration)
- [ ] Write application unit tests:
  - [ ] `LoginCommandHandlerTests.cs`
  - [ ] `Enable2FACommandHandlerTests.cs`
  - [ ] `ValidateSessionQueryHandlerTests.cs`
  - [ ] Mock repositories and external dependencies

### Week 3: Infrastructure & Integration
- [ ] Implement repositories:
  - [ ] `UserRepository` (EF Core, user CRUD)
  - [ ] `SessionRepository` (Redis + Postgres, session CRUD)
  - [ ] `ApiKeyRepository` (Postgres, API key CRUD)
  - [ ] `OAuthAccountRepository` (Postgres, OAuth account CRUD)
  - [ ] `BackupCodeRepository` (Postgres, backup code CRUD)
  - [ ] `TempSessionRepository` (Redis, temp session CRUD)
- [ ] Implement external adapters:
  - [ ] `OAuthProviderAdapter` (Google, Discord, GitHub OAuth)
  - [ ] `EmailSenderAdapter` (SMTP, SendGrid, etc.)
  - [ ] `EncryptionServiceAdapter` (ASP.NET Data Protection)
- [ ] Wire up Authentication context in `Program.cs`:
  - [ ] Register repositories
  - [ ] Register application services
  - [ ] Register MediatR handlers
  - [ ] Register external adapters
  - [ ] **Enable dual-run mode**: Old `AuthService` + new `AuthApplicationService` (feature flag)
- [ ] Create API endpoints:
  - [ ] `AuthenticationEndpoints.cs` (Minimal API endpoints)
  - [ ] `/api/v1/auth/login`, `/api/v1/auth/logout`
  - [ ] `/api/v1/auth/2fa/setup`, `/api/v1/auth/2fa/enable`, `/api/v1/auth/2fa/verify`
  - [ ] `/api/v1/auth/oauth/{provider}/login`, `/api/v1/auth/oauth/{provider}/callback`
- [ ] Write integration tests:
  - [ ] `AuthenticationIntegrationTests.cs` (Testcontainers: Postgres, Redis)
  - [ ] Login flow end-to-end
  - [ ] 2FA flow end-to-end
  - [ ] OAuth flow end-to-end
  - [ ] API key authentication end-to-end
- [ ] Performance benchmarking:
  - [ ] Benchmark old `AuthService` vs new `AuthApplicationService`
  - [ ] Target: <5% regression in P95 latency
  - [ ] Document results in `docs/refactoring/phase2-benchmarks.md`
- [ ] Documentation:
  - [ ] Update API documentation
  - [ ] Write migration guide for developers
  - [ ] Document dual-run mode setup

---

## Phase 3: KnowledgeBase Context (4 weeks)

### Week 1: Domain Layer & Split RagService
- [ ] Define value objects:
  - [ ] `Vector` (dimensions, distance calculation)
  - [ ] `Confidence` (0.0-1.0 range validation)
  - [ ] `Citation` (document ID, page number, snippet)
- [ ] Define entities:
  - [ ] `VectorDocument` (aggregate root): Chunks, metadata
  - [ ] `Embedding` (vector, model, dimensions)
  - [ ] `SearchResult` (document, score, snippet, citation)
  - [ ] `LlmResponse` (content, tokens, confidence, model)
  - [ ] `TextChunk` (content, page, bounding box)
- [ ] **Split RagService (995 lines) into 5 domain services**:
  - [ ] `EmbeddingDomainService` (embedding generation logic, ~150 lines)
  - [ ] `VectorSearchDomainService` (Qdrant search logic, ~200 lines)
  - [ ] `QueryExpansionDomainService` (query variants, synonyms, ~150 lines)
  - [ ] `RrfFusionDomainService` (Reciprocal Rank Fusion, ~180 lines)
  - [ ] `QualityTrackingDomainService` (confidence metrics, ~200 lines)
- [ ] Define domain events:
  - [ ] `EmbeddingGenerated`, `VectorIndexed`, `SearchPerformed`
  - [ ] `LlmResponseGenerated`, `QualityAssessed`
  - [ ] `LowQualityDetected`, `HighConfidenceAchieved`
- [ ] Write domain unit tests:
  - [ ] `VectorDocumentTests.cs`
  - [ ] `EmbeddingDomainServiceTests.cs` (~200-300 lines)
  - [ ] `VectorSearchDomainServiceTests.cs` (~200-300 lines)
  - [ ] `QueryExpansionDomainServiceTests.cs` (~150-200 lines)
  - [ ] `RrfFusionDomainServiceTests.cs` (~180-250 lines)
  - [ ] `QualityTrackingDomainServiceTests.cs` (~200-300 lines)

### Week 2: Application Layer
- [ ] Define commands:
  - [ ] `IndexDocumentCommand` (trigger indexing)
  - [ ] `GenerateEmbeddingCommand` (generate embedding for text)
  - [ ] `AskQuestionCommand` (streaming QA)
- [ ] Define queries:
  - [ ] `SearchQuery` (vector + keyword search)
  - [ ] `GetSearchResultsQuery` (retrieve results by ID)
  - [ ] `EvaluateRagQualityQuery` (quality metrics)
- [ ] Define DTOs:
  - [ ] `SearchResultDto`, `EmbeddingDto`, `LlmResponseDto`
  - [ ] `SearchRequest`, `SearchResponse`, `QualityMetricsDto`
- [ ] Implement command handlers:
  - [ ] `IndexDocumentCommandHandler` (orchestrate PDF → chunks → embeddings → Qdrant)
  - [ ] `AskQuestionCommandHandler` (orchestrate search → LLM → streaming)
- [ ] Implement query handlers:
  - [ ] `SearchQueryHandler` (coordinate 5 domain services)
  - [ ] `EvaluateRagQualityQueryHandler` (calculate P@K, MRR, quality gates)
- [ ] Define application services:
  - [ ] `RagApplicationService` (orchestrate RAG pipeline)
  - [ ] `LlmApplicationService` (LLM integration, streaming)
  - [ ] `StreamingQaApplicationService` (SSE streaming)
  - [ ] `EmbeddingApplicationService` (embedding generation)
- [ ] Write application unit tests:
  - [ ] `SearchQueryHandlerTests.cs` (mock 5 domain services)
  - [ ] `AskQuestionCommandHandlerTests.cs` (mock RAG pipeline)
  - [ ] `RagApplicationServiceTests.cs` (orchestration logic)

### Week 3: Infrastructure & Integration
- [ ] Implement repositories:
  - [ ] `VectorDocumentRepository` (Postgres, document metadata)
  - [ ] `EmbeddingRepository` (tracking embeddings)
- [ ] Implement external adapters:
  - [ ] `QdrantVectorStoreAdapter` (Qdrant client wrapper)
  - [ ] `OpenRouterLlmAdapter` (OpenRouter API wrapper)
  - [ ] `EmbeddingServiceAdapter` (external embedding service)
- [ ] Wire up KnowledgeBase context in `Program.cs`:
  - [ ] Register repositories
  - [ ] Register 5 domain services
  - [ ] Register application services
  - [ ] Register MediatR handlers
  - [ ] Register external adapters
  - [ ] **Feature flag**: `Features:UseNewRagContext` (dual-run mode)
- [ ] Create API endpoints:
  - [ ] `KnowledgeBaseEndpoints.cs` (Minimal API)
  - [ ] `/api/v1/rag/search`, `/api/v1/rag/ask`, `/api/v1/rag/index`
- [ ] Write integration tests:
  - [ ] `RagIntegrationTests.cs` (Testcontainers: Postgres, Qdrant, Redis)
  - [ ] RAG end-to-end flow (PDF → embeddings → search → LLM)
  - [ ] Streaming QA end-to-end
  - [ ] Quality evaluation end-to-end

### Week 4: Performance Validation & Rollout
- [ ] Performance benchmarking:
  - [ ] Benchmark old `RagService` (995 lines) vs new context (5 services)
  - [ ] Target: <5% regression in P95 latency
  - [ ] Load testing: 100 concurrent search requests
  - [ ] Document results in `docs/refactoring/phase3-benchmarks.md`
- [ ] Quality metrics validation:
  - [ ] Monitor AI-11.2 Quality Tracking metrics
  - [ ] Verify no degradation in RAG confidence scores
  - [ ] Compare old vs new RAG evaluation results
- [ ] Gradual rollout:
  - [ ] Enable `Features:UseNewRagContext` for 10% of requests
  - [ ] Monitor Prometheus metrics for 48 hours
  - [ ] Increase to 50% if no issues
  - [ ] Full rollout after 1 week
- [ ] Documentation:
  - [ ] Update RAG architecture documentation
  - [ ] Write migration guide for developers
  - [ ] Document split RagService design decisions

---

## Phase 4: DocumentProcessing & GameManagement (3 weeks)

### Week 1-2: DocumentProcessing Context
- [ ] Domain layer:
  - [ ] Value objects: `FileSize`, `MimeType`, `PageNumber`
  - [ ] Entities: `PdfDocument` (aggregate root), `Page`, `TextChunk`, `Table`
  - [ ] Domain services: `TextExtractionDomainService`, `TableExtractionDomainService`, `ValidationDomainService`
  - [ ] Domain events: `PdfUploaded`, `PdfValidated`, `PdfExtractionCompleted`
  - [ ] Unit tests: `PdfDocumentTests.cs`, `TextExtractionDomainServiceTests.cs`
- [ ] Application layer:
  - [ ] Commands: `UploadPdfCommand`, `ExtractTextCommand`, `ValidatePdfCommand`
  - [ ] Queries: `GetPdfQuery`, `ListPdfsQuery`, `GetExtractionStatusQuery`
  - [ ] DTOs: `PdfDto`, `PageDto`, `ExtractionStatusDto`
  - [ ] Application services: `PdfApplicationService`, `ValidationApplicationService`
  - [ ] Unit tests: `UploadPdfCommandHandlerTests.cs`, `PdfApplicationServiceTests.cs`
- [ ] Infrastructure layer:
  - [ ] Repositories: `PdfDocumentRepository`, `FileStorageRepository`
  - [ ] External adapters: `DocnetAdapter`, `iText7Adapter`, `TesseractAdapter`
  - [ ] Unit tests: `PdfDocumentRepositoryTests.cs`
- [ ] Integration tests:
  - [ ] `PdfUploadIntegrationTests.cs` (upload → extraction → validation)
  - [ ] `OcrFallbackIntegrationTests.cs` (Tesseract OCR)
- [ ] Wire up in `Program.cs` + API endpoints

### Week 2-3: GameManagement Context
- [ ] Domain layer:
  - [ ] Value objects: `GameId`, `Version`, `Diff`
  - [ ] Entities: `Game` (aggregate root), `RuleSpec` (aggregate root), `RuleSpecVersion`, `Comment`
  - [ ] Domain services: `DiffCalculationDomainService`, `VersioningDomainService`
  - [ ] Domain events: `GameCreated`, `RuleSpecCreated`, `VersionCreated`, `CommentAdded`
  - [ ] Unit tests: `GameTests.cs`, `RuleSpecTests.cs`, `DiffCalculationDomainServiceTests.cs`
- [ ] Application layer:
  - [ ] Commands: `CreateGameCommand`, `UpdateRuleSpecCommand`, `AddCommentCommand`
  - [ ] Queries: `GetGameQuery`, `GetRuleSpecQuery`, `ListVersionsQuery`
  - [ ] DTOs: `GameDto`, `RuleSpecDto`, `CommentDto`, `DiffDto`
  - [ ] Application services: `GameApplicationService`, `RuleSpecApplicationService`
  - [ ] Unit tests: `CreateGameCommandHandlerTests.cs`, `RuleSpecApplicationServiceTests.cs`
- [ ] Infrastructure layer:
  - [ ] Repositories: `GameRepository`, `RuleSpecRepository`, `CommentRepository`
  - [ ] External adapters: `BggApiAdapter` (BoardGameGeek integration)
  - [ ] Unit tests: `GameRepositoryTests.cs`
- [ ] Integration tests:
  - [ ] `GameManagementIntegrationTests.cs` (CRUD, versioning, diffs)
  - [ ] `BggIntegrationTests.cs` (BGG search, details)
- [ ] Wire up in `Program.cs` + API endpoints

---

## Phase 5: SystemConfiguration & Administration (2 weeks)

### Week 1: SystemConfiguration Context
- [ ] **Split ConfigurationService (814 lines, 14 operations) into 4 services**:
  - [ ] `ConfigurationApplicationService` (CRUD, validation, ~200 lines)
  - [ ] `ConfigurationVersioningApplicationService` (History, rollback, ~200 lines)
  - [ ] `ConfigurationBulkApplicationService` (Bulk updates, import/export, ~220 lines)
  - [ ] `ConfigurationCacheApplicationService` (Cache invalidation, ~180 lines)
- [ ] Domain layer:
  - [ ] Value objects: `ConfigKey`, `ConfigValue`, `ConfigEnvironment`
  - [ ] Entities: `Configuration` (aggregate root), `FeatureFlag` (aggregate root), `PromptTemplate`, `PromptVersion`
  - [ ] Domain services: `ConfigurationDomainService`, `FeatureFlagDomainService`, `PromptEvaluationDomainService`
  - [ ] Domain events: `ConfigurationUpdated`, `FeatureFlagToggled`, `PromptVersionActivated`
  - [ ] Unit tests: `ConfigurationTests.cs`, `FeatureFlagTests.cs`, `PromptTemplateTests.cs`
- [ ] Application layer:
  - [ ] Commands: `UpdateConfigCommand`, `ToggleFlagCommand`, `ActivatePromptCommand`, `BulkUpdateConfigCommand`
  - [ ] Queries: `GetConfigQuery`, `GetPromptQuery`, `GetConfigHistoryQuery`
  - [ ] DTOs: `ConfigDto`, `FeatureFlagDto`, `PromptDto`
  - [ ] Unit tests for 4 application services (~200-300 lines each)
- [ ] Infrastructure + Integration:
  - [ ] Repositories: `ConfigurationRepository`, `PromptTemplateRepository`
  - [ ] Cache adapters: `RedisCacheAdapter`
  - [ ] Integration tests: `ConfigurationIntegrationTests.cs`, `PromptManagementIntegrationTests.cs`
- [ ] Wire up in `Program.cs` + API endpoints

### Week 2: Administration Context
- [ ] Domain layer:
  - [ ] Value objects: `Role`, `Permission`, `AlertSeverity`
  - [ ] Entities: `AdminUser` (aggregate root), `Alert`, `AuditLog`, `Statistic`
  - [ ] Domain services: `UserManagementDomainService`, `AlertingDomainService`
  - [ ] Domain events: `UserCreated`, `RoleAssigned`, `AlertSent`, `AuditLogCreated`
  - [ ] Unit tests: `AdminUserTests.cs`, `AlertTests.cs`, `AuditLogTests.cs`
- [ ] Application layer:
  - [ ] Commands: `CreateUserCommand`, `UpdateRoleCommand`, `SendAlertCommand`
  - [ ] Queries: `GetUsersQuery`, `GetStatsQuery`, `GetAuditLogsQuery`
  - [ ] DTOs: `AdminUserDto`, `AlertDto`, `StatisticsDto`, `AuditLogDto`
  - [ ] Application services: `UserManagementApplicationService`, `StatsApplicationService`, `AlertingApplicationService`
  - [ ] Unit tests: `UserManagementApplicationServiceTests.cs`, `AlertingApplicationServiceTests.cs`
- [ ] Infrastructure layer:
  - [ ] Repositories: `AdminUserRepository`, `AuditLogRepository`, `StatisticRepository`
  - [ ] External adapters: `EmailAlertSender`, `SlackAlertSender`, `PagerDutyAdapter`
  - [ ] Unit tests: `AdminUserRepositoryTests.cs`
- [ ] Integration tests:
  - [ ] `UserManagementIntegrationTests.cs` (CRUD, role assignment)
  - [ ] `AlertingIntegrationTests.cs` (Email, Slack, PagerDuty)
  - [ ] `StatsIntegrationTests.cs` (metrics, analytics, CSV/JSON export)
- [ ] Wire up in `Program.cs` + API endpoints

---

## Phase 6: WorkflowIntegration & Test Reorganization (2 weeks)

### Week 1: WorkflowIntegration Context
- [ ] Domain layer:
  - [ ] Value objects: `TemplateId`, `ExecutionStatus`, `ErrorSeverity`
  - [ ] Entities: `WorkflowTemplate` (aggregate root), `WorkflowExecution`, `WorkflowError`
  - [ ] Domain services: `WorkflowDomainService`
  - [ ] Domain events: `WorkflowExecuted`, `WorkflowFailed`, `ErrorLogged`
  - [ ] Unit tests: `WorkflowTemplateTests.cs`, `WorkflowDomainServiceTests.cs`
- [ ] Application layer:
  - [ ] Commands: `ExecuteWorkflowCommand`, `HandleWebhookCommand`, `LogErrorCommand`
  - [ ] Queries: `GetTemplateQuery`, `GetErrorsQuery`, `ListTemplatesQuery`
  - [ ] DTOs: `TemplateDto`, `ExecutionDto`, `ErrorDto`
  - [ ] Application services: `WorkflowApplicationService`, `N8nTemplateApplicationService`
  - [ ] Unit tests: `WorkflowApplicationServiceTests.cs`, `N8nTemplateApplicationServiceTests.cs`
- [ ] Infrastructure layer:
  - [ ] Repositories: `WorkflowTemplateRepository`, `WorkflowExecutionRepository`
  - [ ] External adapters: `N8nApiAdapter` (n8n API wrapper)
  - [ ] Unit tests: `N8nApiAdapterTests.cs`
- [ ] Integration tests:
  - [ ] `WorkflowIntegrationTests.cs` (workflow execution, error handling)
  - [ ] `N8nWebhookIntegrationTests.cs` (webhook handling)
- [ ] Wire up in `Program.cs` + API endpoints

### Week 2: Test Reorganization
- [ ] **Split large backend test files** (1000+ lines → 200-400 lines):
  - [ ] `PasswordResetServiceTests.cs` (1454 lines) → Split into:
    - [ ] `PasswordResetCommandHandlerTests.cs` (~400 lines)
    - [ ] `PasswordResetDomainServiceTests.cs` (~350 lines)
    - [ ] `PasswordResetIntegrationTests.cs` (~400 lines)
    - [ ] `PasswordResetEdgeCasesTests.cs` (~300 lines)
  - [ ] `RagServiceTests.cs` (1364 lines) → Split by bounded context:
    - [ ] `EmbeddingDomainServiceTests.cs` (~250 lines)
    - [ ] `VectorSearchDomainServiceTests.cs` (~280 lines)
    - [ ] `QueryExpansionDomainServiceTests.cs` (~200 lines)
    - [ ] `RrfFusionDomainServiceTests.cs` (~230 lines)
    - [ ] `QualityTrackingDomainServiceTests.cs` (~200 lines)
    - [ ] `RagIntegrationTests.cs` (~200 lines)
  - [ ] `LlmServiceTests.cs` (1180 lines) → Split by scenario:
    - [ ] `LlmServiceUnitTests.cs` (~400 lines)
    - [ ] `LlmServiceStreamingTests.cs` (~380 lines)
    - [ ] `LlmServiceErrorHandlingTests.cs` (~400 lines)
- [ ] **Reorganize tests by bounded context**:
  - [ ] Move tests to bounded context directories:
    ```
    tests/
    ├── Authentication.Tests/
    │   ├── Domain/         (unit tests)
    │   ├── Application/    (unit tests)
    │   └── Integration/    (integration tests)
    ├── KnowledgeBase.Tests/
    │   ├── Domain/
    │   ├── Application/
    │   └── Integration/
    ├── ... (other contexts)
    └── Shared/
        ├── Fixtures/       (shared test fixtures)
        └── Helpers/        (shared test helpers)
    ```
- [ ] **Extract shared test infrastructure**:
  - [ ] Move `WebApplicationFactoryFixture.cs` (1036 lines) → `tests/Shared/Fixtures/`
  - [ ] Extract test helpers → `tests/Shared/Helpers/` (e.g., `DatabaseTestHelpers.cs`, `ApiTestHelpers.cs`)
  - [ ] Extract test data builders → `tests/Shared/Builders/` (e.g., `UserBuilder.cs`, `GameBuilder.cs`)
- [ ] **Update CI pipeline**:
  - [ ] Configure test execution per bounded context
  - [ ] Parallel test execution (run contexts in parallel)
  - [ ] Coverage reports per bounded context
  - [ ] Update test discovery patterns
- [ ] **Frontend test reorganization** (if time permits):
  - [ ] Split `TimelineEventList.test.tsx` (1123 lines) → 3 files (~350 lines each)
  - [ ] Split `AccessibleModal.test.tsx` (718 lines) → 2 files (~350 lines each)
  - [ ] Organize tests by feature folders
  - [ ] Extract shared test fixtures → `__tests__/fixtures/`

---

## Post-Migration Tasks

### Documentation
- [ ] Update architecture diagrams
- [ ] Update API documentation (Swagger/OpenAPI)
- [ ] Update developer onboarding guide
- [ ] Create bounded context glossary (ubiquitous language)
- [ ] Document migration lessons learned

### Cleanup
- [ ] Remove old services (after migration complete):
  - [ ] Delete old `Services/` directory
  - [ ] Remove dual-run mode feature flags
  - [ ] Clean up old test files
- [ ] Archive migration documentation
- [ ] Update README.md with new structure

### Validation
- [ ] Final performance benchmarks (old vs new)
- [ ] Final test coverage report (target: 90%+)
- [ ] Code quality metrics (cyclomatic complexity, duplication)
- [ ] Developer satisfaction survey
- [ ] Production monitoring (1 week post-migration)

---

## Success Criteria

### Quantitative
- [ ] All 7 bounded contexts migrated
- [ ] Test coverage maintained at 90%+
- [ ] Average service file size reduced from 700 → 300 lines
- [ ] Average test file size reduced from 800 → 300 lines
- [ ] Performance: <5% regression in P95 latency
- [ ] Build time: CI pipeline <20 minutes
- [ ] Code duplication reduced by 30%

### Qualitative
- [ ] Developer satisfaction survey: 8/10 or higher
- [ ] Onboarding time reduced by 50%
- [ ] Code review speed improved
- [ ] Fewer production bugs (track for 3 months)

---

## Rollback Plan

If critical issues arise during any phase:

1. **Immediate Action**:
   - [ ] Disable feature flag for new implementation
   - [ ] Revert to old service implementation
   - [ ] Monitor metrics to confirm stability

2. **Investigation**:
   - [ ] Identify root cause of issue
   - [ ] Document issue in migration log
   - [ ] Create GitHub issue with reproduction steps

3. **Resolution**:
   - [ ] Fix issue in new implementation
   - [ ] Validate fix with integration tests
   - [ ] Re-enable feature flag with gradual rollout

4. **Communication**:
   - [ ] Notify stakeholders of rollback
   - [ ] Provide timeline for fix and re-deployment
   - [ ] Document lessons learned

---

## Notes

- Checkboxes should be checked as tasks are completed
- Each phase should be tracked in a GitHub Project with issues
- Weekly sync meetings to review progress and adjust timeline
- Pause regular feature development during refactoring
- Prioritize maintaining test coverage throughout migration
- Document all design decisions in `docs/refactoring/decisions/`
