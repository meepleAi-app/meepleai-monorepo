using Api.Infrastructure.Entities.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Authentication;

/// <summary>
/// EF Core configuration for InvitationGameSuggestionEntity.
/// Admin Invitation Flow: game suggestions attached to invitation tokens.
/// </summary>
internal class InvitationGameSuggestionEntityConfiguration : IEntityTypeConfiguration<InvitationGameSuggestionEntity>
{
    public void Configure(EntityTypeBuilder<InvitationGameSuggestionEntity> builder)
    {
        builder.ToTable("invitation_game_suggestions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.InvitationTokenId)
            .IsRequired();

        builder.Property(e => e.GameId)
            .IsRequired();

        builder.Property(e => e.Type)
            .IsRequired()
            .HasMaxLength(16);

        // Indexes
        builder.HasIndex(e => e.InvitationTokenId)
            .HasDatabaseName("IX_InvitationGameSuggestions_InvitationTokenId");

        builder.HasIndex(e => new { e.InvitationTokenId, e.GameId })
            .IsUnique()
            .HasDatabaseName("IX_InvitationGameSuggestions_TokenId_GameId");

        // Relationship configured on parent (InvitationTokenEntityConfiguration)
    }
}
