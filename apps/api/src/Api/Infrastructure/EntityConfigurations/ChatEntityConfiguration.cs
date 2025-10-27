using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

public class ChatEntityConfiguration : IEntityTypeConfiguration<ChatEntity>
{
    public void Configure(EntityTypeBuilder<ChatEntity> builder)
    {
        builder.ToTable("chats");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.UserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.GameId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.AgentId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.StartedAt).IsRequired();
        builder.Property(e => e.LastMessageAt);
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Game)
            .WithMany(g => g.Chats)
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Agent)
            .WithMany(a => a.Chats)
            .HasForeignKey(e => e.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.UserId, e.LastMessageAt });
        builder.HasIndex(e => new { e.GameId, e.StartedAt });
    }
}
