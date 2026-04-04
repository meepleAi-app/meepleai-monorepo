using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries.Tier;

/// <summary>
/// Query to retrieve all tier definitions.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
internal record GetAllTierDefinitionsQuery : IRequest<IReadOnlyList<TierDefinitionDto>>;
