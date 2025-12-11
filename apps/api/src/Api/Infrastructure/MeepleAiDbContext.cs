using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure;

public class MeepleAiDbContext : DbContext
{
    private readonly IMediator _mediator;
    private readonly IDomainEventCollector _eventCollector;

    public MeepleAiDbContext(
        DbContextOptions<MeepleAiDbContext> options,
        IMediator mediator,
        IDomainEventCollector eventCollector)
        : base(options)
    {
        _mediator = mediator;
        _eventCollector = eventCollector;
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserSessionEntity> UserSessions => Set<UserSessionEntity>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();
    public DbSet<ApiKeyUsageLogEntity> ApiKeyUsageLogs => Set<ApiKeyUsageLogEntity>(); // ISSUE-904: API Key usage tracking
    public DbSet<OAuthAccountEntity> OAuthAccounts => Set<OAuthAccountEntity>(); // AUTH-06
    public DbSet<GameEntity> Games => Set<GameEntity>();
    public DbSet<GameSessionEntity> GameSessions => Set<GameSessionEntity>(); // DDD-PHASE2: GameSession aggregate
    public DbSet<RuleSpecEntity> RuleSpecs => Set<RuleSpecEntity>();
    public DbSet<RuleAtomEntity> RuleAtoms => Set<RuleAtomEntity>();
    public DbSet<AgentEntity> Agents => Set<AgentEntity>();
    public DbSet<ChatEntity> Chats => Set<ChatEntity>();
    public DbSet<ChatThreadEntity> ChatThreads => Set<ChatThreadEntity>(); // DDD-PHASE3: KnowledgeBase ChatThread aggregate
    public DbSet<ChatLogEntity> ChatLogs => Set<ChatLogEntity>();
    public DbSet<PdfDocumentEntity> PdfDocuments => Set<PdfDocumentEntity>();
    public DbSet<VectorDocumentEntity> VectorDocuments => Set<VectorDocumentEntity>();
    public DbSet<TextChunkEntity> TextChunks => Set<TextChunkEntity>(); // AI-14: Hybrid search
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<AiRequestLogEntity> AiRequestLogs => Set<AiRequestLogEntity>();
    public DbSet<AgentFeedbackEntity> AgentFeedbacks => Set<AgentFeedbackEntity>();
    public DbSet<N8nConfigEntity> N8nConfigs => Set<N8nConfigEntity>();
    public DbSet<RuleSpecCommentEntity> RuleSpecComments => Set<RuleSpecCommentEntity>();
    public DbSet<PromptTemplateEntity> PromptTemplates => Set<PromptTemplateEntity>();
    public DbSet<PromptVersionEntity> PromptVersions => Set<PromptVersionEntity>();
    public DbSet<PromptAuditLogEntity> PromptAuditLogs => Set<PromptAuditLogEntity>();
    public DbSet<PasswordResetTokenEntity> PasswordResetTokens => Set<PasswordResetTokenEntity>();
    public DbSet<CacheStatEntity> CacheStats => Set<CacheStatEntity>();
    public DbSet<SystemConfigurationEntity> SystemConfigurations => Set<SystemConfigurationEntity>();
    public DbSet<WorkflowErrorLogEntity> WorkflowErrorLogs => Set<WorkflowErrorLogEntity>(); // N8N-05
    // ADMIN-01 Phase 4: Prompt Evaluation Results
    public DbSet<PromptEvaluationResultEntity> PromptEvaluationResults { get; set; }
    // BGAI-039: Validation Accuracy Baseline Tracking
    public DbSet<ValidationAccuracyBaselineEntity> ValidationAccuracyBaselines => Set<ValidationAccuracyBaselineEntity>();
    public DbSet<AlertEntity> Alerts => Set<AlertEntity>(); // OPS-07
    public DbSet<UserBackupCodeEntity> UserBackupCodes => Set<UserBackupCodeEntity>(); // AUTH-07
    public DbSet<TempSessionEntity> TempSessions => Set<TempSessionEntity>(); // AUTH-07
    public DbSet<UsedTotpCodeEntity> UsedTotpCodes => Set<UsedTotpCodeEntity>(); // SEC-07: Issue #1787 TOTP Replay Prevention
    public DbSet<LlmCostLogEntity> LlmCostLogs => Set<LlmCostLogEntity>(); // ISSUE-960: BGAI-018
    public DbSet<ChunkedUploadSessionEntity> ChunkedUploadSessions => Set<ChunkedUploadSessionEntity>(); // Chunked PDF upload

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
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
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeepleAiDbContext).Assembly);

        // Ignore domain aggregate roots - EF Core should only map persistence entities
        modelBuilder.Ignore<BoundedContexts.Authentication.Domain.Entities.OAuthAccount>();
        modelBuilder.Ignore<BoundedContexts.Authentication.Domain.Entities.User>();
        modelBuilder.Ignore<BoundedContexts.Authentication.Domain.Entities.Session>();
        modelBuilder.Ignore<BoundedContexts.Authentication.Domain.Entities.ApiKey>();
        modelBuilder.Ignore<BoundedContexts.GameManagement.Domain.Entities.GameSession>();
        modelBuilder.Ignore<BoundedContexts.GameManagement.Domain.Entities.Game>();
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
        foreach (var domainEvent in events)
        {
            await _mediator.Publish(domainEvent, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
