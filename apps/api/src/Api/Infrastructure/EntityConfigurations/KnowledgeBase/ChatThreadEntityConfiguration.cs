using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// La tabella <c>ChatThreads</c> resta mappata per convenzione; qui l'indice sulla sessione live
/// (#4276), su cui il turno dell'assistente cerca il suo thread — senza chiave esterna: la sessione è un
/// aggregato di un altro confine, citato per id — e il gioco privato del thread (#4244).
/// </summary>
internal class ChatThreadEntityConfiguration : IEntityTypeConfiguration<ChatThreadEntity>
{
    public void Configure(EntityTypeBuilder<ChatThreadEntity> builder)
    {
        builder.HasIndex(t => t.LiveSessionId).HasDatabaseName("IX_ChatThreads_LiveSessionId");

        // #4244: il gioco privato ha la sua colonna, con la sua chiave esterna. Cancellato il gioco,
        // i suoi thread spariscono con lui.
        builder.HasOne<PrivateGameEntity>()
            .WithMany()
            .HasForeignKey(t => t.PrivateGameId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => t.PrivateGameId);
    }
}
