using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

internal record ToggleFeatureFlagCommand(
    Guid FlagId
) : ICommand<FeatureFlagDto>;
