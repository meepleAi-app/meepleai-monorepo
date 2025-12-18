# Feature-to-Class Mapping - MeepleAI

> Mappa completa delle feature con classi coinvolte (Frontend/Backend/Infra)
>
> **Ultimo aggiornamento**: 2025-12-14

---

## Indice

1. [Authentication](#1-authentication)
2. [Game Management](#2-game-management)
3. [Knowledge Base & RAG](#3-knowledge-base--rag)
4. [Document Processing](#4-document-processing)
5. [Workflow Integration](#5-workflow-integration)
6. [System Configuration](#6-system-configuration)
7. [Administration](#7-administration)
8. [Cross-Cutting Concerns](#8-cross-cutting-concerns)
9. [Riepilogo](#riepilogo)

---

## 1. AUTHENTICATION

**Bounded Context**: `Api.BoundedContexts.Authentication`

### 1.1 User Registration/Login

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Creazione account, autenticazione email/password, gestione sessioni |
| **Frontend** | `app/login/`, `app/auth/`, `useAuth.ts`, `useAuthUser.ts`, `useSessionCheck.ts`, `lib/api.ts` |
| **Backend - Endpoints** | `AuthenticationEndpoints.cs` |
| **Backend - Commands** | `RegisterCommand`, `RegisterCommandHandler`, `LoginCommand`, `LoginCommandHandler`, `LogoutCommand`, `LogoutCommandHandler` |
| **Backend - Domain** | `User` entity, `Session` entity |
| **Backend - Repositories** | `UserRepository`, `SessionRepository` |
| **Infrastructure** | PostgreSQL (Users, Sessions tables), Redis session cache, HTTPS/TLS |

### 1.2 API Key Management

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Genera, ruota, revoca API keys per accesso programmatico (`mpl_{env}_{base64}`) |
| **Frontend** | `app/settings/api-keys/`, API key management hooks |
| **Backend - Endpoints** | `ApiKeyEndpoints.cs` |
| **Backend - Commands** | `CreateApiKeyManagementCommand`, `RotateApiKeyCommand`, `RevokeApiKeyManagementCommand`, `DeleteApiKeyCommand` |
| **Backend - Queries** | `ListApiKeysQuery`, `GetApiKeyQuery`, `GetApiKeyUsageQuery`, `GetAllApiKeysWithStatsQuery` |
| **Backend - Domain** | `ApiKey` entity |
| **Backend - Repositories** | `ApiKeyRepository` |
| **Infrastructure** | PostgreSQL (ApiKeys table), PBKDF2 encryption, HMAC signing |

### 1.3 Two-Factor Authentication (2FA)

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | TOTP-based 2FA con backup codes, admin override per utenti bloccati |
| **Frontend** | `app/auth/2fa/`, `useAuth.ts` (2FA flow) |
| **Backend - Endpoints** | `TwoFactorEndpoints.cs` |
| **Backend - Commands** | `GenerateTotpSetupCommand`, `Enable2FACommand`, `Verify2FACommand`, `Disable2FACommand`, `AdminDisable2FACommand` |
| **Backend - Queries** | `Get2FAStatusQuery` |
| **Backend - Domain** | `TwoFactorAuth` value object, TOTP generation |
| **Infrastructure** | TOTP library, PostgreSQL (2FA table), Email notifications |

### 1.4 OAuth Integration

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Autenticazione tramite Google, Discord, GitHub |
| **Frontend** | `app/oauth-callback/`, OAuth login buttons components |
| **Backend - Endpoints** | `OAuthEndpoints.cs` |
| **Backend - Commands** | `LinkOAuthAccountCommand`, `AuthorizeOAuthCommand` |
| **Backend - Domain** | `OAuthAccount` entity |
| **Backend - Repositories** | `OAuthAccountRepository` |
| **Infrastructure** | OAuth client secrets (encrypted), PostgreSQL (OAuthAccounts table) |

### 1.5 Password Management

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Reset password, cambio password |
| **Frontend** | `app/auth/reset-password/`, `app/auth/forgot-password/` |
| **Backend - Endpoints** | `PasswordEndpoints.cs` |
| **Backend - Commands** | `ResetPasswordCommand`, `ChangePasswordCommand`, `ForgotPasswordCommand` |
| **Infrastructure** | Email service, Token validation, PostgreSQL |

### 1.6 Session Management

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Gestione sessioni attive, logout remoto, invalidazione |
| **Frontend** | `useSessionCheck.ts`, session list component |
| **Backend - Endpoints** | `SessionEndpoints.cs` |
| **Backend - Commands** | `InvalidateSessionCommand`, `InvalidateAllSessionsCommand` |
| **Backend - Queries** | `GetActiveSessionsQuery` |
| **Infrastructure** | Redis sessions, Cookie httpOnly/secure |

---

## 2. GAME MANAGEMENT

**Bounded Context**: `Api.BoundedContexts.GameManagement`

### 2.1 Game Catalog CRUD

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Gestione catalogo giochi da tavolo con metadata (titolo, editore, anno, giocatori, durata) |
| **Frontend** | `app/games/`, `app/board-game-ai/`, `useGames.ts`, `useDocumentsByGame.ts`, game list/detail/form components |
| **Backend - Endpoints** | `GameEndpoints.cs` |
| **Backend - Commands** | `CreateGameCommand`, `UpdateGameCommand`, `DeleteGameCommand` |
| **Backend - Queries** | `GetAllGamesQuery`, `GetGameByIdQuery`, `GetGameDetailsQuery`, `GetRuleSpecsQuery` |
| **Backend - Domain** | `Game` entity (Title, Publisher, YearPublished, MinPlayers, MaxPlayers, PlayTime) |
| **Backend - Repositories** | `GameRepository` |
| **Infrastructure** | PostgreSQL (Games table), Full-text search on titles |

### 2.2 Game Sessions

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Tracking sessioni di gioco con state machine (Active, Paused, Completed, Abandoned) |
| **Frontend** | `app/chat/`, `app/dashboard/`, `useChatWithStreaming.ts`, `useDashboardData.ts`, session list/details components |
| **Backend - Endpoints** | `GameEndpoints.cs` |
| **Backend - Commands** | `StartGameSessionCommand`, `AddPlayerToSessionCommand`, `CompleteGameSessionCommand`, `AbandonGameSessionCommand`, `PauseGameSessionCommand`, `ResumeGameSessionCommand`, `EndGameSessionCommand` |
| **Backend - Queries** | `GetGameSessionByIdQuery`, `GetActiveSessionsByGameQuery`, `GetActiveSessionsQuery`, `GetSessionHistoryQuery`, `GetSessionStatsQuery` |
| **Backend - Domain** | `GameSession` entity con state machine |
| **Backend - Repositories** | `GameSessionRepository` |
| **Infrastructure** | PostgreSQL (GameSessions table), Redis cache per sessioni attive |

### 2.3 Game FAQs

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | FAQ community-driven per regole e chiarimenti con sistema di upvoting |
| **Frontend** | `app/games/{id}/faqs/`, FAQ list/form/upvote components |
| **Backend - Endpoints** | `GameEndpoints.cs` |
| **Backend - Commands** | `CreateGameFAQCommand`, `UpdateGameFAQCommand`, `DeleteGameFAQCommand`, `UpvoteGameFAQCommand` |
| **Backend - Queries** | `GetGameFAQsQuery` |
| **Backend - Domain** | `GameFAQ` entity (Question, Answer, VoteCount) |
| **Backend - Repositories** | `GameFAQRepository` |
| **Infrastructure** | PostgreSQL (FAQs table), Vote tracking |

### 2.4 Rule Specifications

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Regole strutturate associate a ciascun gioco |
| **Frontend** | `useDocumentsByGame.ts`, rules display component |
| **Backend - Queries** | `GetRuleSpecsQuery` |
| **Backend - Domain** | `RuleSpec` entity |
| **Infrastructure** | PostgreSQL (RuleSpecs table) |

---

## 3. KNOWLEDGE BASE & RAG

**Bounded Context**: `Api.BoundedContexts.KnowledgeBase`

### 3.1 Hybrid RAG Search

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Ricerca ibrida Vector (70%) + Keyword (30%) con Reciprocal Rank Fusion |
| **Frontend** | `app/board-game-ai/`, `useSearch.ts`, search input/results components |
| **Backend - Endpoints** | `KnowledgeBaseEndpoints.cs` |
| **Backend - Queries** | `SearchQuery`, `AskQuestionQuery` |
| **Backend - Handlers** | Hybrid search handler (Qdrant vectors + PostgreSQL FTS) con RRF ranking |
| **Backend - Domain** | Vector document indexing, semantic search logic |
| **Backend - Repositories** | `VectorDocumentRepository` |
| **Infrastructure** | **Qdrant** :6333 (vector DB), PostgreSQL full-text search, OpenRouter LLM API, Sentence transformers embeddings |

### 3.2 Chat Threads

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Conversazioni multi-turn persistenti con risposte in streaming (SSE) |
| **Frontend** | `app/chat/`, `useChatWithStreaming.ts`, `useChatOptimistic.ts`, `useChats.ts`, chat message list/input/thread list components |
| **Backend - Endpoints** | `KnowledgeBaseEndpoints.cs` |
| **Backend - Commands** | `CreateChatThreadCommand`, `AddMessageCommand`, `DeleteChatThreadCommand` |
| **Backend - Queries** | `GetChatThreadByIdQuery`, `GetMyChatHistoryQuery`, `SearchChatHistoryQuery` |
| **Backend - Domain** | `ChatThread` entity, `ChatMessage` entity |
| **Backend - Repositories** | `ChatThreadRepository`, `ChatMessageRepository` |
| **Infrastructure** | PostgreSQL (ChatThreads, ChatMessages tables), Redis cache, **Server-Sent Events (SSE)** streaming |

### 3.3 Chat Export

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Esporta cronologia conversazione in PDF o Markdown |
| **Frontend** | Export button component in chat |
| **Backend - Endpoints** | `KnowledgeBaseEndpoints.cs` |
| **Backend - Commands** | `ExportChatCommand` |
| **Backend - Handlers** | PDF/Markdown generation con formatting |
| **Infrastructure** | PDF generation library, file download via response stream |

### 3.4 Agent Management

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Gestione agenti RAG configurabili |
| **Frontend** | `app/admin/agents/`, agent list/form components |
| **Backend - Commands** | `CreateAgentCommand`, `UpdateAgentCommand`, `DeleteAgentCommand` |
| **Backend - Queries** | `GetAgentsQuery`, `GetAgentByIdQuery` |
| **Backend - Domain** | `Agent` entity |
| **Backend - Repositories** | `AgentRepository` |
| **Infrastructure** | PostgreSQL (Agents table) |

---

## 4. DOCUMENT PROCESSING

**Bounded Context**: `Api.BoundedContexts.DocumentProcessing`

### 4.1 PDF Upload Pipeline

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Estrazione PDF 3-tier con quality validation orchestrata |
| **Frontend** | `app/upload/`, `app/giochi/`, `useChunkedUpload.ts`, `useUploadQueue.ts`, file upload/progress/validation components |
| **Backend - Endpoints** | `PdfEndpoints.cs` |
| **Backend - Commands** | `UploadPdfCommand` |
| **Backend - Handlers** | Multi-stage pipeline orchestration |
| **Backend - Domain** | `PdfDocument` entity (status: Pending → Unstructured → SmolDocling → Docnet) |
| **Backend - Repositories** | `PdfDocumentRepository` |
| **Infrastructure** | **Stage 1**: Unstructured.io (confidence ≥ 0.80), **Stage 2**: SmolDocling (confidence ≥ 0.70), **Stage 3**: Docnet (fallback), S3/blob storage |

### 4.2 Document Collections

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Organizza e tagga documenti caricati in collezioni |
| **Frontend** | `app/documents/`, collection list/organizer components |
| **Backend - Endpoints** | `DocumentCollectionEndpoints.cs` |
| **Backend - Commands** | `CreateDocumentCollectionCommand`, `UpdateDocumentCollectionCommand`, `DeleteDocumentCollectionCommand` |
| **Backend - Queries** | `GetDocumentCollectionsQuery` |
| **Backend - Domain** | `DocumentCollection` entity |
| **Backend - Repositories** | `DocumentCollectionRepository` |
| **Infrastructure** | PostgreSQL (Collections table), relational mapping |

### 4.3 BoardGameGeek Integration

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Auto-fetch metadata gioco da BGG durante upload PDF |
| **Frontend** | Auto-populated game metadata UI |
| **Backend - Handlers** | BGG API lookup on PDF ingestion |
| **Backend - Integration** | HTTP client to BGG API |
| **Infrastructure** | External BoardGameGeek API |

---

## 5. WORKFLOW INTEGRATION

**Bounded Context**: `Api.BoundedContexts.WorkflowIntegration`

### 5.1 n8n Configuration

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Configurazione istanze n8n per automazione custom |
| **Frontend** | `app/admin/n8n-config/`, config form/connection test components |
| **Backend - Endpoints** | `WorkflowEndpoints.cs` |
| **Backend - Commands** | `CreateN8NConfigCommand`, `UpdateN8NConfigCommand`, `DeleteN8NConfigCommand`, `TestN8NConnectionCommand` |
| **Backend - Queries** | `GetAllN8NConfigsQuery`, `GetN8NConfigByIdQuery` |
| **Backend - Domain** | `N8NConfig` entity (con encrypted API key) |
| **Backend - Repositories** | `N8NConfigRepository` |
| **Infrastructure** | **n8n** :5678, PostgreSQL (configs table), Encryption service |

### 5.2 Workflow Templates

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Template pre-built per automazioni comuni |
| **Frontend** | `app/admin/templates/`, template library browser |
| **Backend - Endpoints** | `WorkflowEndpoints.cs` |
| **Backend - Commands** | `ImportN8NTemplateCommand` |
| **Backend - Queries** | `GetN8NTemplatesQuery`, `GetN8NTemplateByIdQuery`, `ValidateN8NTemplateQuery` |
| **Infrastructure** | Template JSON storage, validation |

### 5.3 Workflow Error Logging

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Cattura e tracking errori esecuzione workflow n8n |
| **Frontend** | `app/admin/workflows/`, error log viewer |
| **Backend - Endpoints** | `WorkflowEndpoints.cs` |
| **Backend - Commands** | `LogWorkflowErrorCommand` |
| **Backend - Queries** | `GetWorkflowErrorsQuery`, `GetWorkflowErrorByIdQuery` |
| **Backend - Domain** | `WorkflowError` entity (WorkflowId, ExecutionId, ErrorMessage, NodeName, StackTrace) |
| **Backend - Repositories** | `WorkflowErrorRepository` |
| **Infrastructure** | PostgreSQL (WorkflowErrors table), webhook integration |

---

## 6. SYSTEM CONFIGURATION

**Bounded Context**: `Api.BoundedContexts.SystemConfiguration`

### 6.1 Dynamic Configuration

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Configurazione runtime con versioning e rollback (3-tier: DB → appsettings → defaults) |
| **Frontend** | `app/admin/configuration/`, config editor/version history components |
| **Backend - Endpoints** | `ConfigurationEndpoints.cs` |
| **Backend - Commands** | `CreateConfigurationCommand`, `UpdateConfigValueCommand`, `DeleteConfigurationCommand`, `ToggleConfigurationCommand`, `BulkUpdateConfigsCommand`, `ImportConfigsCommand`, `RollbackConfigCommand`, `ValidateConfigCommand`, `InvalidateCacheCommand` |
| **Backend - Queries** | `GetAllConfigsQuery`, `GetConfigByIdQuery`, `GetConfigByKeyQuery`, `GetConfigHistoryQuery`, `GetConfigCategoriesQuery`, `ExportConfigsQuery` |
| **Backend - Domain** | `Configuration` entity (Key-Value con metadata, versioning) |
| **Backend - Repositories** | `ConfigurationRepository` |
| **Infrastructure** | PostgreSQL (Configurations table con version tracking), **HybridCache L1+L2**, Environment-scoped configs |

### 6.2 Feature Flags

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Toggle feature a runtime senza deploy |
| **Frontend** | `useFeatureFlags()` hook |
| **Backend - Service** | `IFeatureFlagService` |
| **Backend - Checks** | `Features.N8NIntegration`, `Features.RAGSearch`, etc. |
| **Backend - Storage** | Configuration system con chiavi `Features.*` |
| **Infrastructure** | PostgreSQL config-backed flags, Redis cache |

### 6.3 Config Import/Export

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Bulk operations su configurazioni |
| **Frontend** | Config editor bulk UI |
| **Backend - Commands** | `ImportConfigsCommand`, `BulkUpdateConfigsCommand` |
| **Backend - Queries** | `ExportConfigsQuery` |
| **Infrastructure** | JSON import/export |

---

## 7. ADMINISTRATION

**Bounded Context**: `Api.BoundedContexts.Administration`

### 7.1 User Management

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | CRUD utenti, assegnazione ruoli, gestione tier, bulk operations |
| **Frontend** | `app/admin/users/`, user list/form/bulk operations components |
| **Backend - Endpoints** | `AdminUserEndpoints.cs` |
| **Backend - Commands** | `CreateUserCommand`, `UpdateUserCommand`, `DeleteUserCommand`, `UpdateUserTierCommand`, `BulkPasswordResetCommand`, `BulkRoleChangeCommand`, `BulkImportUsersCommand` |
| **Backend - Queries** | `GetAllUsersQuery`, `SearchUsersQuery`, `BulkExportUsersQuery` |
| **Backend - Domain** | `User` entity con subscription tiers |
| **Backend - Repositories** | `UserRepository` |
| **Infrastructure** | PostgreSQL (Users table), CSV import/export |

### 7.2 Subscription Tiers

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Controllo limiti accesso per tier |
| **Frontend** | Tier badge UI, upgrade prompts |
| **Backend - Commands** | `UpdateUserTierCommand` con validation |
| **Backend - Domain** | Tier validation logic |
| **Backend - Enforcement** | Rate limiting in PDF upload handlers |
| **Infrastructure** | PostgreSQL Users.Tier field |

**Limiti per Tier**:
| Tier | PDF/giorno | PDF/settimana |
|------|------------|---------------|
| Free | 5 | 20 |
| Normal | 20 | 100 |
| Premium | 100 | 500 |

### 7.3 Audit Logging

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Tracking azioni utente per compliance e debugging |
| **Frontend** | `app/admin/activity/`, activity timeline component |
| **Backend - Endpoints** | `AdminUserEndpoints.cs` |
| **Backend - Queries** | `GetUserActivityQuery` |
| **Backend - Domain** | `AuditLog` entity |
| **Backend - Repositories** | `AuditLogRepository` |
| **Infrastructure** | PostgreSQL (AuditLogs table), action/resource filtering |

### 7.4 Alert Management

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Integrazione con Prometheus AlertManager per monitoring |
| **Frontend** | `app/admin/alerts/`, active alerts/history components |
| **Backend - Endpoints** | `AlertEndpoints.cs` |
| **Backend - Service** | `IAlertingService` (SendAlertAsync, ResolveAlertAsync) |
| **Backend - Domain** | `Alert` entity (type, severity, message, metadata) |
| **Infrastructure** | **Prometheus** :9090, **AlertManager** webhook, Email/Slack notifications |

### 7.5 Analytics & Reporting

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Statistiche uso, tracking costi LLM, metriche performance |
| **Frontend** | `app/admin/analytics/`, `app/dashboard/`, stats cards/charts components |
| **Backend - Endpoints** | `AnalyticsEndpoints.cs`, `LlmAnalyticsEndpoints.cs` |
| **Backend - Queries** | Query execution metrics, LLM token counts |
| **Infrastructure** | Prometheus metrics export, **Grafana** :3001 dashboards |

---

## 8. CROSS-CUTTING CONCERNS

### 8.1 Dual Authentication

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Cookie httpOnly/secure + API Key `mpl_{env}_{base64}` |
| **Frontend** | `lib/api.ts`, credentials handling |
| **Backend - Filters** | `RequireSessionFilter`, `RequireAuthenticatedUserFilter`, `RequireAdminSessionFilter` |
| **Infrastructure** | Redis session storage |

### 8.2 Rate Limiting

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Bucket algorithm con refill rate configurabile |
| **Frontend** | Error handling UI (429 responses) |
| **Backend - Middleware** | Rate limit middleware |
| **Infrastructure** | Redis token buckets |

### 8.3 Caching Strategy

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | HybridCache L1+L2 per configurazioni |
| **Backend - Service** | `IHybridCache` |
| **Infrastructure** | Redis per: sessioni attive, chat threads, user sessions, rate limit tokens |

### 8.4 Observability

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Logging, tracing, metriche |
| **Backend - Logging** | Serilog → HyperDX structured logging |
| **Backend - Tracing** | OpenTelemetry integration |
| **Backend - Metrics** | Prometheus endpoints (request counts/latency, DB pool, cache hits, LLM tokens) |
| **Infrastructure** | **HyperDX**, Prometheus, Grafana |

### 8.5 Health Checks

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Endpoint `/health` per monitoring |
| **Backend - Endpoints** | Health check endpoints |
| **Infrastructure** | Docker healthcheck |

### 8.6 Share Links

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Link condivisibili per chat e risultati ricerca |
| **Frontend** | Share button component |
| **Backend - Endpoints** | `ShareLinkEndpoints.cs` |
| **Backend - Commands** | `CreateShareLinkCommand` |
| **Infrastructure** | PostgreSQL (ShareLinks table) |

### 8.7 User Profile

| Layer | Classi/File |
|-------|-------------|
| **Funzionalità** | Aggiorna display name, email, password, preferenze notifiche |
| **Frontend** | `app/settings/profile/` |
| **Backend - Endpoints** | `UserProfileEndpoints.cs` |
| **Backend - Commands** | `UpdateProfileCommand`, `UpdateNotificationPreferencesCommand` |
| **Infrastructure** | PostgreSQL (Users table) |

---

## Riepilogo

### Conteggi per Bounded Context

| Bounded Context | Features | Commands | Queries | Endpoint Files |
|-----------------|----------|----------|---------|----------------|
| **Authentication** | 6 | 12 | 8 | 4 |
| **GameManagement** | 4 | 10 | 8 | 1 |
| **KnowledgeBase** | 4 | 6 | 6 | 1 |
| **DocumentProcessing** | 3 | 3 | 3 | 2 |
| **WorkflowIntegration** | 3 | 5 | 5 | 1 |
| **SystemConfiguration** | 3 | 8 | 6 | 1 |
| **Administration** | 5 | 8 | 6 | 3 |
| **Cross-Cutting** | 7 | 3 | 2 | 2 |
| **TOTALE** | **35** | **55** | **44** | **15** |

### Stack Infrastrutturale

| Servizio | Porta | Scopo |
|----------|-------|-------|
| PostgreSQL | 5432 | Database principale |
| Qdrant | 6333 | Vector DB per RAG |
| Redis | 6379 | Cache, sessions, rate limits |
| n8n | 5678 | Workflow automation |
| Prometheus | 9090 | Metriche |
| Grafana | 3001 | Dashboard |
| API | 8080 | Backend ASP.NET 9 |
| Web | 3000 | Frontend Next.js 16 |

### Pattern Architetturali

- **DDD**: Domain-Driven Design con 7 Bounded Contexts
- **CQRS**: Command/Query Responsibility Segregation
- **MediatR**: Mediator pattern per disaccoppiamento
- **Repository**: Abstraction layer per data access
- **State Machine**: Per GameSession states
- **Hybrid Cache**: L1 (memory) + L2 (Redis)
- **SSE**: Server-Sent Events per streaming chat

---

## Diagramma Architettura

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           FRONTEND (Next.js 16)                         │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐           │
│  │  Auth   │ │  Games  │ │  Chat   │ │ Upload  │ │  Admin  │           │
│  │  Pages  │ │  Pages  │ │  Pages  │ │  Pages  │ │  Pages  │           │
│  └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘           │
│       │           │           │           │           │                 │
│  ┌────┴───────────┴───────────┴───────────┴───────────┴────┐           │
│  │                    Hooks (TanStack Query)                │           │
│  │  useAuth, useGames, useChats, useChunkedUpload, etc.    │           │
│  └────────────────────────────┬────────────────────────────┘           │
│                               │                                         │
│  ┌────────────────────────────┴────────────────────────────┐           │
│  │                      lib/api.ts                          │           │
│  │              (API Client + Auth Headers)                 │           │
│  └────────────────────────────┬────────────────────────────┘           │
└───────────────────────────────┼─────────────────────────────────────────┘
                                │ HTTP/SSE
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        BACKEND (ASP.NET 9)                              │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                      Routing (Endpoints)                         │   │
│  │  Auth, Game, KnowledgeBase, Pdf, Workflow, Config, Admin        │   │
│  └────────────────────────────┬────────────────────────────────────┘   │
│                               │ IMediator.Send()                        │
│  ┌────────────────────────────┴────────────────────────────────────┐   │
│  │                    Application Layer                             │   │
│  │              Commands / Queries / Handlers                       │   │
│  └────────────────────────────┬────────────────────────────────────┘   │
│                               │                                         │
│  ┌────────────────────────────┴────────────────────────────────────┐   │
│  │                      Domain Layer                                │   │
│  │           Entities, Value Objects, Domain Events                 │   │
│  └────────────────────────────┬────────────────────────────────────┘   │
│                               │                                         │
│  ┌────────────────────────────┴────────────────────────────────────┐   │
│  │                   Infrastructure Layer                           │   │
│  │              Repositories, External Services                     │   │
│  └────────────────────────────┬────────────────────────────────────┘   │
└───────────────────────────────┼─────────────────────────────────────────┘
                                │
        ┌───────────────────────┼───────────────────────┐
        │                       │                       │
        ▼                       ▼                       ▼
┌───────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  PostgreSQL   │     │     Qdrant      │     │     Redis       │
│    :5432      │     │     :6333       │     │     :6379       │
│  (Primary DB) │     │  (Vector DB)    │     │    (Cache)      │
└───────────────┘     └─────────────────┘     └─────────────────┘
```

---

*Documento generato automaticamente - MeepleAI Monorepo*
