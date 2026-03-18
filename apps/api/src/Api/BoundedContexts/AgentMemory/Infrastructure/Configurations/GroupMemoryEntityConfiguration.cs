using Api.BoundedContexts.AgentMemory.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.AgentMemory.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for GroupMemoryEntity.
/// Table schema, indexes, and JSONB columns for play group memory persistence.
/// </summary>
internal sealed class GroupMemoryEntityConfiguration : IEntityTypeConfiguration<GroupMemoryEntity>
{
    public void Configure(EntityTypeBuilder<GroupMemoryEntity> builder)
    {
        builder.ToTable("group_memories");

        builder.HasKey(e => e.Id);

        // --- Scalar Properties ---

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.CreatorId)
            .HasColumnName("creator_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // --- JSON Columns ---

        builder.Property(e => e.MembersJson)
            .HasColumnName("members_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.PreferencesJson)
            .HasColumnName("preferences_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.StatsJson)
            .HasColumnName("stats_json")
            .HasColumnType("jsonb");

        // --- Indexes ---

        builder.HasIndex(e => e.CreatorId)
            .HasDatabaseName("ix_group_memories_creator_id");
    }
}
