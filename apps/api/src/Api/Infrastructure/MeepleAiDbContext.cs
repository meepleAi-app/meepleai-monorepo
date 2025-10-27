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
    public DbSet<RuleSpecEntity> RuleSpecs => Set<RuleSpecEntity>();
    public DbSet<RuleAtomEntity> RuleAtoms => Set<RuleAtomEntity>();
    public DbSet<AgentEntity> Agents => Set<AgentEntity>();
    public DbSet<ChatEntity> Chats => Set<ChatEntity>();
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

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.DisplayName).HasMaxLength(128);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Role)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            // AUTH-07: Two-Factor Authentication
            entity.Property(e => e.TotpSecretEncrypted).HasMaxLength(512);
            entity.Property(e => e.IsTwoFactorEnabled).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.TwoFactorEnabledAt);

            // Relationships
            entity.HasMany(e => e.Sessions)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.BackupCodes)
                .WithOne(bc => bc.User)
                .HasForeignKey(bc => bc.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<UserSessionEntity>(entity =>
        {
            entity.ToTable("user_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(128);
            entity.Property(e => e.UserAgent).HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.LastSeenAt);
            entity.Property(e => e.RevokedAt);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        // AUTH-07: User backup codes for 2FA recovery
        modelBuilder.Entity<UserBackupCodeEntity>(entity =>
        {
            entity.ToTable("user_backup_codes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.CodeHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.IsUsed).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.UsedAt);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsUsed }).HasFilter("\"IsUsed\" = FALSE");
            entity.HasIndex(e => e.CodeHash).IsUnique();
        });

        // AUTH-07: Temporary sessions for 2FA verification (short-lived, single-use)
        modelBuilder.Entity<TempSessionEntity>(entity =>
        {
            entity.ToTable("temp_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(128);
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.IsUsed).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.UsedAt);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAt); // For cleanup queries
        });

        modelBuilder.Entity<ApiKeyEntity>(entity =>
        {
            entity.ToTable("api_keys");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.KeyName).IsRequired().HasMaxLength(128);
            entity.Property(e => e.KeyHash).IsRequired().HasMaxLength(256);
            entity.Property(e => e.KeyPrefix).IsRequired().HasMaxLength(16);
            entity.Property(e => e.Scopes).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.LastUsedAt);
            entity.Property(e => e.ExpiresAt);
            entity.Property(e => e.RevokedAt);
            entity.Property(e => e.RevokedBy).HasMaxLength(64);
            entity.Property(e => e.Metadata).HasMaxLength(4096);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.RevokedByUser)
                .WithMany()
                .HasForeignKey(e => e.RevokedBy)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.KeyHash).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.IsActive, e.ExpiresAt });
        });

        // AUTH-06: OAuth Accounts Configuration
        modelBuilder.Entity<OAuthAccountEntity>(entity =>
        {
            entity.ToTable("oauth_accounts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Provider).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ProviderUserId).IsRequired().HasMaxLength(255);
            entity.Property(e => e.AccessTokenEncrypted).IsRequired();
            entity.Property(e => e.RefreshTokenEncrypted);
            entity.Property(e => e.TokenExpiresAt);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Provider);
            entity.HasIndex(e => new { e.Provider, e.ProviderUserId }).IsUnique();
        });

        modelBuilder.Entity<GameEntity>(entity =>
        {
            entity.ToTable("games");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(128);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<RuleSpecEntity>(entity =>
        {
            entity.ToTable("rule_specs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Version).IsRequired().HasMaxLength(32);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.GameId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.CreatedByUserId).HasMaxLength(64);
            // EDIT-06: Version timeline support
            entity.Property(e => e.ParentVersionId).HasMaxLength(64);
            entity.Property(e => e.MergedFromVersionIds).HasMaxLength(1024);
            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            // EDIT-06: Parent version relationship (self-referencing)
            entity.HasOne(e => e.ParentVersion)
                .WithMany()
                .HasForeignKey(e => e.ParentVersionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => new { e.GameId, e.Version }).IsUnique();
            entity.HasIndex(e => e.ParentVersionId);
        });

        modelBuilder.Entity<RuleAtomEntity>(entity =>
        {
            entity.ToTable("rule_atoms");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.Section).HasMaxLength(128);
            entity.Property(e => e.SortOrder).IsRequired();
            entity.HasOne(e => e.RuleSpec)
                .WithMany(r => r.Atoms)
                .HasForeignKey(e => e.RuleSpecId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.RuleSpecId, e.SortOrder });
        });

        modelBuilder.Entity<AgentEntity>(entity =>
        {
            entity.ToTable("agents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Kind).IsRequired().HasMaxLength(32);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.GameId).IsRequired().HasMaxLength(64);
            entity.HasOne(e => e.Game)
                .WithMany(g => g.Agents)
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.GameId, e.Name });
        });

        modelBuilder.Entity<ChatEntity>(entity =>
        {
            entity.ToTable("chats");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.GameId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.AgentId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.StartedAt).IsRequired();
            entity.Property(e => e.LastMessageAt);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Game)
                .WithMany(g => g.Chats)
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Agent)
                .WithMany(a => a.Chats)
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.LastMessageAt });
            entity.HasIndex(e => new { e.GameId, e.StartedAt });
        });

        modelBuilder.Entity<ChatLogEntity>(entity =>
        {
            entity.ToTable("chat_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.ChatId).IsRequired();
            entity.Property(e => e.Level).IsRequired().HasMaxLength(16);
            entity.Property(e => e.Message).IsRequired();
            entity.Property(e => e.MetadataJson).HasMaxLength(2048);
            entity.Property(e => e.SequenceNumber).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired(false);
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).IsRequired(false);
            entity.Property(e => e.IsInvalidated).IsRequired().HasDefaultValue(false);

            // Relationships
            entity.HasOne(e => e.Chat)
                .WithMany(c => c.Logs)
                .HasForeignKey(e => e.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.DeletedByUser)
                .WithMany()
                .HasForeignKey(e => e.DeletedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            entity.HasIndex(e => new { e.ChatId, e.CreatedAt });
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_chat_logs_user_id")
                .HasFilter("\"UserId\" IS NOT NULL");
            entity.HasIndex(e => e.DeletedAt)
                .HasDatabaseName("idx_chat_logs_deleted_at")
                .HasFilter("\"DeletedAt\" IS NOT NULL");
            entity.HasIndex(e => new { e.ChatId, e.SequenceNumber, e.Level })
                .HasDatabaseName("idx_chat_logs_chat_id_sequence_role");

            // Global query filter: exclude soft-deleted messages by default
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<PdfDocumentEntity>(entity =>
        {
            entity.ToTable("pdf_documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.GameId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.FileSizeBytes).IsRequired();
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(128);
            entity.Property(e => e.UploadedByUserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.UploadedAt).IsRequired();
            entity.Property(e => e.Metadata).HasMaxLength(2048);
            entity.Property(e => e.ProcessingStatus).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ProcessingError).HasMaxLength(1024);
            entity.Property(e => e.ExtractedTables).HasMaxLength(8192);
            entity.Property(e => e.ExtractedDiagrams).HasMaxLength(8192);
            entity.Property(e => e.AtomicRules).HasMaxLength(8192);
            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.UploadedBy)
                .WithMany()
                .HasForeignKey(e => e.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.GameId, e.UploadedAt });
        });

        modelBuilder.Entity<VectorDocumentEntity>(entity =>
        {
            entity.ToTable("vector_documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.GameId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.PdfDocumentId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ChunkCount).IsRequired();
            entity.Property(e => e.TotalCharacters).IsRequired();
            entity.Property(e => e.IndexingStatus).IsRequired().HasMaxLength(32);
            entity.Property(e => e.EmbeddingModel).IsRequired().HasMaxLength(128);
            entity.Property(e => e.EmbeddingDimensions).IsRequired();
            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.PdfDocument)
                .WithMany()
                .HasForeignKey(e => e.PdfDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.GameId);
            entity.HasIndex(e => e.PdfDocumentId).IsUnique();
        });

        // AI-14: Hybrid search - text chunks table configuration
        modelBuilder.Entity<TextChunkEntity>(entity =>
        {
            entity.ToTable("text_chunks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.GameId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.PdfDocumentId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Content).IsRequired(); // No max length for long text chunks
            entity.Property(e => e.ChunkIndex).IsRequired();
            entity.Property(e => e.PageNumber);
            entity.Property(e => e.CharacterCount).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            // SearchVector column is managed by PostgreSQL trigger, no need to configure here
            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.PdfDocument)
                .WithMany()
                .HasForeignKey(e => e.PdfDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.GameId);
            entity.HasIndex(e => e.PdfDocumentId);
            entity.HasIndex(e => e.ChunkIndex);
            entity.HasIndex(e => e.PageNumber);
        });

        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).HasMaxLength(64);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Resource).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ResourceId).HasMaxLength(64);
            entity.Property(e => e.Result).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Details).HasMaxLength(1024);
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.Property(e => e.UserAgent).HasMaxLength(256);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<AiRequestLogEntity>(entity =>
        {
            entity.ToTable("ai_request_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).HasMaxLength(64);
            entity.Property(e => e.GameId).HasMaxLength(64);
            entity.Property(e => e.Endpoint).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Query).HasMaxLength(2048);
            entity.Property(e => e.ResponseSnippet).HasMaxLength(1024);
            entity.Property(e => e.LatencyMs).IsRequired();
            entity.Property(e => e.TokenCount).HasDefaultValue(0);
            entity.Property(e => e.PromptTokens).HasDefaultValue(0);
            entity.Property(e => e.CompletionTokens).HasDefaultValue(0);
            entity.Property(e => e.Confidence);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1024);
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.Property(e => e.UserAgent).HasMaxLength(256);
            entity.Property(e => e.Model).HasMaxLength(128);
            entity.Property(e => e.FinishReason).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Endpoint);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.GameId);
        });

        modelBuilder.Entity<AgentFeedbackEntity>(entity =>
        {
            entity.ToTable("agent_feedback");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.MessageId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Endpoint).IsRequired().HasMaxLength(32);
            entity.Property(e => e.GameId).HasMaxLength(64);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Outcome).IsRequired().HasMaxLength(32);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.HasIndex(e => new { e.MessageId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.Endpoint);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.GameId);
        });

        modelBuilder.Entity<N8nConfigEntity>(entity =>
        {
            entity.ToTable("n8n_configs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(128);
            entity.Property(e => e.BaseUrl).IsRequired().HasMaxLength(512);
            entity.Property(e => e.ApiKeyEncrypted).IsRequired().HasMaxLength(512);
            entity.Property(e => e.WebhookUrl).HasMaxLength(512);
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.LastTestedAt);
            entity.Property(e => e.LastTestResult).HasMaxLength(512);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(64);
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // EDIT-05: Enhanced Comments System
        modelBuilder.Entity<RuleSpecCommentEntity>(entity =>
        {
            entity.ToTable("rulespec_comments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GameId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Version).IsRequired().HasMaxLength(32);
            entity.Property(e => e.AtomId).HasMaxLength(64);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.CommentText).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt);

            // EDIT-05: Inline Annotations
            entity.Property(e => e.LineNumber);
            entity.Property(e => e.LineContext).HasMaxLength(500);

            // EDIT-05: Comment Threading
            entity.Property(e => e.ParentCommentId);

            // EDIT-05: Resolution Tracking
            entity.Property(e => e.IsResolved).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.ResolvedByUserId).HasMaxLength(64);
            entity.Property(e => e.ResolvedAt);

            // EDIT-05: User Mentions (stored as JSON array)
            entity.Property(e => e.MentionedUserIds)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
                .HasMaxLength(1000);

            // Relationships
            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // EDIT-05: Threading Relationship (self-referencing)
            entity.HasOne(e => e.ParentComment)
                .WithMany(p => p.Replies)
                .HasForeignKey(e => e.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);

            // EDIT-05: Resolution Relationship
            entity.HasOne(e => e.ResolvedByUser)
                .WithMany()
                .HasForeignKey(e => e.ResolvedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            entity.HasIndex(e => new { e.GameId, e.Version });
            entity.HasIndex(e => e.AtomId);

            // EDIT-05: Performance indexes for new features
            entity.HasIndex(e => new { e.GameId, e.Version, e.LineNumber })
                .HasDatabaseName("idx_rulespec_comments_game_version_line");
            entity.HasIndex(e => e.ParentCommentId)
                .HasDatabaseName("idx_rulespec_comments_parent_id");
            entity.HasIndex(e => e.IsResolved)
                .HasDatabaseName("idx_rulespec_comments_is_resolved");
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_rulespec_comments_user_id");
        });

        modelBuilder.Entity<PromptTemplateEntity>(entity =>
        {
            entity.ToTable("prompt_templates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Description).HasMaxLength(512);
            entity.Property(e => e.Category).HasMaxLength(64);
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<PromptVersionEntity>(entity =>
        {
            entity.ToTable("prompt_versions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.TemplateId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.VersionNumber).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.Metadata).HasMaxLength(4096);
            entity.HasOne(e => e.Template)
                .WithMany(t => t.Versions)
                .HasForeignKey(e => e.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TemplateId, e.VersionNumber }).IsUnique();
            entity.HasIndex(e => new { e.TemplateId, e.IsActive });
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<PromptAuditLogEntity>(entity =>
        {
            entity.ToTable("prompt_audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.TemplateId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.VersionId).HasMaxLength(64);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ChangedByUserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ChangedAt).IsRequired();
            entity.Property(e => e.Details).HasMaxLength(2048);
            entity.HasOne(e => e.Template)
                .WithMany(t => t.AuditLogs)
                .HasForeignKey(e => e.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Version)
                .WithMany(v => v.AuditLogs)
                .HasForeignKey(e => e.VersionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ChangedBy)
                .WithMany()
                .HasForeignKey(e => e.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.TemplateId);
            entity.HasIndex(e => e.VersionId);
            entity.HasIndex(e => e.ChangedAt);
            entity.HasIndex(e => e.Action);
        });

        // AUTH-04: Password reset tokens
        modelBuilder.Entity<PasswordResetTokenEntity>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.IsUsed).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UsedAt);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAt);
        });

        // CONFIG-01: System configurations
        modelBuilder.Entity<SystemConfigurationEntity>(entity =>
        {
            entity.ToTable("system_configurations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.ValueType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.RequiresRestart).IsRequired();
            entity.Property(e => e.Environment).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Version).IsRequired();
            entity.Property(e => e.PreviousValue);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.UpdatedByUserId).HasMaxLength(64);
            entity.Property(e => e.LastToggledAt);

            // Relationships
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.UpdatedBy)
                .WithMany()
                .HasForeignKey(e => e.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes for performance
            entity.HasIndex(e => new { e.Key, e.Environment }).IsUnique();
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.Environment);
            entity.HasIndex(e => e.UpdatedAt);
        });

        // N8N-05: Workflow Error Logs
        modelBuilder.Entity<WorkflowErrorLogEntity>(entity =>
        {
            entity.ToTable("workflow_error_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.WorkflowId).HasColumnName("workflow_id").HasMaxLength(255).IsRequired();
            entity.Property(e => e.ExecutionId).HasColumnName("execution_id").HasMaxLength(255).IsRequired();
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(5000).IsRequired();
            entity.Property(e => e.NodeName).HasColumnName("node_name").HasMaxLength(255);
            entity.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
            entity.Property(e => e.StackTrace).HasColumnName("stack_trace").HasMaxLength(10000);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasIndex(e => e.WorkflowId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ExecutionId);
        });

        // OPS-07: Alerting system
        modelBuilder.Entity<AlertEntity>(entity =>
        {
            entity.ToTable("alerts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.AlertType).HasColumnName("alert_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Severity).HasColumnName("severity").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Message).HasColumnName("message").IsRequired();
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(e => e.TriggeredAt).HasColumnName("triggered_at").IsRequired();
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(e => e.ChannelSent).HasColumnName("channel_sent").HasColumnType("jsonb");

            // Index for querying active alerts
            entity.HasIndex(e => e.IsActive)
                .HasFilter("is_active = true");
            // Index for alert type and time-based queries
            entity.HasIndex(e => new { e.AlertType, e.TriggeredAt });
        });
    }
}
