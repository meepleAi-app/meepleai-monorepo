using Api.BoundedContexts.KnowledgeBase.Application.ContextEngineering.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.ContextEngineering.Queries;

/// <summary>
/// Query to retrieve available context sources and their metadata.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
/// <remarks>
/// Returns information about registered context sources including:
/// - Source identifiers and display names
/// - Default priorities
/// - Availability status
/// - Supported retrieval strategies
/// </remarks>
internal sealed record GetContextSourcesQuery : IRequest<IReadOnlyList<ContextSourceInfoDto>>;
