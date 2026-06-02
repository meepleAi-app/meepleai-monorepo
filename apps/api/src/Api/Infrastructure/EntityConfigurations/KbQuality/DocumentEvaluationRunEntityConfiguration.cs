using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KbQuality;

/// <summary>
/// EF Core configuration for <see cref="DocumentEvaluationRun"/> (#1675).
/// </summary>
internal sealed class DocumentEvaluationRunEntityConfiguration
    : IEntityTypeConfiguration<DocumentEvaluationRun>
{
    public void Configure(EntityTypeBuilder<DocumentEvaluationRun> builder)
    {
        builder.ToTable("document_evaluation_runs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PdfDocumentId).IsRequired();
        builder.Property(e => e.StartedAt).IsRequired();
        builder.Property(e => e.CompletedAt);
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(e => e.GoldsetVersion).HasMaxLength(64).IsRequired();
        builder.Property(e => e.GoldsetGenerationSeed).IsRequired();
        builder.Property(e => e.CostUsd).HasPrecision(10, 4);
        builder.Property(e => e.TriggeredByAdminId).IsRequired();
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);

        // Metrics as JSON via OwnsOne — full materialization, EF Core 9 JSON columns.
        builder.OwnsOne(e => e.Metrics, mb =>
        {
            mb.ToJson();
            mb.OwnsOne(m => m.Precision);
            mb.OwnsOne(m => m.Ranking);
            mb.OwnsOne(m => m.Latency);
        });

        builder.HasIndex(e => new { e.PdfDocumentId, e.StartedAt });
        builder.HasIndex(e => e.TriggeredByAdminId);
        builder.HasIndex(e => e.CompletedAt);
    }
}
