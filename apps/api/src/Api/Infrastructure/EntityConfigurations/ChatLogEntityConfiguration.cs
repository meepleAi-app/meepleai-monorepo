using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

public class ChatLogEntityConfiguration : IEntityTypeConfiguration<ChatLogEntity>
{
    public void Configure(EntityTypeBuilder<ChatLogEntity> builder)
    {
        builder.ToTable("chat_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.ChatId).IsRequired();
        builder.Property(e => e.Level).IsRequired().HasMaxLength(16);
        builder.Property(e => e.Message).IsRequired();
        builder.Property(e => e.MetadataJson).HasMaxLength(2048);
        builder.Property(e => e.SequenceNumber).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired(false);
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.DeletedAt).IsRequired(false);
        builder.Property(e => e.IsInvalidated).IsRequired().HasDefaultValue(false);

        // Relationships
        builder.HasOne(e => e.Chat)
            .WithMany(c => c.Logs)
            .HasForeignKey(e => e.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.DeletedByUser)
            .WithMany()
            .HasForeignKey(e => e.DeletedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(e => new { e.ChatId, e.CreatedAt });
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("idx_chat_logs_user_id")
            .HasFilter("\"UserId\" IS NOT NULL");
        builder.HasIndex(e => e.DeletedAt)
            .HasDatabaseName("idx_chat_logs_deleted_at")
            .HasFilter("\"DeletedAt\" IS NOT NULL");
        builder.HasIndex(e => new { e.ChatId, e.SequenceNumber, e.Level })
            .HasDatabaseName("idx_chat_logs_chat_id_sequence_role");

        // Global query filter: exclude soft-deleted messages by default
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
