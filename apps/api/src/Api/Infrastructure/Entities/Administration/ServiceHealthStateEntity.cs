using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.Administration;

[Table("service_health_states")]
public class ServiceHealthStateEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    [Column("service_name")]
    public string ServiceName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("current_status")]
    public string CurrentStatus { get; set; } = "Healthy";

    [Required]
    [MaxLength(20)]
    [Column("previous_status")]
    public string PreviousStatus { get; set; } = "Healthy";

    [Column("consecutive_failures")]
    public int ConsecutiveFailures { get; set; }

    [Column("consecutive_successes")]
    public int ConsecutiveSuccesses { get; set; }

    [Required]
    [Column("last_transition_at")]
    public DateTime LastTransitionAt { get; set; } = DateTime.UtcNow;

    [Column("last_notified_at")]
    public DateTime? LastNotifiedAt { get; set; }

    [Column("last_description")]
    public string? LastDescription { get; set; }

    [Required]
    [Column("tags", TypeName = "jsonb")]
    public string Tags { get; set; } = "[]";

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
