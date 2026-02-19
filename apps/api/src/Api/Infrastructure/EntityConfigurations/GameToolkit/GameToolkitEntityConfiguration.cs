using Api.Infrastructure.Entities.GameToolkit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class GameToolkitEntityConfiguration : IEntityTypeConfiguration<GameToolkitEntity>
{
    public void Configure(EntityTypeBuilder<GameToolkitEntity> builder)
    {
        builder.ToTable("GameToolkits");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.GameId).IsRequired();
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Version).IsRequired().HasDefaultValue(1);
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.Property(e => e.IsPublished).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        // JSONB columns for tool configs
        builder.Property(e => e.DiceToolsJson).HasColumnType("jsonb");
        builder.Property(e => e.CardToolsJson).HasColumnType("jsonb");
        builder.Property(e => e.TimerToolsJson).HasColumnType("jsonb");
        builder.Property(e => e.CounterToolsJson).HasColumnType("jsonb");

        // JSONB columns for templates
        builder.Property(e => e.ScoringTemplateJson).HasColumnType("jsonb");
        builder.Property(e => e.TurnTemplateJson).HasColumnType("jsonb");
        builder.Property(e => e.StateTemplate).HasColumnType("jsonb");
        builder.Property(e => e.AgentConfig).HasColumnType("jsonb");

        // Concurrency token
        builder.Property(e => e.RowVersion).IsRowVersion();

        // FK to GameEntity
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.GameId).HasDatabaseName("IX_GameToolkits_GameId");
        builder.HasIndex(e => e.IsPublished).HasDatabaseName("IX_GameToolkits_IsPublished");
        builder.HasIndex(e => new { e.GameId, e.Version })
            .IsUnique()
            .HasDatabaseName("IX_GameToolkits_GameId_Version");
    }
}
