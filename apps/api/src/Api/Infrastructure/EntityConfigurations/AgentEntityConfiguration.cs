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
        
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(e => e.Type)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(e => e.StrategyName)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(e => e.StrategyParametersJson)
            .IsRequired()
            .HasColumnType("jsonb"); // PostgreSQL JSONB for efficient querying
        
        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);
        
        builder.Property(e => e.CreatedAt)
            .IsRequired();
        
        builder.Property(e => e.LastInvokedAt);
        
        builder.Property(e => e.InvocationCount)
            .IsRequired()
            .HasDefaultValue(0);
        
        // Indexes for common queries
        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => e.Type);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => e.LastInvokedAt);

        // DEPRECATED properties for backward compatibility
        builder.Property(e => e.GameId); // Optional, nullable
        builder.Property(e => e.Kind).HasMaxLength(50); // Optional, nullable
    }
}