using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

internal class SharedGameDeleteRequestEntityConfiguration : IEntityTypeConfiguration<SharedGameDeleteRequestEntity>
{
    public void Configure(EntityTypeBuilder<SharedGameDeleteRequestEntity> builder)
    {
        builder.ToTable("shared_game_delete_requests");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.SharedGameId).HasColumnName("shared_game_id").IsRequired();
        builder.Property(e => e.RequestedBy).HasColumnName("requested_by").IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason").IsRequired().HasColumnType("text");
        builder.Property(e => e.Status).HasColumnName("status").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(e => e.ReviewComment).HasColumnName("review_comment").HasColumnType("text");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");

        builder.HasIndex(e => e.Status).HasDatabaseName("ix_delete_requests_status");
        builder.HasIndex(e => e.SharedGameId).HasDatabaseName("ix_delete_requests_shared_game_id");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_delete_requests_created_at");

        // Many-to-one with SharedGame
        builder.HasOne(r => r.SharedGame)
            .WithMany()
            .HasForeignKey(r => r.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
