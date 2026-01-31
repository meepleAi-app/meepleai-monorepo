using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to retrieve all feature flags with their current states.
/// Issue #3073: Includes tier restrictions for tier-based feature flags.
/// </summary>
internal record GetAllFeatureFlagsQuery() : IQuery<List<FeatureFlagDto>>;
