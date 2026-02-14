using Api.BoundedContexts.UserLibrary.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to create/configure an agent for a game with custom typology and strategy.
/// Issue #5: Backend Agent Creation with Custom Config API.
/// </summary>
/// <remarks>
/// This command creates agent configuration on the user's library entry for a game.
/// It follows the existing UserLibrary domain pattern where agent config is stored
/// on UserLibraryEntry rather than creating separate Agent entities.
/// </remarks>
/// <param name="GameId">Game to create agent for</param>
/// <param name="TypologyId">Agent typology ID</param>
/// <param name="StrategyName">RAG strategy name (Fast, Balanced, etc.)</param>
/// <param name="StrategyParameters">Optional strategy-specific parameters (JSON)</param>
/// <param name="UserId">User creating the agent</param>
public record CreateGameAgentCommand(
    Guid GameId,
    Guid TypologyId,
    string StrategyName,
    string? StrategyParameters,
    Guid UserId
) : IRequest<CreateGameAgentResult>;

/// <summary>
/// Result of agent creation command.
/// </summary>
public record CreateGameAgentResult
{
    public required Guid LibraryEntryId { get; init; }
    public required Guid GameId { get; init; }
    public required string Status { get; init; }  // "processing" | "ready"
    public required AgentTypologyInfo Typology { get; init; }
    public required AgentStrategyInfo Strategy { get; init; }
}

public record AgentTypologyInfo
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}

public record AgentStrategyInfo
{
    public required string Name { get; init; }
    public required string? Parameters { get; init; }
}
