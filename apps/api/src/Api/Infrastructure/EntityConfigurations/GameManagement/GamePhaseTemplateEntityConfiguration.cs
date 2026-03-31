using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

internal sealed class GamePhaseTemplateEntityConfiguration : IEntityTypeConfiguration<GamePhaseTemplateEntity>
{
    public void Configure(EntityTypeBuilder<GamePhaseTemplateEntity> builder)
    {
        builder.ToTable("game_phase_templates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(t => t.GameId)
            .HasColumnName("game_id")
            .IsRequired();

        builder.Property(t => t.PhaseName)
            .HasColumnName("phase_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.PhaseOrder)
            .HasColumnName("phase_order")
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasMaxLength(200);

        builder.Property(t => t.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(t => new { t.GameId, t.PhaseOrder })
            .HasDatabaseName("ix_game_phase_templates_game_id_order")
            .IsUnique();

        builder.HasOne(t => t.Game)
            .WithMany()
            .HasForeignKey(t => t.GameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
