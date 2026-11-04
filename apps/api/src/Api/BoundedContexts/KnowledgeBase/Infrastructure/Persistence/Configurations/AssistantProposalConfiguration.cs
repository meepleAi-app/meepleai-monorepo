using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>knowledge_base.assistant_proposals</c> (spec 2026-10-11 §5.2). Nessuna chiave esterna verso la
/// sessione live o il thread: sono aggregati di altri confini, citati per id.
/// </summary>
public sealed class AssistantProposalConfiguration : IEntityTypeConfiguration<AssistantProposal>
{
    public void Configure(EntityTypeBuilder<AssistantProposal> builder)
    {
        builder.ToTable("assistant_proposals", "knowledge_base");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.LiveSessionId).HasColumnName("live_session_id").IsRequired();
        builder.Property(p => p.ChatThreadId).HasColumnName("chat_thread_id").IsRequired();
        builder.Property(p => p.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(p => p.Kind).HasColumnName("kind").IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").IsRequired();

        builder.Property(p => p.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .HasConversion(payload => AssistantProposalJson.Serialize(payload), json => AssistantProposalJson.DeserializePayload(json))
            .IsRequired();

        builder.Property(p => p.Expected)
            .HasColumnName("expected")
            .HasColumnType("jsonb")
            .HasConversion(expected => AssistantProposalJson.Serialize(expected), json => AssistantProposalJson.DeserializeExpectation(json))
            .IsRequired();

        builder.Property(p => p.ConfirmedPayload)
            .HasColumnName("confirmed_payload")
            .HasColumnType("jsonb")
            .HasConversion(
                confirmed => confirmed == null ? null : AssistantProposalJson.Serialize(confirmed),
                json => json == null ? null : AssistantProposalJson.DeserializeConfirmation(json));

        builder.Property(p => p.FailureCode).HasColumnName("failure_code").HasMaxLength(AssistantProposal.MaxFailureCodeLength);
        builder.Property(p => p.ProfileVersionId).HasColumnName("profile_version_id").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(p => p.ResolvedBy).HasColumnName("resolved_by");

        // Pattern xmin di ADR-060: la prenotazione della conferma si regge su questo controllo.
        builder.Property(p => p.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(p => new { p.LiveSessionId, p.Status }).HasDatabaseName("ix_assistant_proposals_session_status");
        builder.HasIndex(p => p.ChatThreadId).HasDatabaseName("ix_assistant_proposals_thread");

        builder.Ignore(p => p.IsOpen);
    }
}
