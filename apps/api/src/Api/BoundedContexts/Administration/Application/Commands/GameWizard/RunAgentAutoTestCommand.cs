using Api.BoundedContexts.Administration.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.GameWizard;

/// <summary>
/// Runs an automated test suite against a game's RAG agent.
/// Sends 5-10 standard board game questions and evaluates response quality.
/// </summary>
public record RunAgentAutoTestCommand(
    Guid GameId,
    Guid RequestedByUserId) : IRequest<AgentAutoTestResult>;
