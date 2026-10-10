using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>knowledge_base.agent_profile_versions</c> (ADR-095 D2, #4167). Gli invarianti dell'aggregato
/// hanno un riscontro nello schema: numero di versione unico per profilo, al più una bozza e al più
/// una versione pubblicata (indici unici parziali sullo stato).
/// </summary>
public sealed class AgentProfileVersionConfiguration : IEntityTypeConfiguration<AgentProfileVersion>
{
    public void Configure(EntityTypeBuilder<AgentProfileVersion> builder)
    {
        builder.ToTable("agent_profile_versions", "knowledge_base");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(v => v.AgentProfileId).HasColumnName("agent_profile_id").IsRequired();
        builder.Property(v => v.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(v => v.Status).HasColumnName("status").IsRequired();

        builder.Property(v => v.Content)
            .HasColumnName("content")
            .HasColumnType("jsonb")
            .HasConversion(
                content => AgentProfileContentJson.Serialize(content),
                json => AgentProfileContentJson.Deserialize(json))
            .IsRequired();

        builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(v => v.PublishedAt).HasColumnName("published_at");
        builder.Property(v => v.ArchivedAt).HasColumnName("archived_at");

        builder.HasIndex(v => new { v.AgentProfileId, v.VersionNumber })
            .IsUnique()
            .HasDatabaseName("ux_agent_profile_versions_profile_version");

        // Indici con nome nel HasIndex, non con HasDatabaseName: due HasIndex senza nome sulla stessa
        // colonna sono per EF lo stesso indice, e il secondo sovrascriverebbe il filtro del primo.
        builder.HasIndex(v => v.AgentProfileId, "ux_agent_profile_versions_one_draft")
            .IsUnique()
            .HasFilter($"status = {(int)AgentProfileVersionStatus.Draft}");

        builder.HasIndex(v => v.AgentProfileId, "ux_agent_profile_versions_one_published")
            .IsUnique()
            .HasFilter($"status = {(int)AgentProfileVersionStatus.Published}");
    }
}
