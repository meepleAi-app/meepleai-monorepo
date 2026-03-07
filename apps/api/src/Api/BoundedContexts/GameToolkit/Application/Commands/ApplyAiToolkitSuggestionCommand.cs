using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

/// <summary>
/// Applies an AI-generated toolkit suggestion, creating a new GameToolkit or updating an existing one.
/// The caller reviews the <see cref="AiToolkitSuggestionDto"/> produced by GenerateToolkitFromKb
/// and then submits it here to persist the result.
/// </summary>
internal record ApplyAiToolkitSuggestionCommand(
    Guid GameId,
    Guid UserId,
    Guid? ToolkitId,
    AiToolkitSuggestionDto Suggestion
) : ICommand<GameToolkitDto>;
