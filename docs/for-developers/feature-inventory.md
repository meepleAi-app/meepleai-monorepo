# Inventario delle feature

> **Documento generato — non modificarlo a mano.**
> Rigeneralo con `make feature-inventory` (o `bash infra/scripts/generate-feature-inventory.sh`).
> Ogni modifica manuale viene persa alla prossima esecuzione, ed è voluto: un inventario
> scritto a mano diverge dal codice in pochi giorni. Il predecessore di questo documento
> — una roadmap redatta a mano — dichiarava «All planned features have been implemented»
> e vive in `.docs-archive/`.

Conta e localizza **ciò che esiste**. Non dice a cosa serve (→ gli [ADR](../for-claude/architecture/adr/))
né se funziona (→ i test).

**Generato da**: commit `fe1a30993` · rigenera per aggiornare i conteggi

## Backend — bounded context

Aggregati e entità di dominio, comandi e query per contesto. Un contesto con pochi
aggregati e zero comandi è tipicamente un'area appena abbozzata, non una feature completa.

| Contesto | Dominio | Comandi | Query | Aggregati principali |
|---|---:|---:|---:|---|
| Administration | 93 | 83 | 118 | AdminReport, Alert, AuditLog, BatchJob, DatabaseMetricsSnapshot, ReportExecution |
| AgentMemory | 16 | 8 | 6 | GameMemory, GroupMemory, PlayerMemory |
| Authentication | 54 | 49 | 26 | AccessRequest, InvitationGameSuggestion, InvitationToken, OAuthAccount, Session, ShareLink |
| BusinessSimulations | 17 | 8 | 12 | CostScenario, LedgerEntry, ResourceForecast, UserBudget |
| DatabaseSync | 13 | 5 | 5 | — |
| DocumentProcessing | 65 | 42 | 38 | ChunkedUploadSession, DocumentCollection, PdfDocument, PhotoBatchPage, PhotoBatchUpload, ProcessingJob |
| EntityRelationships | 10 | 3 | 2 | EntityLink |
| GameManagement | 172 | 118 | 75 | GameBook, GamePhaseTemplate, GameReview, GameSession, GameSessionState, GameStateSnapshot |
| GameToolbox | 8 | 0 | 0 | Phase, Toolbox, ToolboxTemplate, ToolboxTool |
| GameToolkit | 20 | 5 | 6 | AiToolkitSuggestionCacheEntry, GameToolkit, Toolkit, ToolkitVersion, ToolkitWidget |
| Gamification | 6 | 0 | 2 | Achievement, UserAchievement |
| KbQuality | 10 | 1 | 2 | — |
| KnowledgeBase | 318 | 83 | 102 | AbTestSession, AbTestVariant, AdminRagStrategy, AgentConfiguration, AgentDefinition, AgentGameStateSnapshot |
| SecurityAudit | 0 | 0 | 0 | — |
| SessionTracking | 84 | 46 | 25 | Card, CardDraw, DiceRoll, GamebookCampaignSession, GamebookGlossaryEntry, GamebookPhotoArtifact |
| SharedGameCatalog | 153 | 113 | 82 | Badge, CatalogSyncRun, CertificationThresholdsConfig, Contributor, CoverAssignmentSource, DeleteRequestStatus |
| SystemConfiguration | 31 | 32 | 26 | AiModelConfiguration, IncidentBannerState, LlmSystemConfig, ShareRequestLimitConfig, SystemConfiguration, TierDefinition |
| Testing | 0 | 6 | 0 | — |
| UserLibrary | 50 | 33 | 26 | GameChecklist, GameLabel, GameSession, GameSuggestion, LibraryShareLink, PrivateGame |
| UserNotifications | 19 | 26 | 18 | EmailQueueItem, EmailTemplate, Notification, NotificationPreferences, NotificationQueueItem, SlackConnection |
| **20 contesti** | **1139** | **661** | **571** | |

## Backend — endpoint HTTP

**1381** endpoint registrati, per file di routing:

