using Api.Infrastructure.Entities.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Administration;

internal sealed class ServiceHealthStateEntityConfiguration
    : IEntityTypeConfiguration<ServiceHealthStateEntity>
{
    public void Configure(EntityTypeBuilder<ServiceHealthStateEntity> builder)
    {
        builder.HasIndex(e => e.ServiceName).IsUnique();
    }
}
