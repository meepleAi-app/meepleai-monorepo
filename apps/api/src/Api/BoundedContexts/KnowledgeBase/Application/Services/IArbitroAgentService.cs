using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Interface for Arbitro Agent service - AI-powered move validation.
/// Issue #3760: Arbitro Agent Move Validation Logic.
/// </summary>
internal interface IArbitroAgentService
{
    /// <summary>
    /// Validates a player move using AI-powered arbitration.
    /// </summary>
    /// <param name="session">Active game session.</param>
    /// <param name="move">Move to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with AI reasoning.</returns>
    Task<MoveValidationResultDto> ValidateMoveAsync(
        GameSession session,
        Move move,
        CancellationToken cancellationToken = default);
}