| File | Endpoint |
|---|---:|
| `LiveSessionEndpoints.cs` | 45 |
| `GameNightEndpoints.cs` | 35 |
| `KnowledgeBaseEndpoints.cs` | 31 |
| `GameToolkitRoutes.cs` | 30 |
| `GameEndpoints.cs` | 27 |
| `RuleSpecEndpoints.cs` | 19 |
| `GameToolboxRoutes.cs` | 19 |
| `AdminQueueEndpoints.cs` | 19 |
| `AnalyticsEndpoints.cs` | 18 |
| `AdminMechanicAnalysesEndpoints.cs` | 18 |
| `PromptManagementEndpoints.cs` | 16 |
| `PlayRecordEndpoints.cs` | 16 |
| `AgentsEndpoints.cs` | 16 |
| `ConfigurationEndpoints.cs` | 15 |
| `AuthenticationEndpoints.cs` | 15 |
| `AdminMechanicExtractorValidationEndpoints.cs` | 15 |
| `UserProfileEndpoints.cs` | 14 |
| `AgentMemoryEndpoints.cs` | 14 |
| `AdminCatalogIngestionEndpoints.cs` | 14 |
| `AdminKnowledgeBaseEndpoints.cs` | 13 |
| `PlaylistEndpoints.cs` | 11 |
| `SessionFlowEndpoints.cs` | 10 |
| `NotificationPreferencesEndpoints.cs` | 10 |
| `GamebookPhotoEndpoints.cs` | 10 |
| `DatabaseSyncEndpoints.cs` | 10 |
| `DashboardEndpoints.cs` | 10 |
| `AdminPdfManagementEndpoints.cs` | 10 |
| `AdminAgentDefinitionEndpoints.cs` | 10 |
| `TierStrategyAdminEndpoints.cs` | 9 |
| `RagDashboardEndpoints.cs` | 9 |
| `GamebookCampaignEndpoints.cs` | 9 |
| `BggImportQueueEndpoints.cs` | 9 |
| `AlertConfigEndpoints.cs` | 9 |
| `AiModelAdminEndpoints.cs` | 9 |
| `AccessRequestEndpoints.cs` | 9 |
| `RagPipelineAdminEndpoints.cs` | 8 |
| `PrivateGameEndpoints.cs` | 8 |
| `ChatSessionEndpoints.cs` | 8 |
| `AiEndpoints.cs` | 8 |
| `AdminResourcesEndpoints.cs` | 8 |
| `TwoFactorEndpoints.cs` | 7 |
| `FinancialLedgerEndpoints.cs` | 7 |
| `FeatureFlagEndpoints.cs` | 7 |
| `AdminInfrastructureEndpoints.cs` | 7 |
| `AdminEmailTemplateEndpoints.cs` | 7 |
| `WhiteboardEndpoints.cs` | 6 |
| `TokenManagementEndpoints.cs` | 6 |
| `SlackIntegrationEndpoints.cs` | 6 |
| `SessionInviteEndpoints.cs` | 6 |
| `RateLimitAdminEndpoints.cs` | 6 |
| `GameNightImprovvisataEndpoints.cs` | 6 |
| `DocumentCollectionEndpoints.cs` | 6 |
| `BatchJobEndpoints.cs` | 6 |
| `AgentSessionEndpoints.cs` | 6 |
| `AdminOperationsEndpoints.cs` | 6 |
| `AdminGameKbEndpoints.cs` | 6 |
| `AdminEmailEndpoints.cs` | 6 |
| `AdminConfigEndpoints.cs` | 6 |
| `AdminAbTestEndpoints.cs` | 6 |
| `WishlistEndpoints.cs` | 5 |
| `TurnOrderEndpoints.cs` | 5 |
| `ToolStateEndpoints.cs` | 5 |
| `SharedGameTranslationEndpoints.cs` | 5 |
| `SessionAttachmentEndpoints.cs` | 5 |
| `RulebookAnalysisEndpoints.cs` | 5 |
| `ReportingEndpoints.cs` | 5 |
| `PlaygroundTestScenarioEndpoints.cs` | 5 |
| `NotificationEndpoints.cs` | 5 |
| `AdminStrategyEndpoints.cs` | 5 |
| `AdminSharedGameContentEndpoints.cs` | 5 |
| `AdminSandboxEndpoints.cs` | 5 |
| `AdminRagBackupEndpoints.cs` | 5 |
| `AdminOpenRouterEndpoints.cs` | 5 |
| `AdminNotificationQueueEndpoints.cs` | 5 |
| `AdminAnalyticsEndpoints.cs` | 5 |
| `ShareLinkEndpoints.cs` | 4 |
| `SessionSnapshotEndpoints.cs` | 4 |
| `ResourceForecastEndpoints.cs` | 4 |
| `RagPipelineEndpoints.cs` | 4 |
| `RagEnhancementAdminEndpoints.cs` | 4 |
| `PhotoIngestionEndpoints.cs` | 4 |
| `OAuthEndpoints.cs` | 4 |
| `MonitoringEndpoints.cs` | 4 |
| `GameBookEndpoints.cs` | 4 |
| `EntityLinkUserEndpoints.cs` | 4 |
| `EntityLinkAdminEndpoints.cs` | 4 |
| `CostCalculatorEndpoints.cs` | 4 |
| `AdminTierEndpoints.cs` | 4 |
| `AdminTestResultEndpoints.cs` | 4 |
| `AdminProviderEndpoints.cs` | 4 |
| `AdminGameImportWizardEndpoints.cs` | 4 |
| `AdminCategoriesEndpoints.cs` | 4 |
| `UserHandEndpoints.cs` | 3 |
| `TestingMetricsEndpoints.cs` | 3 |
| `StatusBannerEndpoints.cs` | 3 |
| `SessionLimitsConfigEndpoints.cs` | 3 |
| `SessionEndpoints.cs` | 3 |
| `PasswordEndpoints.cs` | 3 |
| `OnboardingEndpoints.cs` | 3 |
| `LlmAnalyticsEndpoints.cs` | 3 |
| `LedgerModeEndpoints.cs` | 3 |
| `KbManagementEndpoints.cs` | 3 |
| `GamePhaseTemplateEndpoints.cs` | 3 |
| `CacheEndpoints.cs` | 3 |
| `AlertEndpoints.cs` | 3 |
| `AlertConfigurationEndpoints.cs` | 3 |
| `AlertChannelsEndpoints.cs` | 3 |
| `AdminStorageMigrationEndpoints.cs` | 3 |
| `AdminSlackEndpoints.cs` | 3 |
| `AdminSecretsEndpoints.cs` | 3 |
| `AdminRagExecutionEndpoints.cs` | 3 |
| `AdminGameWizardEndpoints.cs` | 3 |
| `AdminEventsEndpoints.cs` | 3 |
| `AdminEmergencyControlsEndpoints.cs` | 3 |
| `AdminBusinessStatsEndpoints.cs` | 3 |
| `AdminAuditLogEndpoints.cs` | 3 |
| `UserUsageEndpoints.cs` | 2 |
| `UserLlmDataEndpoints.cs` | 2 |
| `UserGameKbEndpoints.cs` | 2 |
| `UserAiConsentEndpoints.cs` | 2 |
| `UserAccountEndpoints.cs` | 2 |
| `TestEndpoints.cs` | 2 |
| `TermsConsentEndpoints.cs` | 2 |
| `StatusPageEndpoints.cs` | 2 |
| `SessionTrackingEndpoints.cs` | 2 |
| `RulebookEndpoints.cs` | 2 |
| `RagExecutionAdminEndpoints.cs` | 2 |
| `PermissionRoutes.cs` | 2 |
| `PdfUploadLimitsConfigEndpoints.cs` | 2 |
| `PdfTierUploadLimitsConfigEndpoints.cs` | 2 |
| `GameLibraryConfigEndpoints.cs` | 2 |
| `EmailVerificationEndpoints.cs` | 2 |
| `DeviceEndpoints.cs` | 2 |
| `ChatHistoryConfigEndpoints.cs` | 2 |
| `BudgetEndpoints.cs` | 2 |
| `BggEndpoints.cs` | 2 |
| `ArbitroAgentEndpoints.cs` | 2 |
| `AdminServiceCallEndpoints.cs` | 2 |
| `AdminLlmConfigEndpoints.cs` | 2 |
| `AdminKBSettingsEndpoints.cs` | 2 |
| `AdminEmbeddingEndpoints.cs` | 2 |
| `AdminDockerEndpoints.cs` | 2 |
| `AdminBulkImportEndpoints.cs` | 2 |
| `AdminBudgetEndpoints.cs` | 2 |
| `AdminAgentMetricsEndpoints.cs` | 2 |
| `AdminAgentAnalyticsEndpoints.cs` | 2 |
| `AchievementEndpoints.cs` | 2 |
| `WaitlistEndpoints.cs` | 1 |
| `UserGamebooksEndpoints.cs` | 1 |
| `UserActivityEndpoints.cs` | 1 |
| `UnsubscribeEndpoints.cs` | 1 |
| `RagStrategyEndpoints.cs` | 1 |
| `PdfAnalyticsEndpoints.cs` | 1 |
| `ModelPerformanceEndpoints.cs` | 1 |
| `ModelEndpoints.cs` | 1 |
| `LlmEndpoints.cs` | 1 |
| `ContactEndpoints.cs` | 1 |
| `CollectionWizardEndpoints.cs` | 1 |
| `ChatAnalyticsEndpoints.cs` | 1 |
| `BggAttemptBeaconEndpoints.cs` | 1 |
| `BatchJobLogsEndpoints.cs` | 1 |
| `AuditEndpoints.cs` | 1 |
| `ArbitroAdminEndpoints.cs` | 1 |
| `AgentTypologiesEndpoints.cs` | 1 |
| `AgentPlaygroundEndpoints.cs` | 1 |
| `AdminSeedingEndpoints.cs` | 1 |
| `AdminRagQualityEndpoints.cs` | 1 |
| `AdminPipelineEndpoints.cs` | 1 |
| `AdminPdfStorageEndpoints.cs` | 1 |
| `AdminPdfMetricsEndpoints.cs` | 1 |
| `AdminMiscEndpoints.cs` | 1 |
| `AdminMetricsEndpoints.cs` | 1 |
| `AdminManualNotificationEndpoints.cs` | 1 |
| `AdminLogEndpoints.cs` | 1 |
| `AdminIndexerEndpoints.cs` | 1 |
| `AdminDebugChatEndpoints.cs` | 1 |
| `AdminCircuitBreakerEndpoints.cs` | 1 |
| `AdminAgentTestEndpoints.cs` | 1 |
| `ActivityTimelineEndpoints.cs` | 1 |
| `ActivityFeedEndpoints.cs` | 1 |

