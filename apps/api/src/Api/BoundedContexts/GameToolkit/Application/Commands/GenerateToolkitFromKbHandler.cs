using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

/// <summary>
/// Generates an AI-powered toolkit suggestion by querying KB vector chunks
/// for game rules and asking the LLM to extract mechanical components.
/// Returns the suggestion DTO (not persisted — caller reviews and applies).
/// NOTE: Vector store (Qdrant) has been removed. This handler now returns
/// a default suggestion until a replacement retrieval mechanism is in place.
/// </summary>
internal class GenerateToolkitFromKbHandler
    : IRequestHandler<GenerateToolkitFromKbCommand, AiToolkitSuggestionDto>
{
    private readonly ILogger<GenerateToolkitFromKbHandler> _logger;

    public GenerateToolkitFromKbHandler(
        ILogger<GenerateToolkitFromKbHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<AiToolkitSuggestionDto> Handle(
        GenerateToolkitFromKbCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Vector store (Qdrant) has been removed — return default suggestion.
        _logger.LogWarning(
            "Vector store removed, returning default toolkit suggestion for game {GameId}",
            request.GameId);
        return Task.FromResult(CreateDefaultSuggestion("Vector store not available. Provided generic defaults."));
    }

    private static AiToolkitSuggestionDto CreateDefaultSuggestion(string reasoning) => new(
        ToolkitName: "Game Toolkit",
        DiceTools: [new("Standard Dice", DiceType.D6, 2, null, false, null)],
        CounterTools: [],
        TimerTools: [],
        ScoringTemplate: new(["Points"], "points", ScoreType.Points),
        TurnTemplate: new(TurnOrderType.RoundRobin, []),
        Overrides: new(false, false, false),
        Reasoning: reasoning
    );
}
