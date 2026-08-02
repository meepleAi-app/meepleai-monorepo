using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

internal class GameCoverAssignmentEntityConfiguration : IEntityTypeConfiguration<GameCoverAssignmentEntity>
{
    public void Configure(EntityTypeBuilder<GameCoverAssignmentEntity> builder)
    {
        builder.ToTable("game_cover_assignments");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.SharedGameId).HasColumnName("shared_game_id").IsRequired();
        builder.Property(e => e.Context).HasColumnName("context").IsRequired().HasConversion<short>();
        builder.Property(e => e.Source).HasColumnName("source").IsRequired().HasConversion<short>();
        builder.Property(e => e.FocalX).HasColumnName("focal_x").IsRequired().HasDefaultValue(0.5);
        builder.Property(e => e.FocalY).HasColumnName("focal_y").IsRequired().HasDefaultValue(0.5);
        builder.Property(e => e.GeneratedR2Key).HasColumnName("generated_r2_key");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        // Optimistic concurrency via Postgres xmin (ADR-060).
        builder.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();

        // At most one assignment per (game, context).
        builder.HasIndex(e => new { e.SharedGameId, e.Context })
            .IsUnique()
            .HasDatabaseName("ux_game_cover_assignments_game_context");

        builder.HasOne(a => a.SharedGame)
            .WithMany(g => g.CoverAssignments)
            .HasForeignKey(a => a.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
