using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// Entity Framework configuration for MechanicDraft entity.
/// </summary>
internal sealed class MechanicDraftEntityConfiguration : IEntityTypeConfiguration<MechanicDraftEntity>
{
    public void Configure(EntityTypeBuilder<MechanicDraftEntity> builder)
    {
        builder.ToTable("mechanic_drafts");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(d => d.SharedGameId)
            .HasColumnName("shared_game_id")
            .IsRequired();

        builder.Property(d => d.PdfDocumentId)
            .HasColumnName("pdf_document_id")
            .IsRequired();

        builder.Property(d => d.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(d => d.GameTitle)
            .HasColumnName("game_title")
            .HasMaxLength(300)
            .IsRequired();

        // Human notes columns (text, no size limit)
        builder.Property(d => d.SummaryNotes)
            .HasColumnName("summary_notes")
            .HasColumnType("text")
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(d => d.MechanicsNotes)
            .HasColumnName("mechanics_notes")
            .HasColumnType("text")
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(d => d.VictoryNotes)
            .HasColumnName("victory_notes")
            .HasColumnType("text")
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(d => d.ResourcesNotes)
            .HasColumnName("resources_notes")
            .HasColumnType("text")
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(d => d.PhasesNotes)
            .HasColumnName("phases_notes")
            .HasColumnType("text")
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(d => d.QuestionsNotes)
            .HasColumnName("questions_notes")
            .HasColumnType("text")
            .HasDefaultValue(string.Empty)
            .IsRequired();

        // AI draft columns (text, no size limit)
        builder.Property(d => d.SummaryDraft)
            .HasColumnName("summary_draft")
            .HasColumnType("text")
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(d => d.MechanicsDraft)
            .HasColumnName("mechanics_draft")
            .HasColumnType("text")
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(d => d.VictoryDraft)
            .HasColumnName("victory_draft")
            .HasColumnType("text")
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(d => d.ResourcesDraft)
            .HasColumnName("resources_draft")
            .HasColumnType("text")
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(d => d.PhasesDraft)
            .HasColumnName("phases_draft")
            .HasColumnType("text")
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(d => d.QuestionsDraft)
            .HasColumnName("questions_draft")
            .HasColumnType("text")
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.LastModified)
            .HasColumnName("last_modified")
            .IsRequired();

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasDefaultValue(0)
            .IsRequired();

        // Indexes
        builder.HasIndex(d => d.SharedGameId)
            .HasDatabaseName("ix_mechanic_drafts_shared_game_id");

        builder.HasIndex(d => d.PdfDocumentId)
            .HasDatabaseName("ix_mechanic_drafts_pdf_document_id");

        builder.HasIndex(d => new { d.SharedGameId, d.PdfDocumentId, d.Status })
            .HasDatabaseName("ix_mechanic_drafts_game_pdf_status");

        // Foreign key to SharedGame
        builder.HasOne(d => d.SharedGame)
            .WithMany()
            .HasForeignKey(d => d.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
