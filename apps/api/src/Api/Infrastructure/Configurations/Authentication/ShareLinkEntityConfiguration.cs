using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure.Entities.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.Authentication;

/// <summary>
/// Entity Framework configuration for ShareLink entity.
/// </summary>
internal sealed class ShareLinkEntityConfiguration : IEntityTypeConfiguration<ShareLinkEntity>
{
    public void Configure(EntityTypeBuilder<ShareLinkEntity> builder)
    {
        builder.ToTable("share_links");

        builder.HasKey(sl => sl.Id);

        builder.Property(sl => sl.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(sl => sl.ThreadId)
            .HasColumnName("thread_id")
            .IsRequired();

        builder.Property(sl => sl.CreatorId)
            .HasColumnName("creator_id")
            .IsRequired();

        builder.Property(sl => sl.Role)
            .HasColumnName("role")
            .HasConversion<string>() // Store enum as string
            .IsRequired();

        builder.Property(sl => sl.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(sl => sl.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // #4573: token di concorrenza, la stessa guardia di stato di LibraryShareLinkEntity (#4558).
        // ShareLinkRepository.UpdateAsync ne fissa l'originale a NULL, così una scrittura del proprietario vale
        // solo su un link non ancora revocato: una revoca non si annulla. Niente xmin di proposito: ogni visita
        // è un UPDATE e lo cambierebbe, quindi farebbe fallire la revoca di un link molto visitato.
        // Nessuna migrazione: lo schema non cambia, cambia solo la condizione dell'UPDATE.
        builder.Property(sl => sl.RevokedAt)
            .HasColumnName("revoked_at")
            .IsConcurrencyToken();

        builder.Property(sl => sl.Label)
            .HasColumnName("label")
            .HasMaxLength(200);

        builder.Property(sl => sl.AccessCount)
            .HasColumnName("access_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(sl => sl.LastAccessedAt)
            .HasColumnName("last_accessed_at");

        // Indexes for query performance
        builder.HasIndex(sl => sl.ThreadId)
            .HasDatabaseName("ix_share_links_thread_id");

        builder.HasIndex(sl => sl.CreatorId)
            .HasDatabaseName("ix_share_links_creator_id");

        builder.HasIndex(sl => sl.ExpiresAt)
            .HasDatabaseName("ix_share_links_expires_at");

        builder.HasIndex(sl => sl.RevokedAt)
            .HasDatabaseName("ix_share_links_revoked_at");

        // Foreign keys
        builder.HasOne(sl => sl.ChatThread)
            .WithMany()
            .HasForeignKey(sl => sl.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sl => sl.Creator)
            .WithMany()
            .HasForeignKey(sl => sl.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
