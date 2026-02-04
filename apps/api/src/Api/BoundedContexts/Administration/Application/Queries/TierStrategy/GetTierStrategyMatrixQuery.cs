using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.TierStrategy;

/// <summary>
/// Query to get the tier-strategy access matrix.
/// Returns all tier-strategy combinations with their enabled status.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
internal record GetTierStrategyMatrixQuery : IQuery<TierStrategyMatrixDto>;
