using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

// N8N-05: Workflow Error Logs
public class WorkflowErrorLogEntityConfiguration : IEntityTypeConfiguration<WorkflowErrorLogEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowErrorLogEntity> builder)
    {
        builder.ToTable("workflow_error_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.WorkflowId).HasColumnName("workflow_id").HasMaxLength(255).IsRequired();
        builder.Property(e => e.ExecutionId).HasColumnName("execution_id").HasMaxLength(255).IsRequired();
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(5000).IsRequired();
        builder.Property(e => e.NodeName).HasColumnName("node_name").HasMaxLength(255);
        builder.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
        builder.Property(e => e.StackTrace).HasColumnName("stack_trace").HasMaxLength(10000);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => e.WorkflowId);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.ExecutionId);
    }
}
