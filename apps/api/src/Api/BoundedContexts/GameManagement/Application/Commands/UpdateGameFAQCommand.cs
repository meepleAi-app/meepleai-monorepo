using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to update an existing FAQ.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
internal record UpdateGameFAQCommand(
    Guid Id,
    string Question,
    string Answer
) : ICommand<GameFAQDto>;
