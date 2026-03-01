using System.Text.Json;
using Api.Services;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// LLM-powered implementation of game state schema generation.
/// Uses ILlmService for AI-powered analysis of rulebook content.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
internal sealed class LlmGameStateSchemaGenerator : IGameStateSchemaGenerator
{
    private readonly ILlmService _llmService;
    private readonly ILogger<LlmGameStateSchemaGenerator> _logger;

    private const string SystemPrompt = """
        You are an expert game designer and data analyst. Your task is to analyze board game rulebooks
        and extract the key elements that need to be tracked during gameplay.

        Based on the rulebook content provided, generate a JSON Schema that defines the structure
        for tracking game state. The schema should include:

        1. **Players**: Player-specific state (name, score, resources, inventory, position, etc.)
        2. **Game State**: Global game state (current turn, phase, round, active player)
        3. **Board State**: Any board-related tracking (tiles, spaces, zones, areas)
        4. **Resources**: Game resources that need tracking (cards, tokens, money, materials)
        5. **Victory Conditions**: How winning is determined

        Return a JSON object with this exact structure:
        {
            "schema": { /* JSON Schema for game state */ },
            "confidence": 0.85,
            "extractedElements": ["players", "score", "resources", "turns"]
        }

        Guidelines:
        - Use JSON Schema draft-07 format
        - Include "type", "properties", and "required" fields
        - Use appropriate types: "string", "number", "integer", "boolean", "array", "object"
        - For enums, use "enum" with specific values (e.g., phases, player colors)
        - Confidence should be 0.0 to 1.0 based on how complete the rulebook content was
        - ExtractedElements should list the main game elements you identified

        CRITICAL: Return ONLY the JSON object, no markdown or explanations.
        """;

    public LlmGameStateSchemaGenerator(
        ILlmService llmService,
        ILogger<LlmGameStateSchemaGenerator> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameStateSchemaResult> GenerateSchemaAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating game state schema for: {GameName}", gameName);

        // Truncate rulebook content if too long (LLM context limits)
        var truncatedContent = rulebookContent.Length > 15000
            ? string.Concat(rulebookContent.AsSpan(0, 15000), "\n\n[Content truncated...]")
            : rulebookContent;

        var userPrompt = $"""
            Game: {gameName}

            Rulebook Content:
            {truncatedContent}

            Please analyze this rulebook and generate a JSON Schema for tracking the game state.
            """;

        var result = await _llmService.GenerateJsonAsync<SchemaGenerationResponse>(
            SystemPrompt,
            userPrompt,
            RequestSource.RagPipeline,
            cancellationToken).ConfigureAwait(false);

        if (result == null)
        {
            _logger.LogWarning("LLM returned null for schema generation, using fallback schema");
            return CreateFallbackSchema(gameName);
        }

        _logger.LogInformation(
            "Schema generated for {GameName} with confidence {Confidence}, extracted {ElementCount} elements",
            gameName, result.Confidence, result.ExtractedElements?.Count ?? 0);

        // Serialize the schema back to JSON string
        var schemaJson = result.Schema != null
            ? JsonSerializer.Serialize(result.Schema)
            : CreateDefaultSchemaJson(gameName);

        return new GameStateSchemaResult(
            schemaJson,
            result.Confidence,
            result.ExtractedElements ?? new List<string>());
    }

    private static GameStateSchemaResult CreateFallbackSchema(string gameName)
    {
        return new GameStateSchemaResult(
            CreateDefaultSchemaJson(gameName),
            0.3m, // Low confidence for fallback
            new List<string> { "players", "score", "turns" });
    }

    private static string CreateDefaultSchemaJson(string gameName)
    {
        var defaultSchema = new
        {
            type = "object",
            title = $"{gameName} Game State",
            properties = new
            {
                players = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string" },
                            score = new { type = "integer", @default = 0 }
                        },
                        required = new[] { "name" }
                    }
                },
                gameState = new
                {
                    type = "object",
                    properties = new
                    {
                        currentTurn = new { type = "integer", minimum = 1 },
                        currentPlayerIndex = new { type = "integer", minimum = 0 },
                        phase = new { type = "string", @enum = new[] { "setup", "playing", "scoring", "ended" } }
                    }
                }
            },
            required = new[] { "players", "gameState" }
        };

        return JsonSerializer.Serialize(defaultSchema);
    }

    /// <summary>
    /// Internal record for deserializing LLM response.
    /// </summary>
    private sealed record SchemaGenerationResponse(
        JsonElement? Schema,
        decimal Confidence,
        List<string>? ExtractedElements);
}
