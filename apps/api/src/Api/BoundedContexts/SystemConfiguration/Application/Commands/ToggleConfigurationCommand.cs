using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

internal record ToggleConfigurationCommand(
    Guid ConfigId,
    bool IsActive
) : ICommand<ConfigurationDto>;
