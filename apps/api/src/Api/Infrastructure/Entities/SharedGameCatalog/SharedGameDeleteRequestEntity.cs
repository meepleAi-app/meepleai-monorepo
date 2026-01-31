namespace Api.Infrastructure.Entities.SharedGameCatalog;

public class SharedGameDeleteRequestEntity
{
    public Guid Id { get; set; }
    public Guid SharedGameId { get; set; }
    public Guid RequestedBy { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int Status { get; set; } // 0=Pending, 1=Approved, 2=Rejected
    public Guid? ReviewedBy { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public SharedGameEntity SharedGame { get; set; } = default!;
}
