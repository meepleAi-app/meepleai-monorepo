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
    public DbSet<GameEntity> Games => Set<GameEntity>();
    public DbSet<RuleSpecEntity> RuleSpecs => Set<RuleSpecEntity>();
    public DbSet<RuleAtomEntity> RuleAtoms => Set<RuleAtomEntity>();
    public DbSet<AgentEntity> Agents => Set<AgentEntity>();
    public DbSet<ChatEntity> Chats => Set<ChatEntity>();
    public DbSet<ChatLogEntity> ChatLogs => Set<ChatLogEntity>();
    public DbSet<PdfDocumentEntity> PdfDocuments => Set<PdfDocumentEntity>();
    public DbSet<VectorDocumentEntity> VectorDocuments => Set<VectorDocumentEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<AiRequestLogEntity> AiRequestLogs => Set<AiRequestLogEntity>();
    public DbSet<AgentFeedbackEntity> AgentFeedbacks => Set<AgentFeedbackEntity>();
    public DbSet<N8nConfigEntity> N8nConfigs => Set<N8nConfigEntity>();

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
            entity.HasMany(e => e.Sessions)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
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
            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => new { e.GameId, e.Version }).IsUnique();
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
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne(e => e.Chat)
                .WithMany(c => c.Logs)
                .HasForeignKey(e => e.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.ChatId, e.CreatedAt });
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
    }
}
