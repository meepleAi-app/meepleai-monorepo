using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class AgentFeedbackEntityConfiguration : IEntityTypeConfiguration<AgentFeedbackEntity>
{
    public void Configure(EntityTypeBuilder<AgentFeedbackEntity> builder)
    {
        builder.ToTable("agent_feedback");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.MessageId).IsRequired().HasMaxLength(128);
        builder.Property(e => e.Endpoint).IsRequired().HasMaxLength(32);
        builder.Property(e => e.GameId).HasMaxLength(64);
        builder.Property(e => e.UserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Outcome).IsRequired().HasMaxLength(32);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.HasIndex(e => new { e.MessageId, e.UserId }).IsUnique();
        builder.HasIndex(e => e.Endpoint);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.GameId);
    }
}
