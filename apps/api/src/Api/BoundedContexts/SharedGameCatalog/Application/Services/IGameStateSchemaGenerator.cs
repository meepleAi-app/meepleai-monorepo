namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Service interface for generating game state schemas using AI.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
internal interface IGameStateSchemaGenerator
{
    /// <summary>
    /// Generates a JSON Schema for game state tracking from rulebook content.
    /// </summary>
    /// <param name="gameName">Name of the game</param>
    /// <param name="rulebookContent">Extracted text from rulebook PDF</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated schema result with JSON and confidence score</returns>
    Task<GameStateSchemaResult> GenerateSchemaAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of AI schema generation.
/// </summary>
/// <param name="SchemaJson">The generated JSON Schema as a string</param>
/// <param name="ConfidenceScore">AI confidence in the schema (0-1)</param>
/// <param name="ExtractedElements">List of extracted game elements</param>
internal record GameStateSchemaResult(
    string SchemaJson,
    decimal ConfidenceScore,
    IReadOnlyList<string> ExtractedElements
);
