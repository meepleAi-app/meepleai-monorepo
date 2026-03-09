using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// Entity Framework configuration for RulebookAnalysis entity.
/// Issue #2402: Rulebook Analysis Service
/// </summary>
internal sealed class RulebookAnalysisEntityConfiguration : IEntityTypeConfiguration<RulebookAnalysisEntity>
{
    public void Configure(EntityTypeBuilder<RulebookAnalysisEntity> builder)
    {
        builder.ToTable("rulebook_analyses");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(a => a.SharedGameId)
            .HasColumnName("shared_game_id")
            .IsRequired();

        builder.Property(a => a.PdfDocumentId)
            .HasColumnName("pdf_document_id")
            .IsRequired();

        builder.Property(a => a.GameTitle)
            .HasColumnName("game_title")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(a => a.Summary)
            .HasColumnName("summary")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(a => a.KeyMechanicsJson)
            .HasColumnName("key_mechanics_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(a => a.VictoryConditionsJson)
            .HasColumnName("victory_conditions_json")
            .HasColumnType("jsonb");

        builder.Property(a => a.ResourcesJson)
            .HasColumnName("resources_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(a => a.GamePhasesJson)
            .HasColumnName("game_phases_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(a => a.CommonQuestionsJson)
            .HasColumnName("common_questions_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        // Issue #5448: Key concepts / glossary terms
        builder.Property(a => a.KeyConceptsJson)
            .HasColumnName("key_concepts_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        // Issue #5449: Generated FAQ entries
        builder.Property(a => a.GeneratedFaqsJson)
            .HasColumnName("generated_faqs_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        // Issue #5450: Game state tracking schema
        builder.Property(a => a.GameStateSchemaJson)
            .HasColumnName("game_state_schema_json")
            .HasColumnType("jsonb");

        // Issue #5452: Critical section quality gate
        builder.Property(a => a.CompletionStatus)
            .HasColumnName("completion_status")
            .HasDefaultValue(0) // Complete by default
            .IsRequired();

        builder.Property(a => a.MissingSectionsJson)
            .HasColumnName("missing_sections_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(a => a.ConfidenceScore)
            .HasColumnName("confidence_score")
            .HasPrecision(5, 4) // 0.0000 to 1.0000
            .IsRequired();

        builder.Property(a => a.Version)
            .HasColumnName("version")
            .HasMaxLength(20)
            .HasDefaultValue("1.0")
            .IsRequired();

        builder.Property(a => a.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(a => a.Source)
            .HasColumnName("source")
            .HasDefaultValue(0) // AI by default
            .IsRequired();

        builder.Property(a => a.AnalyzedAt)
            .HasColumnName("analyzed_at")
            .IsRequired();

        builder.Property(a => a.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        // Indexes for query performance
        builder.HasIndex(a => a.SharedGameId)
            .HasDatabaseName("ix_rulebook_analyses_shared_game_id");

        builder.HasIndex(a => a.PdfDocumentId)
            .HasDatabaseName("ix_rulebook_analyses_pdf_document_id");

        builder.HasIndex(a => new { a.SharedGameId, a.PdfDocumentId, a.IsActive })
            .HasDatabaseName("ix_rulebook_analyses_game_pdf_active");

        builder.HasIndex(a => new { a.SharedGameId, a.PdfDocumentId, a.Version })
            .HasDatabaseName("ix_rulebook_analyses_game_pdf_version")
            .IsUnique(); // Only one version per game+PDF combination

        // Foreign key to SharedGame
        builder.HasOne(a => a.SharedGame)
            .WithMany()
            .HasForeignKey(a => a.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
