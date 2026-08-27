using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Infrastructure.Entities.SessionTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for SessionDeckEntity.
/// </summary>
public class SessionDeckConfiguration : IEntityTypeConfiguration<SessionDeckEntity>
{
    public void Configure(EntityTypeBuilder<SessionDeckEntity> builder)
    {
        builder.ToTable("SessionDecks", "session_tracking");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.DeckType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.DrawPileJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(x => x.DiscardPileJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(x => x.HandsJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        // Relationships
        //
        // #3856 — un mazzo appartiene a una sessione OPPURE a un toolbox. Prima esisteva solo
        // SessionId, obbligatorio: il percorso dei toolbox ci passava l'id del toolbox e la chiave
        // esterna verso le sessioni lo rifiutava (23503), quindi l'endpoint rispondeva 500.
        //
        // Il vincolo "esattamente uno" e' lo stesso schema di CK_UserLibraryEntry_GameSource.
        builder.HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Api.BoundedContexts.GameToolbox.Domain.Entities.Toolbox>()
            .WithMany()
            .HasForeignKey(x => x.ToolboxId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_SessionDecks_Owner",
            "(\"SessionId\" IS NOT NULL AND \"ToolboxId\" IS NULL) OR " +
            "(\"SessionId\" IS NULL AND \"ToolboxId\" IS NOT NULL)"));

        builder.HasMany(x => x.Cards)
            .WithOne(x => x.SessionDeck)
            .HasForeignKey(x => x.SessionDeckId)
            .OnDelete(DeleteBehavior.Cascade);

        // Query filter for soft delete
        builder.HasQueryFilter(x => !x.IsDeleted);

        // Indexes
        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => x.ToolboxId);
        builder.HasIndex(x => x.IsDeleted);
    }
}

/// <summary>
/// EF Core configuration for CardEntity.
/// </summary>
public class CardConfiguration : IEntityTypeConfiguration<CardEntity>
{
    public void Configure(EntityTypeBuilder<CardEntity> builder)
    {
        builder.ToTable("Cards", "session_tracking");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(500);

        builder.Property(x => x.Suit)
            .HasMaxLength(50);

        builder.Property(x => x.Value)
            .HasMaxLength(50);

        // Indexes
        builder.HasIndex(x => x.SessionDeckId);
        builder.HasIndex(x => x.SortOrder);
    }
}
