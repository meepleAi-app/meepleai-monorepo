using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to check if a feature is enabled for a specific role.
/// Hierarchy: Role-specific flag > Global flag > Default false.
/// </summary>
internal record IsFeatureEnabledQuery(
    string FeatureName,
    UserRole? Role = null
) : IQuery<bool>;
