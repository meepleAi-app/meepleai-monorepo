using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class RuleAtomEntityConfiguration : IEntityTypeConfiguration<RuleAtomEntity>
{
    public void Configure(EntityTypeBuilder<RuleAtomEntity> builder)
    {
        builder.ToTable("rule_atoms");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.Key).IsRequired().HasMaxLength(32);
        builder.Property(e => e.Text).IsRequired();
        builder.Property(e => e.Section).HasMaxLength(128);
        builder.Property(e => e.SortOrder).IsRequired();
        builder.HasOne(e => e.RuleSpec)
            .WithMany(r => r.Atoms)
            .HasForeignKey(e => e.RuleSpecId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.RuleSpecId, e.SortOrder });
    }
}
