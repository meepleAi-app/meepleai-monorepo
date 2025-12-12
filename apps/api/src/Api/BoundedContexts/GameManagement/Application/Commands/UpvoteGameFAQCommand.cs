using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to upvote an FAQ.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
public record UpvoteGameFAQCommand(Guid Id) : ICommand<GameFAQDto>;
