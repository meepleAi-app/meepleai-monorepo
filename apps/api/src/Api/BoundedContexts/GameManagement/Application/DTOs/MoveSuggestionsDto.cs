#pragma warning disable MA0002 // Dictionary without StringComparer
namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// DTO containing move suggestions for Player Mode.
/// Issue #2404 - Player Mode move suggestions
/// </summary>
public sealed record MoveSuggestionsDto
{
    /// <summary>
    /// List of suggested moves
    /// </summary>
    public IReadOnlyList<MoveSuggestionItemDto> Suggestions { get; init; } = Array.Empty<MoveSuggestionItemDto>();

    /// <summary>
    /// Overall confidence in suggestions (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Game session ID these suggestions are for
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Agent that generated these suggestions
    /// </summary>
    public Guid AgentId { get; init; }

    /// <summary>
    /// When suggestions were generated
    /// </summary>
    public DateTime GeneratedAt { get; init; }
}

/// <summary>
/// Individual move suggestion item
/// </summary>
public sealed record MoveSuggestionItemDto
{
    /// <summary>
    /// Unique identifier for this suggestion
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The action/move being suggested
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Reasoning for this suggestion
    /// </summary>
    public string Reasoning { get; init; } = string.Empty;

    /// <summary>
    /// Risk level (low, medium, high)
    /// </summary>
    public string Risk { get; init; } = "medium";

    /// <summary>
    /// Confidence score for this specific suggestion (0.0 to 1.0)
    /// </summary>
    public float ConfidenceScore { get; init; }

    /// <summary>
    /// Expected state changes if applied
    /// </summary>
    public IReadOnlyDictionary<string, object> StateChange { get; init; }
        = new Dictionary<string, object>();
}
