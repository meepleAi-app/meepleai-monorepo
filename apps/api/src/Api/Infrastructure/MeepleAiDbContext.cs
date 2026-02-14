using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Administration;
using Api.Infrastructure.Entities.Authentication;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.SystemConfiguration;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pgvector; // Issue #3547: Value converter for float[] → Vector mapping

namespace Api.Infrastructure;

public class MeepleAiDbContext : DbContext
{
    private readonly IMediator _mediator;
    private readonly IDomainEventCollector _eventCollector;
    private readonly bool _isInMemoryDatabase;

    public MeepleAiDbContext(
        DbContextOptions<MeepleAiDbContext> options,
        IMediator mediator,
        IDomainEventCollector eventCollector)
        : base(options)
    {
        _mediator = mediator;
        _eventCollector = eventCollector;

        // Issue #3578: Detect InMemory provider for unit tests
        // Check extensions to see if InMemory provider is being used
        _isInMemoryDatabase = options.Extensions
            .Any(e => e.GetType().Name.Contains("InMemory", StringComparison.OrdinalIgnoreCase));
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserSessionEntity> UserSessions => Set<UserSessionEntity>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();
    public DbSet<ApiKeyUsageLogEntity> ApiKeyUsageLogs => Set<ApiKeyUsageLogEntity>(); // ISSUE-904: API Key usage tracking
    public DbSet<OAuthAccountEntity> OAuthAccounts => Set<OAuthAccountEntity>(); // AUTH-06
    public DbSet<GameEntity> Games => Set<GameEntity>();
    public DbSet<GameSessionEntity> GameSessions => Set<GameSessionEntity>(); // DDD-PHASE2: GameSession aggregate
    public DbSet<PlayRecordEntity> PlayRecords => Set<PlayRecordEntity>(); // ISSUE-3888: Play history tracking
    public DbSet<RecordPlayerEntity> RecordPlayers => Set<RecordPlayerEntity>(); // ISSUE-3888: Play record players
    public DbSet<RuleConflictFAQEntity> RuleConflictFAQs => Set<RuleConflictFAQEntity>(); // ISSUE-3761: Conflict resolution FAQ
    public DbSet<RecordScoreEntity> RecordScores => Set<RecordScoreEntity>(); // ISSUE-3888: Play record scores
    public DbSet<RuleSpecEntity> RuleSpecs => Set<RuleSpecEntity>();
    public DbSet<RuleAtomEntity> RuleAtoms => Set<RuleAtomEntity>();
    public DbSet<AgentEntity> Agents => Set<AgentEntity>();
    public DbSet<ChatEntity> Chats => Set<ChatEntity>();
    public DbSet<ChatThreadEntity> ChatThreads => Set<ChatThreadEntity>(); // DDD-PHASE3: KnowledgeBase ChatThread aggregate
    public DbSet<ChatLogEntity> ChatLogs => Set<ChatLogEntity>();
    public DbSet<PdfDocumentEntity> PdfDocuments => Set<PdfDocumentEntity>();
    public DbSet<ProcessingMetricEntity> ProcessingMetrics => Set<ProcessingMetricEntity>(); // ISSUE-4212: Historical metrics storage
    public DbSet<VectorDocumentEntity> VectorDocuments => Set<VectorDocumentEntity>();
    public DbSet<TextChunkEntity> TextChunks => Set<TextChunkEntity>(); // AI-14: Hybrid search
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<AiRequestLogEntity> AiRequestLogs => Set<AiRequestLogEntity>();
    public DbSet<AgentFeedbackEntity> AgentFeedbacks => Set<AgentFeedbackEntity>();
    public DbSet<AgentConfigurationEntity> AgentConfigurations => Set<AgentConfigurationEntity>(); // ISSUE-2391 Sprint 2
    public DbSet<AgentTypologyEntity> AgentTypologies => Set<AgentTypologyEntity>(); // ISSUE-3175: Agent typology domain model
    public DbSet<TypologyPromptTemplateEntity> TypologyPromptTemplates => Set<TypologyPromptTemplateEntity>(); // ISSUE-3175: Typology prompt templates
    public DbSet<AgentSessionEntity> AgentSessions => Set<AgentSessionEntity>(); // ISSUE-3183: Agent session state persistence
    public DbSet<N8NConfigEntity> N8NConfigs => Set<N8NConfigEntity>();
    public DbSet<RuleSpecCommentEntity> RuleSpecComments => Set<RuleSpecCommentEntity>();
    public DbSet<PromptTemplateEntity> PromptTemplates => Set<PromptTemplateEntity>();
    public DbSet<PromptVersionEntity> PromptVersions => Set<PromptVersionEntity>();
    public DbSet<PromptAuditLogEntity> PromptAuditLogs => Set<PromptAuditLogEntity>();
    public DbSet<PasswordResetTokenEntity> PasswordResetTokens => Set<PasswordResetTokenEntity>();
    public DbSet<EmailVerificationEntity> EmailVerifications => Set<EmailVerificationEntity>(); // ISSUE-3071: Email verification
    public DbSet<CacheStatEntity> CacheStats => Set<CacheStatEntity>();
    public DbSet<SystemConfigurationEntity> SystemConfigurations => Set<SystemConfigurationEntity>();
    public DbSet<WorkflowErrorLogEntity> WorkflowErrorLogs => Set<WorkflowErrorLogEntity>(); // N8N-05
    // ADMIN-01 Phase 4: Prompt Evaluation Results
    public DbSet<PromptEvaluationResultEntity> PromptEvaluationResults => Set<PromptEvaluationResultEntity>();
    // BGAI-039: Validation Accuracy Baseline Tracking
    public DbSet<ValidationAccuracyBaselineEntity> ValidationAccuracyBaselines => Set<ValidationAccuracyBaselineEntity>();
    public DbSet<AlertEntity> Alerts => Set<AlertEntity>(); // OPS-07
    public DbSet<AlertRuleEntity> AlertRules => Set<AlertRuleEntity>(); // ISSUE-921: Dynamic alert rules
    public DbSet<AlertConfigurationEntity> AlertConfigurations => Set<AlertConfigurationEntity>(); // ISSUE-921: Dynamic alert config
    public DbSet<UserBackupCodeEntity> UserBackupCodes => Set<UserBackupCodeEntity>(); // AUTH-07
    public DbSet<TempSessionEntity> TempSessions => Set<TempSessionEntity>(); // AUTH-07
    public DbSet<UsedTotpCodeEntity> UsedTotpCodes => Set<UsedTotpCodeEntity>(); // SEC-07: Issue #1787 TOTP Replay Prevention
    public DbSet<LlmCostLogEntity> LlmCostLogs => Set<LlmCostLogEntity>(); // ISSUE-960: BGAI-018
    public DbSet<ChunkedUploadSessionEntity> ChunkedUploadSessions => Set<ChunkedUploadSessionEntity>(); // Chunked PDF upload
    public DbSet<AdminReportEntity> AdminReports => Set<AdminReportEntity>(); // ISSUE-916: Report generation + scheduling
    public DbSet<ReportExecutionEntity> ReportExecutions => Set<ReportExecutionEntity>(); // ISSUE-916: Report execution history
    public DbSet<DocumentCollectionEntity> DocumentCollections => Set<DocumentCollectionEntity>(); // ISSUE-2051: Multi-document collections
    public DbSet<ChatThreadCollectionEntity> ChatThreadCollections => Set<ChatThreadCollectionEntity>(); // ISSUE-2051: Chat-collection junction
    public DbSet<ShareLinkEntity> ShareLinks => Set<ShareLinkEntity>(); // ISSUE-2052: Shareable chat links
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>(); // ISSUE-2053: User notifications
    public DbSet<SharedGameEntity> SharedGames => Set<SharedGameEntity>(); // ISSUE-2370: Shared game catalog
    public DbSet<GameDesignerEntity> GameDesigners => Set<GameDesignerEntity>(); // ISSUE-2370: Game designers
    public DbSet<GamePublisherEntity> GamePublishers => Set<GamePublisherEntity>(); // ISSUE-2370: Game publishers
    public DbSet<GameCategoryEntity> GameCategories => Set<GameCategoryEntity>(); // ISSUE-2370: Game categories taxonomy
    public DbSet<GameMechanicEntity> GameMechanics => Set<GameMechanicEntity>(); // ISSUE-2370: Game mechanics taxonomy
    public DbSet<GameFaqEntity> GameFaqs => Set<GameFaqEntity>(); // ISSUE-2370: Game FAQs
    public DbSet<GameErrataEntity> GameErrata => Set<GameErrataEntity>(); // ISSUE-2370: Game errata
    public DbSet<SharedGameDeleteRequestEntity> SharedGameDeleteRequests => Set<SharedGameDeleteRequestEntity>(); // ISSUE-2370: Delete requests
    public DbSet<SharedGameDocumentEntity> SharedGameDocuments => Set<SharedGameDocumentEntity>(); // ISSUE-2391: Sprint 1 - PDF association
    public DbSet<GameStateTemplateEntity> GameStateTemplates => Set<GameStateTemplateEntity>(); // ISSUE-2400: Sprint 3 - Game state templates
    public DbSet<RulebookAnalysisEntity> RulebookAnalyses => Set<RulebookAnalysisEntity>(); // ISSUE-2402: Sprint 3 - Rulebook analysis service
    public DbSet<QuickQuestionEntity> QuickQuestions => Set<QuickQuestionEntity>(); // ISSUE-2401: Sprint 3 - Quick questions AI generation
    public DbSet<UserLibraryEntryEntity> UserLibraryEntries => Set<UserLibraryEntryEntity>(); // User Library feature
    public DbSet<WishlistItemEntity> WishlistItems => Set<WishlistItemEntity>(); // ISSUE-3917: Wishlist management
    public DbSet<UserCollectionEntryEntity> UserCollectionEntries => Set<UserCollectionEntryEntity>(); // ISSUE-4263: Generic user collections
    public DbSet<LibraryShareLinkEntity> LibraryShareLinks => Set<LibraryShareLinkEntity>(); // ISSUE-2614: Library sharing
    public DbSet<GameLabelEntity> GameLabels => Set<GameLabelEntity>(); // ISSUE-3512: Game labels for library
    public DbSet<UserGameLabelEntity> UserGameLabels => Set<UserGameLabelEntity>(); // ISSUE-3512: User game label assignments
    public DbSet<GameSessionStateEntity> GameSessionStates => Set<GameSessionStateEntity>(); // ISSUE-2403: Sprint 4 - Game session state tracking
    public DbSet<GameStateSnapshotEntity> GameStateSnapshots => Set<GameStateSnapshotEntity>(); // ISSUE-2403: Sprint 4 - State snapshots
    public DbSet<AiModelConfigurationEntity> AiModelConfigurations => Set<AiModelConfigurationEntity>(); // ISSUE-2512: Auto-configuration pipeline - AI model seed
    public DbSet<BadgeEntity> Badges => Set<BadgeEntity>(); // ISSUE-2731: Badge gamification system
    public DbSet<UserBadgeEntity> UserBadges => Set<UserBadgeEntity>(); // ISSUE-2731: User badge awards
    public DbSet<ShareRequestLimitConfigEntity> ShareRequestLimitConfigs => Set<ShareRequestLimitConfigEntity>(); // ISSUE-2730: Rate limit config
    public DbSet<UserRateLimitOverrideEntity> UserRateLimitOverrides => Set<UserRateLimitOverrideEntity>(); // ISSUE-2730: User overrides
    public DbSet<TierStrategyAccessEntity> TierStrategyAccess => Set<TierStrategyAccessEntity>(); // ISSUE-3438: Tier-strategy access control
    public DbSet<StrategyModelMappingEntity> StrategyModelMappings => Set<StrategyModelMappingEntity>(); // ISSUE-3438: Strategy-model mapping
    public DbSet<ChatSessionEntity> ChatSessions => Set<ChatSessionEntity>(); // ISSUE-3483: Chat session persistence
    public DbSet<AgentTestResultEntity> AgentTestResults => Set<AgentTestResultEntity>(); // ISSUE-3379: Agent test results persistence
    public DbSet<ConversationMemoryEntity> ConversationMemories => Set<ConversationMemoryEntity>(); // ISSUE-3493: Temporal RAG
    public DbSet<AgentGameStateSnapshotEntity> AgentGameStateSnapshots => Set<AgentGameStateSnapshotEntity>(); // ISSUE-3493: Position similarity
    public DbSet<StrategyPatternEntity> StrategyPatterns => Set<StrategyPatternEntity>(); // ISSUE-3493: Cached evaluations
    public DbSet<BggImportQueueEntity> BggImportQueue => Set<BggImportQueueEntity>(); // ISSUE-3541: BGG import queue service
    public DbSet<PrivateGameEntity> PrivateGames => Set<PrivateGameEntity>(); // ISSUE-3662: Private games for user library
    public DbSet<ProposalMigrationEntity> ProposalMigrations => Set<ProposalMigrationEntity>(); // ISSUE-3666: Migration choice flow
    public DbSet<BoundedContexts.Administration.Domain.Entities.TokenTier> TokenTiers => Set<BoundedContexts.Administration.Domain.Entities.TokenTier>(); // ISSUE-3692: Token Management
    public DbSet<BoundedContexts.Administration.Domain.Entities.UserTokenUsage> UserTokenUsages => Set<BoundedContexts.Administration.Domain.Entities.UserTokenUsage>(); // ISSUE-3692: Token Management
    public DbSet<BoundedContexts.Administration.Domain.Entities.BatchJob> BatchJobs => Set<BoundedContexts.Administration.Domain.Entities.BatchJob>(); // ISSUE-3693: Batch Job System
    public DbSet<BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition> AgentDefinitions => Set<BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition>(); // ISSUE-3808: Agent Definition for AI Lab
    public DbSet<GameAnalyticsEventEntity> GameAnalyticsEvents => Set<GameAnalyticsEventEntity>(); // ISSUE-3918: Catalog Trending Analytics
    public DbSet<Entities.Gamification.AchievementEntity> Achievements => Set<Entities.Gamification.AchievementEntity>(); // ISSUE-3922: Achievement System
    public DbSet<Entities.Gamification.UserAchievementEntity> UserAchievements => Set<Entities.Gamification.UserAchievementEntity>(); // ISSUE-3922: Achievement System
    public DbSet<BoundedContexts.BusinessSimulations.Domain.Entities.LedgerEntry> LedgerEntries => Set<BoundedContexts.BusinessSimulations.Domain.Entities.LedgerEntry>(); // ISSUE-3720: Financial Ledger
    public DbSet<BoundedContexts.BusinessSimulations.Domain.Entities.CostScenario> CostScenarios => Set<BoundedContexts.BusinessSimulations.Domain.Entities.CostScenario>(); // ISSUE-3725: Agent Cost Calculator
    public DbSet<BoundedContexts.BusinessSimulations.Domain.Entities.ResourceForecast> ResourceForecasts => Set<BoundedContexts.BusinessSimulations.Domain.Entities.ResourceForecast>(); // ISSUE-3726: Resource Forecasting Simulator

    // GST-001: SessionTracking bounded context (persistence entities)
    public DbSet<Api.Infrastructure.Entities.SessionTracking.SessionEntity> SessionTrackingSessions => Set<Api.Infrastructure.Entities.SessionTracking.SessionEntity>();
    public DbSet<Api.Infrastructure.Entities.SessionTracking.ParticipantEntity> SessionTrackingParticipants => Set<Api.Infrastructure.Entities.SessionTracking.ParticipantEntity>();
    public DbSet<Api.Infrastructure.Entities.SessionTracking.ScoreEntryEntity> SessionTrackingScoreEntries => Set<Api.Infrastructure.Entities.SessionTracking.ScoreEntryEntity>();
    public DbSet<Api.Infrastructure.Entities.SessionTracking.PlayerNoteEntity> SessionTrackingPlayerNotes => Set<Api.Infrastructure.Entities.SessionTracking.PlayerNoteEntity>();
    public DbSet<Api.Infrastructure.Entities.SessionTracking.DiceRollEntity> SessionTrackingDiceRolls => Set<Api.Infrastructure.Entities.SessionTracking.DiceRollEntity>();
    public DbSet<Api.Infrastructure.Entities.SessionTracking.CardDrawEntity> SessionTrackingCardDraws => Set<Api.Infrastructure.Entities.SessionTracking.CardDrawEntity>();
    public DbSet<Api.Infrastructure.Entities.SessionTracking.SessionDeckEntity> SessionDecks => Set<Api.Infrastructure.Entities.SessionTracking.SessionDeckEntity>();
    public DbSet<Api.Infrastructure.Entities.SessionTracking.CardEntity> Cards => Set<Api.Infrastructure.Entities.SessionTracking.CardEntity>();
    public DbSet<Api.Infrastructure.Entities.SessionTracking.SessionNoteEntity> SessionNotes => Set<Api.Infrastructure.Entities.SessionTracking.SessionNoteEntity>();

    // Issue #4220: Notification preferences
    public DbSet<Api.Infrastructure.Entities.UserNotifications.NotificationPreferencesEntity> NotificationPreferences => Set<Api.Infrastructure.Entities.UserNotifications.NotificationPreferencesEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        base.OnConfiguring(optionsBuilder);

        // Issue #1694: Suppress PendingModelChangesWarning in Alpha phase
        // Rationale: We're in alpha with no production data. TokenUsage is a value object
        // used only in-memory (method parameters, domain events), not persisted to DB.
        // EF Core incorrectly detects it as a model change.
        // ISSUE-1694: Remove this suppression when moving to Beta/Production with real data.
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        // Issue #3578: Ignore Pgvector.Vector type for InMemory database (unit tests)
        // InMemory provider can't handle pgvector types - they're only used with PostgreSQL
        // IMPORTANT: This must be called BEFORE ApplyConfigurationsFromAssembly
        if (_isInMemoryDatabase)
        {
            modelBuilder.Ignore<Vector>();
        }

        // Issue #3547: Enable pgvector extension for embedding columns
        // Issue #3578: Skip pgvector extension for InMemory database (unit tests)
        // InMemory provider doesn't support PostgreSQL extensions and Pgvector.Vector type
        if (!_isInMemoryDatabase)
        {
            modelBuilder.HasPostgresExtension("vector");
        }

        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeepleAiDbContext).Assembly);

        // Issue #3578: Explicitly ignore Vector properties for InMemory database (unit tests)
        // Must be done AFTER ApplyConfigurationsFromAssembly to override the property configs
        if (_isInMemoryDatabase)
        {
            modelBuilder.Entity<AgentGameStateSnapshotEntity>().Ignore(e => e.Embedding);
            modelBuilder.Entity<ConversationMemoryEntity>().Ignore(e => e.Embedding);
            modelBuilder.Entity<StrategyPatternEntity>().Ignore(e => e.Embedding);
        }

        // Ignore domain aggregate roots - EF Core should only map persistence entities
        modelBuilder.Ignore<BoundedContexts.Authentication.Domain.Entities.OAuthAccount>();
        modelBuilder.Ignore<BoundedContexts.Authentication.Domain.Entities.User>();
        modelBuilder.Ignore<BoundedContexts.Authentication.Domain.Entities.Session>();
        modelBuilder.Ignore<BoundedContexts.Authentication.Domain.Entities.ApiKey>();
        modelBuilder.Ignore<BoundedContexts.Authentication.Domain.Entities.ShareLink>(); // ISSUE-2052
        modelBuilder.Ignore<BoundedContexts.GameManagement.Domain.Entities.GameSession>();
        modelBuilder.Ignore<BoundedContexts.GameManagement.Domain.Entities.GameSessionState>(); // ISSUE-2403
        modelBuilder.Ignore<BoundedContexts.GameManagement.Domain.Entities.GameStateSnapshot>(); // ISSUE-2403
        modelBuilder.Ignore<BoundedContexts.GameManagement.Domain.Entities.RuleConflictFAQ>(); // ISSUE-3761
        modelBuilder.Ignore<BoundedContexts.UserNotifications.Domain.Aggregates.Notification>(); // ISSUE-2053
        modelBuilder.Ignore<BoundedContexts.GameManagement.Domain.Entities.Game>();
        modelBuilder.Ignore<BoundedContexts.GameManagement.Domain.Entities.PlayRecord>(); // ISSUE-3888
        modelBuilder.Ignore<BoundedContexts.GameManagement.Domain.Entities.RecordPlayer>(); // ISSUE-3888
        modelBuilder.Ignore<BoundedContexts.Administration.Domain.Entities.AdminReport>(); // ISSUE-916
        modelBuilder.Ignore<BoundedContexts.Administration.Domain.Entities.ReportExecution>(); // ISSUE-916
        modelBuilder.Ignore<BoundedContexts.DocumentProcessing.Domain.Entities.DocumentCollection>(); // ISSUE-2051
        modelBuilder.Ignore<BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame>(); // ISSUE-2370
        modelBuilder.Ignore<BoundedContexts.SharedGameCatalog.Domain.Entities.GameCategory>(); // ISSUE-2370
        modelBuilder.Ignore<BoundedContexts.SharedGameCatalog.Domain.Entities.GameMechanic>(); // ISSUE-2370
        modelBuilder.Ignore<BoundedContexts.SharedGameCatalog.Domain.Entities.ShareRequest>(); // ISSUE-2726
        modelBuilder.Ignore<BoundedContexts.SharedGameCatalog.Domain.Entities.Contributor>(); // ISSUE-2726
        modelBuilder.Ignore<BoundedContexts.SharedGameCatalog.Domain.Entities.Badge>(); // ISSUE-2731
        modelBuilder.Ignore<BoundedContexts.SharedGameCatalog.Domain.Entities.UserBadge>(); // ISSUE-2731
        modelBuilder.Ignore<BoundedContexts.SystemConfiguration.Domain.Entities.AiModelConfiguration>(); // ISSUE-2512
        modelBuilder.Ignore<BoundedContexts.SystemConfiguration.Domain.Entities.ShareRequestLimitConfig>(); // ISSUE-2730
        modelBuilder.Ignore<BoundedContexts.SystemConfiguration.Domain.Entities.UserRateLimitOverride>(); // ISSUE-2730
        modelBuilder.Ignore<BoundedContexts.UserLibrary.Domain.Entities.UserLibraryEntry>(); // User Library feature
        modelBuilder.Ignore<BoundedContexts.SharedGameCatalog.Domain.Entities.RulebookAnalysis>(); // ISSUE-2402
        modelBuilder.Ignore<BoundedContexts.KnowledgeBase.Domain.Entities.AgentTypology>(); // ISSUE-3175
        modelBuilder.Ignore<BoundedContexts.KnowledgeBase.Domain.Entities.TypologyPromptTemplate>(); // ISSUE-3175
        modelBuilder.Ignore<BoundedContexts.KnowledgeBase.Domain.Entities.AgentSession>(); // ISSUE-3183
        modelBuilder.Ignore<BoundedContexts.KnowledgeBase.Domain.Entities.ChatSession>(); // ISSUE-3483
        modelBuilder.Ignore<BoundedContexts.UserLibrary.Domain.Entities.ProposalMigration>(); // ISSUE-3666
        modelBuilder.Ignore<BoundedContexts.UserLibrary.Domain.Entities.PrivateGame>(); // ISSUE-3662

        // GST-001: SessionTracking domain entities
        modelBuilder.Ignore<BoundedContexts.SessionTracking.Domain.Entities.Session>(); // ISSUE-3160
        modelBuilder.Ignore<BoundedContexts.SessionTracking.Domain.Entities.Participant>(); // ISSUE-3160
        modelBuilder.Ignore<BoundedContexts.SessionTracking.Domain.Entities.ScoreEntry>(); // ISSUE-3160
        modelBuilder.Ignore<BoundedContexts.SessionTracking.Domain.Entities.PlayerNote>(); // ISSUE-3160
        modelBuilder.Ignore<BoundedContexts.SessionTracking.Domain.Entities.DiceRoll>(); // ISSUE-3160
        modelBuilder.Ignore<BoundedContexts.SessionTracking.Domain.Entities.CardDraw>(); // ISSUE-3160
    }

    /// <summary>
    /// Saves all changes made in this context to the database and dispatches domain events.
    /// Domain events are collected by repositories via IDomainEventCollector before SaveChangesAsync.
    /// After successful save, collected events are dispatched via MediatR.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Save changes first
        var result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Get collected domain events from repositories
        var events = _eventCollector.GetAndClearEvents();

        // Dispatch domain events after successful save
        if (events != null)
        {
            foreach (var domainEvent in events)
            {
                await _mediator.Publish(domainEvent, cancellationToken).ConfigureAwait(false);
            }
        }

        return result;
    }
}
