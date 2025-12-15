using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to create a new FAQ for a game.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
internal record CreateGameFAQCommand(
    Guid GameId,
    string Question,
    string Answer
) : ICommand<GameFAQDto>;
