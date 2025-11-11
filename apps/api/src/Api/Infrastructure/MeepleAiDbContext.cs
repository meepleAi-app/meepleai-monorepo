using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure;

public class MeepleAiDbContext : DbContext
{
    public MeepleAiDbContext(DbContextOptions<MeepleAiDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserSessionEntity> UserSessions => Set<UserSessionEntity>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();
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
    public DbSet<AlertEntity> Alerts => Set<AlertEntity>(); // OPS-07
    public DbSet<UserBackupCodeEntity> UserBackupCodes => Set<UserBackupCodeEntity>(); // AUTH-07
    public DbSet<TempSessionEntity> TempSessions => Set<TempSessionEntity>(); // AUTH-07

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeepleAiDbContext).Assembly);
    }
}
