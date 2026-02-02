using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve available AI models with optional tier filtering.
/// Issue #3377: Models Tier Endpoint
/// </summary>
/// <param name="Tier">Optional tier filter (free, normal, premium, custom). Returns models at this tier and below.</param>
public record GetAvailableModelsQuery(string? Tier = null) : IRequest<GetModelsResponse>;
