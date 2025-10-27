using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

public class AgentEntityConfiguration : IEntityTypeConfiguration<AgentEntity>
{
    public void Configure(EntityTypeBuilder<AgentEntity> builder)
    {
        builder.ToTable("agents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(128);
        builder.Property(e => e.Kind).IsRequired().HasMaxLength(32);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.GameId).IsRequired().HasMaxLength(64);
        builder.HasOne(e => e.Game)
            .WithMany(g => g.Agents)
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.GameId, e.Name });
    }
}
