using Api.Infrastructure.Entities.Authentication;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Infrastructure entity for RecordPlayer.
/// Maps domain RecordPlayer to database table.
/// </summary>
public class RecordPlayerEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayRecordId { get; set; }
    public Guid? UserId { get; set; }
    public string DisplayName { get; set; } = default!;

    // Navigation Properties
    public PlayRecordEntity PlayRecord { get; set; } = default!;
    public UserEntity? User { get; set; }
    public ICollection<RecordScoreEntity> Scores { get; set; } = new List<RecordScoreEntity>();
}
