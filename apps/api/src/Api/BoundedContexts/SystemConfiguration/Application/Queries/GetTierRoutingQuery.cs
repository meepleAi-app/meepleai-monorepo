using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to get all tier routing configurations.
/// Issue #2596: LLM tier routing admin overview.
/// </summary>
internal sealed record GetTierRoutingQuery : IQuery<TierRoutingListDto>;
