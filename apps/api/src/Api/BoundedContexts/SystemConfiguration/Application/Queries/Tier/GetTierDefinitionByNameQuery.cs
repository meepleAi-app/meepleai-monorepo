using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries.Tier;

/// <summary>
/// Query to retrieve a single tier definition by name.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
internal record GetTierDefinitionByNameQuery(string Name) : IRequest<TierDefinitionDto?>;
