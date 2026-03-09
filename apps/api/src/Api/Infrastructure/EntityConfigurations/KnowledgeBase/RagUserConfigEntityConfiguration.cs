using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for RagUserConfigEntity.
/// Issue #5311: Per-user RAG configuration persistence.
/// </summary>
internal class RagUserConfigEntityConfiguration : IEntityTypeConfiguration<RagUserConfigEntity>
{
    public void Configure(EntityTypeBuilder<RagUserConfigEntity> builder)
    {
        builder.ToTable("rag_user_configs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.ConfigJson).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        builder.HasIndex(e => e.UserId).IsUnique();

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
