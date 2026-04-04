using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SystemConfiguration;

/// <summary>
/// EF Core configuration for TierDefinition entity.
/// D3: Game Night Flow - tier system definitions.
/// </summary>
internal class TierDefinitionConfiguration : IEntityTypeConfiguration<TierDefinition>
{
    public void Configure(EntityTypeBuilder<TierDefinition> builder)
    {
        builder.ToTable("tier_definitions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("name");

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("display_name");

        builder.Property(e => e.LlmModelTier)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("llm_model_tier");

        builder.Property(e => e.IsDefault)
            .HasColumnName("is_default");

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at");

        // Owned value object — stored as columns in the same table
        builder.OwnsOne(e => e.Limits, limits =>
        {
            limits.Property(l => l.MaxPrivateGames)
                .HasColumnName("max_private_games");
            limits.Property(l => l.MaxPdfUploadsPerMonth)
                .HasColumnName("max_pdf_uploads_per_month");
            limits.Property(l => l.MaxPdfSizeBytes)
                .HasColumnName("max_pdf_size_bytes");
            limits.Property(l => l.MaxAgents)
                .HasColumnName("max_agents");
            limits.Property(l => l.MaxAgentQueriesPerDay)
                .HasColumnName("max_agent_queries_per_day");
            limits.Property(l => l.MaxSessionQueries)
                .HasColumnName("max_session_queries");
            limits.Property(l => l.MaxSessionPlayers)
                .HasColumnName("max_session_players");
            limits.Property(l => l.MaxPhotosPerSession)
                .HasColumnName("max_photos_per_session");
            limits.Property(l => l.SessionSaveEnabled)
                .HasColumnName("session_save_enabled");
            limits.Property(l => l.MaxCatalogProposalsPerWeek)
                .HasColumnName("max_catalog_proposals_per_week");
        });

        // Unique index on name
        builder.HasIndex(e => e.Name).IsUnique();
    }
}
