using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// EF Core entity for QuickQuestion.
/// Maps domain entity to database table.
/// </summary>
public sealed class QuickQuestionEntity
{
    public Guid Id { get; set; }
    public Guid SharedGameId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public QuestionCategory Category { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsGenerated { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }

    // Navigation property
    public SharedGameEntity SharedGame { get; set; } = null!;
}
