using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to delete an FAQ.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
public record DeleteGameFAQCommand(Guid Id) : ICommand;
