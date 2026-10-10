using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>knowledge_base.agent_profiles</c>: una sola riga, imposta dal vincolo di check sull'id
/// (ADR-095 D1, #4167). La riga nasce dalla migration di seed.
/// </summary>
public sealed class AgentProfileConfiguration : IEntityTypeConfiguration<AgentProfile>
{
    public void Configure(EntityTypeBuilder<AgentProfile> builder)
    {
        builder.ToTable("agent_profiles", "knowledge_base", t =>
            t.HasCheckConstraint(
                "ck_agent_profiles_singleton",
                $"id = '{AgentProfile.SingletonId}'"));

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Pattern xmin di ADR-060. UpdatedAt cambia a ogni comando, quindi anche un comando che
        // tocca solo le versioni aggiorna la radice e passa da questo controllo.
        builder.Property(p => p.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasMany(p => p.Versions)
            .WithOne()
            .HasForeignKey(v => v.AgentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.Published);
        builder.Ignore(p => p.Draft);
    }
}
