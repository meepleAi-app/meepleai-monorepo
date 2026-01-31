using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetAllBadges;

/// <summary>
/// Query to retrieve all badge definitions available in the system.
/// Issue #2736: API - Badge Endpoints
/// </summary>
internal sealed record GetAllBadgesQuery : IQuery<List<BadgeDefinitionDto>>;
