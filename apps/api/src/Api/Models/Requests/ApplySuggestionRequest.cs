namespace Api.Models.Requests;

/// <summary>
/// Request to apply a move suggestion to the game state.
/// Issue #2404 - Player Mode apply suggestion
/// </summary>
/// <param name="SuggestionId">ID of the suggestion to apply</param>
/// <param name="StateChanges">State changes to apply from the suggestion</param>
internal sealed record ApplySuggestionRequest(
    Guid SuggestionId,
    IReadOnlyDictionary<string, object> StateChanges
);
