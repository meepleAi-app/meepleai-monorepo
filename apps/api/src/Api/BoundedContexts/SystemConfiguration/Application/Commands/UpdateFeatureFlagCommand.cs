using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to update a feature flag with optional role and tier restrictions.
/// Issue #3073: Extended to support tier-based feature flags.
/// </summary>
internal record UpdateFeatureFlagCommand(
    string FeatureName,
    bool Enabled,
    UserRole? Role = null,
    UserTier? Tier = null,
    string? UserId = null
) : ICommand<MediatR.Unit>;
