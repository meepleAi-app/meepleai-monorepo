namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Infrastructure entity for RecordScore.
/// Maps domain RecordScore value object to database table.
/// </summary>
public class RecordScoreEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecordPlayerId { get; set; }
    public string Dimension { get; set; } = default!;
    public int Value { get; set; }
    public string? Unit { get; set; }

    // Navigation Property
    public RecordPlayerEntity RecordPlayer { get; set; } = default!;
}
