namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// RAG Enhancement: Graph RAG entity-relation triple.
/// Stores extracted knowledge graph edges linking game concepts
/// (e.g., "Catan" --HasMechanic--> "Trading").
/// </summary>
public class GameEntityRelationEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string SourceEntity { get; set; } = string.Empty;    // "Catan"
    public string SourceType { get; set; } = string.Empty;      // "Game"
    public string Relation { get; set; } = string.Empty;        // "HasMechanic"
    public string TargetEntity { get; set; } = string.Empty;    // "Trading"
    public string TargetType { get; set; } = string.Empty;      // "Mechanic"
    public float Confidence { get; set; }
    public DateTime ExtractedAt { get; set; }

    // Navigation
    public GameEntity? Game { get; set; }
}
