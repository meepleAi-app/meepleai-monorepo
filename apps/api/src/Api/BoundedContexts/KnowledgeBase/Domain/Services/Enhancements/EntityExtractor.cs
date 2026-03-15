using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

/// <summary>
/// RAG Enhancement: Graph RAG entity extractor.
/// Uses LLM to identify entities (games, mechanics, components, phases, actions, rules)
/// and their relationships from board game rulebook text.
/// </summary>
internal sealed class EntityExtractor : IEntityExtractor
{
    private readonly ILlmService _llmService;
    private readonly ILogger<EntityExtractor> _logger;

    private const float DefaultConfidence = 0.8f;

    private const string ExtractionSystemPrompt = """
        You are a board game knowledge graph extractor.
        Given a game title and rulebook text, extract entities and their relationships.

        Entity types: Game, Mechanic, Component, Phase, Action, Rule
        Relation types: HasMechanic, HasComponent, HasPhase, RequiresAction, InteractsWith, Overrides

        Return a JSON object with a "Relations" array. Each relation has:
        - SourceEntity: name of the source entity
        - SourceType: one of the entity types above
        - Relation: one of the relation types above
        - TargetEntity: name of the target entity
        - TargetType: one of the entity types above

        Extract only clearly stated relationships. Do not infer or speculate.
        Return an empty Relations array if no clear relationships can be identified.
        """;

    public EntityExtractor(ILlmService llmService, ILogger<EntityExtractor> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<EntityExtractionResult> ExtractEntitiesAsync(
        Guid gameId, string gameTitle, string textContent,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(textContent))
        {
            return new EntityExtractionResult([], 0);
        }

        ct.ThrowIfCancellationRequested();

        var userPrompt = $"Game: {gameTitle}\n\nRulebook text:\n{textContent}";

        try
        {
            var response = await _llmService.GenerateJsonAsync<EntityExtractionResponse>(
                ExtractionSystemPrompt,
                userPrompt,
                RequestSource.RagClassification,
                ct).ConfigureAwait(false);

            if (response?.Relations is null || response.Relations.Count == 0)
            {
                _logger.LogInformation(
                    "Graph RAG extraction returned no relations for game {GameId} ({GameTitle})",
                    gameId, gameTitle);
                return new EntityExtractionResult([], 0);
            }

            var relations = response.Relations
                .Select(r => new ExtractedRelation(
                    r.SourceEntity, r.SourceType,
                    r.Relation, r.TargetEntity, r.TargetType,
                    DefaultConfidence))
                .ToList();

            var uniqueEntities = relations
                .SelectMany(r => new[] { r.SourceEntity, r.TargetEntity })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            _logger.LogInformation(
                "Graph RAG extracted {RelationCount} relations and {EntityCount} entities for game {GameId} ({GameTitle})",
                relations.Count, uniqueEntities, gameId, gameTitle);

            return new EntityExtractionResult(relations, uniqueEntities);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Graph RAG entity extraction failed for game {GameId} ({GameTitle}), returning empty result",
                gameId, gameTitle);
            return new EntityExtractionResult([], 0);
        }
    }
}

/// <summary>
/// LLM response shape for entity extraction. Deserialized from JSON.
/// </summary>
internal sealed record EntityExtractionResponse(List<RawRelation> Relations);

internal sealed record RawRelation(
    string SourceEntity, string SourceType,
    string Relation, string TargetEntity, string TargetType);
