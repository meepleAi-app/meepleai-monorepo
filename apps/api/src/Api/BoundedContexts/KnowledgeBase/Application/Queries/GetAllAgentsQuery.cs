using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get all agents with optional filtering.
/// Issue 866: AI Agents Entity and Configuration
/// </summary>
internal record GetAllAgentsQuery(
    bool? ActiveOnly = null,
    string? Type = null
) : IRequest<List<AgentDto>>;
