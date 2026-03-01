using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// Entity Framework configuration for TurnOrder persistence entity.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
internal sealed class TurnOrderEntityConfiguration : IEntityTypeConfiguration<TurnOrderEntity>
{
    public void Configure(EntityTypeBuilder<TurnOrderEntity> builder)
    {
        builder.ToTable("turn_orders");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(t => t.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(t => t.PlayerOrderJson)
            .HasColumnName("player_order_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(t => t.CurrentIndex)
            .HasColumnName("current_index")
            .IsRequired();

        builder.Property(t => t.RoundNumber)
            .HasColumnName("round_number")
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // One turn order per session
        builder.HasIndex(t => t.SessionId)
            .HasDatabaseName("ix_turn_orders_session_id")
            .IsUnique();
    }
}
