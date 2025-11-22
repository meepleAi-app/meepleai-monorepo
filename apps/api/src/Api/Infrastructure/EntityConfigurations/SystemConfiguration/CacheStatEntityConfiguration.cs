using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

public class CacheStatEntityConfiguration : IEntityTypeConfiguration<CacheStatEntity>
{
    public void Configure(EntityTypeBuilder<CacheStatEntity> builder)
    {
        builder.ToTable("cache_stats");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.GameId).IsRequired().HasMaxLength(50);
        builder.Property(e => e.QuestionHash).IsRequired().HasMaxLength(64);
        builder.Property(e => e.HitCount).IsRequired();
        builder.Property(e => e.MissCount).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.LastHitAt).IsRequired();
        builder.HasIndex(e => e.QuestionHash);
        builder.HasIndex(e => e.GameId);
    }
}
