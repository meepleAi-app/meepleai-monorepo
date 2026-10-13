using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// La tabella <c>ChatThreads</c> resta mappata per convenzione; qui solo l'indice sulla sessione live
/// (#4276), su cui il turno dell'assistente cerca il suo thread. Senza chiave esterna: la sessione è un
/// aggregato di un altro confine, citato per id.
/// </summary>
internal class ChatThreadEntityConfiguration : IEntityTypeConfiguration<ChatThreadEntity>
{
    public void Configure(EntityTypeBuilder<ChatThreadEntity> builder)
    {
        builder.HasIndex(t => t.LiveSessionId).HasDatabaseName("IX_ChatThreads_LiveSessionId");
    }
}
