using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.TierStrategy;

/// <summary>
/// Query to get all strategy-to-model mappings.
/// Returns configuration of which models are used for each strategy.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
internal record GetStrategyModelMappingsQuery : IQuery<IReadOnlyList<StrategyModelMappingDto>>;
