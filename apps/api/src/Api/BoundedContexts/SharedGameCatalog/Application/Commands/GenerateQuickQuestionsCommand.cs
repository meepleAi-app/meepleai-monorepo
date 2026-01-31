using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to generate quick questions for a game using AI analysis of the rulebook.
/// </summary>
/// <param name="SharedGameId">The ID of the game to generate questions for.</param>
internal record GenerateQuickQuestionsCommand(Guid SharedGameId)
    : ICommand<GenerateQuickQuestionsResultDto>;