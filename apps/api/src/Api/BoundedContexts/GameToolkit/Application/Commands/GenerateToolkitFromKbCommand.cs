using Api.BoundedContexts.GameToolkit.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

/// <summary>
/// Reads KB cards for a game and uses the LLM to generate a draft GameToolkit.
/// Returns the suggestion DTO (not yet persisted — caller reviews and applies).
/// </summary>
internal record GenerateToolkitFromKbCommand(
    Guid GameId,
    Guid UserId
) : IRequest<AiToolkitSuggestionDto>;
