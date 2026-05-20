using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SessionTracking;

/// <summary>
/// EF Core configuration for GamebookPhotoArtifact.
/// Uses direct domain entity mapping (no separate persistence entity).
/// Maps the IReadOnlyList&lt;GamebookSegment&gt; collection to a single jsonb column via System.Text.Json.
/// Phase A0.1 (2026-05-19): moved from BoundedContexts/SessionTracking/Infrastructure/Persistence/Configurations
/// to canonical Infrastructure/EntityConfigurations location to align with the rest of the codebase.
/// Iter 1.B — Libro Game Nanolith dogfood demo.
/// </summary>
internal class GamebookPhotoArtifactEntityConfiguration : IEntityTypeConfiguration<GamebookPhotoArtifact>
{
    public void Configure(EntityTypeBuilder<GamebookPhotoArtifact> builder)
    {
        builder.ToTable("gamebook_photo_artifacts", schema: "session_tracking");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CampaignId).HasColumnName("campaign_id").IsRequired();
        builder.Property(e => e.GameBookId).HasColumnName("game_book_id").IsRequired();
        builder.Property(e => e.S3Key).HasColumnName("s3_key").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.OcrFullText).HasColumnName("ocr_full_text").HasColumnType("text");
        builder.Property(e => e.FailureReason).HasColumnName("failure_reason").HasMaxLength(1000);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at").IsRequired();

        builder.Property(e => e.Segments)
            .HasColumnName("segments")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(
                    v.Select(s => new SegmentDto(s.ParagraphNumber, s.SourceText, s.BoundingBox)).ToArray(),
                    (JsonSerializerOptions?)null),
                v => DeserializeSegments(v));

        builder.HasIndex(e => e.CampaignId)
            .HasDatabaseName("ix_gamebook_photo_artifacts_campaign_id");

        builder.HasIndex(e => e.ExpiresAt)
            .HasDatabaseName("ix_gamebook_photo_artifacts_expires_at_active")
            .HasFilter("status <> 99");
    }

    private static IReadOnlyList<GamebookSegment> DeserializeSegments(string json)
    {
        var dtos = JsonSerializer.Deserialize<SegmentDto[]>(json) ?? Array.Empty<SegmentDto>();
        return dtos
            .Select(d => GamebookSegment.Create(d.Number, d.Text, d.Bbox))
            .ToList()
            .AsReadOnly();
    }

    private sealed record SegmentDto(int Number, string Text, string? Bbox);
}
