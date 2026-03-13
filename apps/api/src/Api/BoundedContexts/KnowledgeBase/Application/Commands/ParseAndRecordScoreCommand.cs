using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to parse a natural language message for score information and optionally record it.
/// </summary>
internal sealed record ParseAndRecordScoreCommand : ICommand<ScoreParseResultDto>
{
    /// <summary>
    /// The live session ID to associate the score with.
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// The natural language message to parse (e.g., "Marco ha 5 punti").
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// If true and confidence is high enough, automatically record the score.
    /// </summary>
    public bool AutoRecord { get; init; }
}