## Frontend — pagine

**220** pagine (App Router), per gruppo di route — il gruppo segmenta
**chi** accede, non la feature:

| Gruppo | Pagine |
|---|---:|
| `(root)` | 98 |
| `(authenticated)` | 83 |
| `(public)` | 25 |
| `(auth)` | 10 |
| `(chat)` | 4 |

## Decisioni architetturali

**68** ADR in `docs/for-claude/architecture/adr/`. Sono la fonte del **perché**:
questo inventario dice cosa esiste, gli ADR dicono per quale ragione e a quali condizioni.

I cinque più recenti:

- [`adr-060-live-session-persistence.md`](../for-claude/architecture/adr/adr-060-live-session-persistence.md) — ADR-060: Live session persistence strategy
- [`adr-2026-06-09-wikidata-enrichment-architecture.md`](../for-claude/architecture/adr/adr-2026-06-09-wikidata-enrichment-architecture.md) — ADR 2026-06-09 — Wikidata enrichment architecture
- [`adr-089-session-scoring-ssot.md`](../for-claude/architecture/adr/adr-089-session-scoring-ssot.md) — ADR-089 — SSOT tra i modelli di "sessione live" e i sistemi di scoring
- [`adr-090-in-session-grounded-answer-ownership.md`](../for-claude/architecture/adr/adr-090-in-session-grounded-answer-ownership.md) — ADR-090 — Ownership della risposta grounded in-sessione: KnowledgeBase owner, SessionTracking consumer
- [`adr-088-mechanic-cards-as-rag-retrieval-source.md`](../for-claude/architecture/adr/adr-088-mechanic-cards-as-rag-retrieval-source.md) — ADR-088 — Mechanic Cards as RAG Retrieval Source
